using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Server._Sunrise.NewLife.UI;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.NewLife;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Ghost;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Sunrise.Interfaces.Shared;

namespace Content.Server._Sunrise.NewLife;

[UsedImplicitly]
public sealed partial class NewLifeSystem : SharedNewLifeSystem
{
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IServerPreferencesManager _prefsManager = default!;
    [Dependency] private PlayTimeTrackingSystem _playTimeTrackings = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _netMgr = default!;
    [Dependency] private IGameTiming _timing = default!;
    private ISharedSponsorsManager? _sponsorsManager; // Sunrise-Sponsors

    private readonly Dictionary<ICommonSession, NewLifeEui> _openUis = new();
    public int NewLifeTimeout;
    private bool _newLifeEnable;
    private bool _newLifeSponsorOnly;

    private readonly Dictionary<NetUserId, NewLifeUserData> _newLifeRoundData = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<NewLifeOpenRequest>(OnRespawnMenuOpenRequest);
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEndSystemChange);
        _netMgr.Connecting += NetMgrOnConnecting;

        _cfg.OnValueChanged(SunriseCCVars.NewLifeTimeout, SetNewLifeTimeout, true);
        _cfg.OnValueChanged(SunriseCCVars.NewLifeEnable, SetNewLifeEnable, true);
        _cfg.OnValueChanged(SunriseCCVars.NewLifeSponsorOnly, SetNewLifeSponsorOnly, true);

        IoCManager.Instance!.TryResolveType(out _sponsorsManager); // Sunrise-Sponsors
    }

    private void OnRoundEndSystemChange(RoundEndSystemChangedEvent args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        foreach (var newLifeUserData in _newLifeRoundData)
            newLifeUserData.Value.UsedCharactersForRespawn.Clear();
    }


    private NewLifeUserData EnsureRoundData(NetUserId userId)
    {
        if (!_newLifeRoundData.TryGetValue(userId, out var data))
            _newLifeRoundData[userId] = data = new NewLifeUserData();

        return data;
    }

    public void SetNextAllowRespawn(NetUserId userId, TimeSpan nextRespawnTime)
    {
        EnsureRoundData(userId).NextAllowRespawn = nextRespawnTime;
    }

    public void AddUsedCharactersForRespawn(NetUserId userId, int usedCharacter)
    {
        EnsureRoundData(userId).UsedCharactersForRespawn.Add(usedCharacter);
    }

    private bool TryGetUsedCharactersForRespawn(NetUserId userId, [NotNullWhen(true)] out List<int>? usedCharactersForRespawn)
    {
        usedCharactersForRespawn = null;
        if (!_newLifeRoundData.TryGetValue(userId, out var data))
            return false;

        usedCharactersForRespawn = data.UsedCharactersForRespawn;
        return true;
    }

    private bool TryGetNextAllowRespawn(NetUserId userId, [NotNullWhen(true)] out TimeSpan? nextAllowRespawn)
    {
        nextAllowRespawn = null;
        if (!_newLifeRoundData.TryGetValue(userId, out var data))
            return false;

        nextAllowRespawn = data.NextAllowRespawn;
        return true;
    }

    private async Task NetMgrOnConnecting(NetConnectingArgs e)
    {
        if (!_newLifeEnable)
            return;

        if (_sponsorsManager != null && _sponsorsManager.IsAllowedRespawn(e.UserId) || !_newLifeSponsorOnly)
        {
            if (_newLifeRoundData.ContainsKey(e.UserId))
                return;

            _newLifeRoundData.Add(e.UserId, new NewLifeUserData());
        }
    }

    private void SetNewLifeTimeout(int value)
    {
        NewLifeTimeout = value;
    }

    private void SetNewLifeEnable(bool value)
    {
        _newLifeEnable = value;
    }

    private void SetNewLifeSponsorOnly(bool value)
    {
        _newLifeSponsorOnly = value;
    }

    private void OnRespawnMenuOpenRequest(NewLifeOpenRequest msg, EntitySessionEventArgs args)
    {
        OpenEui(args.SenderSession);
    }

    public void OnGhostRespawnMenuRequest(ICommonSession player, int? characterId, NetEntity? stationId, string? roleProto)
    {
        if (!_openUis.TryGetValue(player, out var eui))
            return;

        if (characterId is not { } selectedCharacterId || stationId is not { } selectedStationId || string.IsNullOrWhiteSpace(roleProto))
        {
            eui.StateDirty();
            return;
        }

        if (!TryGetEuiState(player, out var state))
        {
            CloseEui(player);
            return;
        }

        var validation = NewLifeRequestValidation.Validate(state, _timing.CurTime, selectedCharacterId, selectedStationId, roleProto);
        if (validation != NewLifeRequestValidationResult.Valid)
        {
            eui.StateDirty();
            return;
        }

        var stationUid = GetEntity(selectedStationId);
        if (stationUid == EntityUid.Invalid)
        {
            eui.StateDirty();
            return;
        }

        CloseEui(player);
        _prefsManager.GetPreferences(player.UserId).SetProfile(selectedCharacterId);
        _gameTicker.MakeJoinGame(player, stationUid, roleProto, canBeAntag: false);
    }

    public bool TryGetEuiState(ICommonSession session, [NotNullWhen(true)] out NewLifeEuiState? state)
    {
        state = null;

        if (!TryGetRespawnUiData(session, out var characterProfiles, out var stationsList, out var jobsDict,
                out var nextAllowRespawn, out var usedCharactersForRespawn))
            return false;

        state = new NewLifeEuiState(
            GetCharactersInfo(characterProfiles),
            stationsList,
            GetRolesInfo(jobsDict),
            nextAllowRespawn,
            usedCharactersForRespawn);

        return true;
    }

    private bool TryGetRespawnUiData(
        ICommonSession session,
        [NotNullWhen(true)] out IReadOnlyDictionary<int, HumanoidCharacterProfile>? characterProfiles,
        [NotNullWhen(true)] out Dictionary<NetEntity, string>? stationsList,
        [NotNullWhen(true)] out Dictionary<NetEntity, List<(JobPrototype, int?)>>? jobsDict,
        out TimeSpan nextAllowRespawn,
        [NotNullWhen(true)] out List<int>? usedCharactersForRespawn)
    {
        characterProfiles = null;
        stationsList = null;
        jobsDict = null;
        usedCharactersForRespawn = null;
        nextAllowRespawn = TimeSpan.Zero;

        if (!CanUseNewLife(session))
            return false;

        var prefs = _prefsManager.GetPreferences(session.UserId);
        characterProfiles = prefs.Characters;
        jobsDict = new Dictionary<NetEntity, List<(JobPrototype, int?)>>();
        stationsList = new Dictionary<NetEntity, string>();
        var stations = _stationSystem.GetStations();
        foreach (var stationUid in stations)
        {
            if (!HasComp<StationJobsComponent>(stationUid))
                continue;

            var availableStationJobs = new List<(JobPrototype, int?)>();
            var stationJobs = _stationJobs.GetJobs(stationUid);

            foreach (var job in stationJobs)
            {
                if (!_playTimeTrackings.IsAllowed(session, job.Key))
                    continue;

                availableStationJobs.Add((_prototypeManager.Index(job.Key), job.Value));
            }

            if (availableStationJobs.Count == 0)
                continue;

            availableStationJobs.Sort((x, y) =>
            {
                var xName = x.Item1.LocalizedName;
                var yName = y.Item1.LocalizedName;
                return -string.Compare(xName, yName, StringComparison.CurrentCultureIgnoreCase);
            });

            jobsDict.Add(GetNetEntity(stationUid), availableStationJobs);
            stationsList.Add(GetNetEntity(stationUid), MetaData(stationUid).EntityName);
        }

        if (stationsList.Count == 0 || jobsDict.Count == 0)
            return false;

        if (!TryGetNextAllowRespawn(session.UserId, out var nextAllowRespawnValue))
            return false;

        if (!TryGetUsedCharactersForRespawn(session.UserId, out usedCharactersForRespawn))
            return false;

        nextAllowRespawn = nextAllowRespawnValue.Value;
        return true;
    }

    private bool CanUseNewLife(ICommonSession session)
    {
        if (!_newLifeEnable)
            return false;

        if ((_sponsorsManager == null || !_sponsorsManager.IsAllowedRespawn(session.UserId)) && _newLifeSponsorOnly)
            return false;

        if (session.AttachedEntity is not { Valid: true } attached || !HasComp<GhostComponent>(attached))
            return false;

        return true;
    }

    private void OpenEui(ICommonSession session)
    {
        if (!TryGetEuiState(session, out _))
            return;

        if (_openUis.ContainsKey(session))
            CloseEui(session);

        var eui = _openUis[session] = new NewLifeEui();
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void CloseEui(ICommonSession session)
    {
        if (!_openUis.ContainsKey(session))
            return;

        _openUis.Remove(session, out var eui);

        eui?.Close();
    }

    public void UpdateAllEui()
    {
        foreach (var eui in _openUis.Values)
        {
            eui.StateDirty();
        }
    }

    public List<NewLifeCharacterInfo> GetCharactersInfo(IReadOnlyDictionary<int, HumanoidCharacterProfile> characterProfiles)
    {
        var characters = new List<NewLifeCharacterInfo>();

        foreach (var (charKey, characterProfile) in characterProfiles)
        {
            characters.Add(new NewLifeCharacterInfo { Identifier = charKey, Name = characterProfile.Name });
        }

        return characters;
    }

    public Dictionary<NetEntity, List<NewLifeRolesInfo>> GetRolesInfo(Dictionary<NetEntity, List<(JobPrototype, int?)>> availableStations)
    {
        var stations = new Dictionary<NetEntity, List<NewLifeRolesInfo>>();

        foreach (var station in availableStations)
        {
            var roles = new List<NewLifeRolesInfo>();

            foreach (var availableJob in station.Value)
            {
                roles.Add(new NewLifeRolesInfo { Identifier = availableJob.Item1.ID, Name = availableJob.Item1.LocalizedName, Count = availableJob.Item2 });
            }

            stations.Add(station.Key, roles);
        }

        return stations;
    }

    private void OnPlayerAttached(PlayerAttachedEvent message)
    {
        // Close the session of any player that has a ghost roles window open and isn't a ghost anymore.
        if (!_openUis.ContainsKey(message.Player))
            return;

        if (HasComp<GhostComponent>(message.Entity))
            return;

        CloseEui(message.Player);
    }
}

