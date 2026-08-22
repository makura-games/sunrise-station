using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Server._Sunrise.ImmortalGrid;
using Content.Server._Sunrise.TransitHub;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Parallax;
using Content.Server.Pinpointer;
using Content.Server.RoundEnd;
using Content.Server.Screens.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.AlwaysPoweredMap;
using Content.Shared._Sunrise.UnbuildableGrid;
using Content.Shared.Access.Systems;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Localizations;
using Content.Shared.Parallax;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Tag;
using Content.Shared.Tiles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Parallax.Biomes;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem : SharedEmergencyShuttleSystem
{
    /*
     * Handles the escape shuttle + CentCom.
     */

    [Dependency] private IAdminLogManager _logger = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private AccessReaderSystem _reader = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private CommunicationsConsoleSystem _commsConsole = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IdCardSystem _idSystem = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private BiomeSystem _biomes = default!;

    private const float ShuttleSpawnBuffer = 1f;

    // Sunrise-start
    public TimeSpan? DockTime;
    // Sunrise-end

    private bool _emergencyShuttleEnabled;

    private static readonly ProtoId<TagPrototype> DockTag = "DockEmergency";

    public override void Initialize()
    {
        base.Initialize();

        _emergencyShuttleEnabled = ConfigManager.GetCVar(CCVars.EmergencyShuttleEnabled);
        // Don't immediately invoke as roundstart will just handle it.
        Subs.CVar(ConfigManager, CCVars.EmergencyShuttleEnabled, SetEmergencyShuttleEnabled);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<StationEmergencyShuttleComponent, StationPostInitEvent>(OnStationStartup);
        SubscribeLocalEvent<StationTransitHubComponent, ComponentShutdown>(OnCentcommShutdown); // Sunrise-Edit
        SubscribeLocalEvent<StationTransitHubComponent, MapInitEvent>(OnTransitHubInit); // Sunrise-Edit

        SubscribeLocalEvent<EmergencyShuttleComponent, FTLStartedEvent>(OnEmergencyFTL);
        SubscribeLocalEvent<EmergencyShuttleComponent, FTLCompletedEvent>(OnEmergencyFTLComplete);
        SubscribeNetworkEvent<EmergencyShuttleRequestPositionMessage>(OnShuttleRequestPosition);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded); // Sunrise-edit
        InitializeEmergencyConsole();
    }

    // Sunrise-start
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        DockTime = null;
    }
    // Sunrise-end

    private void OnRoundStart(RoundStartingEvent ev)
    {
        CleanupEmergencyConsole();
        _roundEndCancelToken = new CancellationTokenSource();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _roundEndCancelToken?.Cancel();
        _roundEndCancelToken = null;
    }

    // Sunrise edit start - TransitHub shutdown handlers moved to partial class EmergencyShuttleSystem.TransitHub.cs
    // Sunrise edit end

    /// <summary>
    ///     Attempts to get the EntityUid of the emergency shuttle
    /// </summary>
    public EntityUid? GetShuttle()
    {
        AllEntityQuery<EmergencyShuttleComponent>().MoveNext(out var shuttle, out _);
        return shuttle;
    }

    private void SetEmergencyShuttleEnabled(bool value)
    {
        if (_emergencyShuttleEnabled == value)
            return;

        _emergencyShuttleEnabled = value;

        if (value)
        {
            SetupEmergencyShuttle();
        }
        else
        {
            CleanupEmergencyShuttle();
        }
    }

    private void CleanupEmergencyShuttle()
    {
        var query = AllEntityQuery<StationTransitHubComponent>(); // Sunrise-Edit

        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<StationTransitHubComponent>(uid); // Sunrise-Edit
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // Don't handle any of this logic if in lobby
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
            UpdateEmergencyConsole(frameTime);
    }

    /// <summary>
    ///     If the client is requesting debug info on where an emergency shuttle would dock.
    /// </summary>
    private void OnShuttleRequestPosition(EmergencyShuttleRequestPositionMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.IsAdmin(args.SenderSession))
            return;

        var player = args.SenderSession.AttachedEntity;
        if (player is null)
            return;

        var station = _station.GetOwningStation(player.Value);

        if (!TryComp<StationEmergencyShuttleComponent>(station, out var stationShuttle) ||
            !HasComp<ShuttleComponent>(stationShuttle.EmergencyShuttle))
        {
            return;
        }

        var targetGrid = _station.GetLargestGrid(station.Value);
        if (targetGrid == null)
            return;

        var config = _dock.GetDockingConfig(stationShuttle.EmergencyShuttle.Value, targetGrid.Value, DockTag, true);
        if (config == null)
            return;

        foreach (var configDock in config.Docks)
        {
            _dock.Undock((configDock.DockBUid, configDock.DockB));
        }

        RaiseNetworkEvent(new EmergencyShuttlePositionMessage()
        {
            StationUid = GetNetEntity(targetGrid),
            Position = config.Area,
        });
    }

    /// <summary>
    ///     Escape shuttle FTL event handler. The only escape shuttle FTL transit should be from station to centcomm at round end
    /// </summary>
    private void OnEmergencyFTL(EntityUid uid, EmergencyShuttleComponent component, ref FTLStartedEvent args)
    {
        var ftlTime = TimeSpan.FromSeconds
        (
            TryComp<FTLComponent>(uid, out var ftlComp) ? ftlComp.TravelTime : _shuttle.DefaultTravelTime
        );

        if (TryComp<DeviceNetworkComponent>(uid, out var netComp))
        {
            var payload = new NetworkPayload
            {
                [ShuttleTimerMasks.ShuttleMap] = uid,
                [ShuttleTimerMasks.SourceMap] = args.FromMapUid,
                [ShuttleTimerMasks.DestMap] = _transformSystem.GetMap(args.TargetCoordinates),
                [ShuttleTimerMasks.ShuttleTime] = ftlTime,
                [ShuttleTimerMasks.SourceTime] = ftlTime,
                [ShuttleTimerMasks.DestTime] = ftlTime
            };
            _deviceNetworkSystem.QueuePacket(uid, null, payload, netComp.TransmitFrequency);
        }
    }

    /// <summary>
    ///     When the escape shuttle finishes FTL (docks at centcomm), have the timers display the round end countdown
    /// </summary>
    private void OnEmergencyFTLComplete(EntityUid uid, EmergencyShuttleComponent component, ref FTLCompletedEvent args)
    {
        var countdownTime = TimeSpan.FromSeconds(ConfigManager.GetCVar(CCVars.RoundRestartTime));
        var shuttle = args.Entity;
        if (TryComp<DeviceNetworkComponent>(shuttle, out var net))
        {
            var payload = new NetworkPayload
            {
                [ShuttleTimerMasks.ShuttleMap] = shuttle,
                [ShuttleTimerMasks.SourceMap] = _roundEnd.GetTransitHub(), // Sunrise-Edit
                [ShuttleTimerMasks.DestMap] = _roundEnd.GetStation(),
                [ShuttleTimerMasks.ShuttleTime] = countdownTime,
                [ShuttleTimerMasks.SourceTime] = countdownTime,
                [ShuttleTimerMasks.DestTime] = countdownTime,
            };

            // by popular request
            // https://discord.com/channels/310555209753690112/770682801607278632/1189989482234126356
            if (_random.Next(1000) == 0)
            {
                payload.Add(ScreenMasks.Text, ShuttleTimerMasks.Kill);
                payload.Add(ScreenMasks.Color, Color.Red);
            }
            else
                payload.Add(ScreenMasks.Text, ShuttleTimerMasks.Bye);

            _deviceNetworkSystem.QueuePacket(shuttle, null, payload, net.TransmitFrequency);
        }
    }

    /// <summary>
    ///     Attempts to dock a station's emergency shuttle.
    /// </summary>
    /// <seealso cref="DockEmergencyShuttle"/>
    public ShuttleDockResult? DockSingleEmergencyShuttle(EntityUid stationUid, StationEmergencyShuttleComponent? stationShuttle = null)
    {
        if (!Resolve(stationUid, ref stationShuttle))
            return null;

        if (!TryComp<TransformComponent>(stationShuttle.EmergencyShuttle, out var xform) || // Sunrise-Edit
            !TryComp<ShuttleComponent>(stationShuttle.EmergencyShuttle, out var shuttle))
        {
            Log.Error($"Attempted to call an emergency shuttle for an uninitialized station? Station: {ToPrettyString(stationUid)}. Shuttle: {ToPrettyString(stationShuttle.EmergencyShuttle)}");
            return null;
        }

        var targetGrid = _station.GetLargestGrid(stationUid);

        // Sunrise-start
        DockTime = _timing.CurTime;
        // Sunrise-end

        // UHH GOOD LUCK
        if (targetGrid == null)
        {
            _logger.Add(
                LogType.EmergencyShuttle,
                LogImpact.High,
                $"Emergency shuttle {ToPrettyString(stationUid)} unable to dock with station {ToPrettyString(stationUid)}");

            return new ShuttleDockResult
            {
                Station = (stationUid, stationShuttle),
                ResultType = ShuttleDockResultType.GoodLuck,
            };
        }

        ShuttleDockResultType resultType;
        if (_shuttle.TrySunriseFtlDock(stationShuttle.EmergencyShuttle.Value, shuttle, targetGrid.Value, out var config, DockTag, true, true)) // Sunrise-Edit - используем FTL-расширение из partial-класса.
        {
            _logger.Add(
                LogType.EmergencyShuttle,
                LogImpact.High,
                $"Emergency shuttle {ToPrettyString(stationUid)} docked with stations");

            resultType = _dock.IsConfigPriority(config, DockTag)
                ? ShuttleDockResultType.PriorityDock
                : ShuttleDockResultType.OtherDock;
        }
        else
        {
            _logger.Add(
                LogType.EmergencyShuttle,
                LogImpact.High,
                $"Emergency shuttle {ToPrettyString(stationUid)} unable to find a valid docking port for {ToPrettyString(stationUid)}");

            resultType = ShuttleDockResultType.NoDock;
        }

        return new ShuttleDockResult
        {
            Station = (stationUid, stationShuttle),
            DockingConfig = config,
            ResultType = resultType,
            TargetGrid = targetGrid,
        };
    }

    /// <summary>
    /// Do post-shuttle-dock setup. Announce to the crew and set up shuttle timers.
    /// </summary>
    public void AnnounceShuttleDock(ShuttleDockResult result, bool extended)
    {
        var stationShuttleComp = result.Station.Comp;
        var shuttle = result.Station.Comp.EmergencyShuttle;

        DebugTools.Assert(shuttle != null);

        if (result.ResultType == ShuttleDockResultType.GoodLuck)
        {
            _chatSystem.DispatchStationAnnouncement(
                result.Station,
                Loc.GetString(stationShuttleComp.FailureAnnouncement),
                playDefault: false,
                announcementSound: stationShuttleComp.FailureAudio);

            // TODO: Need filter extensions or something don't blame me.
            //_audio.PlayGlobal(stationShuttleComp.FailureAudio, Filter.Broadcast(), true);
            return;
        }

        DebugTools.Assert(result.TargetGrid != null);

        // Send station announcement.

        var targetXform = Transform(result.TargetGrid.Value);
        var angle = _dock.GetAngle(
            shuttle.Value,
            Transform(shuttle.Value),
            result.TargetGrid.Value,
            targetXform);

        var direction = ContentLocalizationManager.FormatDirection(angle.GetDir());
        var location = FormattedMessage.RemoveMarkupPermissive(
            _navMap.GetNearestBeaconString((shuttle.Value, Transform(shuttle.Value))));

        var extendedText = extended ? Loc.GetString(stationShuttleComp.LaunchExtendedMessage) : "";
        var locKey = result.ResultType == ShuttleDockResultType.NoDock
            ? stationShuttleComp.NearbyAnnouncement
            : stationShuttleComp.DockedAnnouncement;

        _chatSystem.DispatchStationAnnouncement(
            result.Station,
            Loc.GetString(
                locKey,
                ("time", $"{_consoleAccumulator:0}"),
                ("direction", direction),
                ("location", location),
                ("extended", extendedText)),
            playDefault: false);

        // Trigger shuttle timers on the shuttle.

        var time = TimeSpan.FromSeconds(_consoleAccumulator);
        if (TryComp<DeviceNetworkComponent>(shuttle, out var netComp))
        {
            var payload = new NetworkPayload
            {
                [ShuttleTimerMasks.ShuttleMap] = shuttle,
                [ShuttleTimerMasks.SourceMap] = targetXform.MapUid,
                [ShuttleTimerMasks.DestMap] = _roundEnd.GetTransitHub(),
                [ShuttleTimerMasks.ShuttleTime] = time,
                [ShuttleTimerMasks.SourceTime] = time,
                [ShuttleTimerMasks.DestTime] = time + TimeSpan.FromSeconds(TransitTime),
                [ShuttleTimerMasks.Docked] = true,
            };
            _deviceNetworkSystem.QueuePacket(shuttle.Value, null, payload, netComp.TransmitFrequency);
        }

        // Play announcement audio.

        // Sunrise-Edit
        // var audioFile = result.ResultType == ShuttleDockResultType.NoDock
        //     ? stationShuttleComp.NearbyAudio
        //     : stationShuttleComp.DockedAudio;
        //
        // TODO: Need filter extensions or something don't blame me.
        //_audio.PlayGlobal(audioFile, Filter.Broadcast(), true);
    }

    // Sunrise edit start - OnTransitHubInit moved to EmergencyShuttleSystem.TransitHub.cs
    // Sunrise edit end

    private void OnStationStartup(Entity<StationEmergencyShuttleComponent> ent, ref StationPostInitEvent args)
    {
        AddEmergencyShuttle((ent, ent));
    }

    /// <summary>
    /// Teleports the emergency shuttle to its station and starts the countdown until it launches.
    /// </summary>
    /// <remarks>
    /// If the emergency shuttle is disabled, this immediately ends the round.
    /// </remarks>
    public void DockEmergencyShuttle()
    {
        if (EmergencyShuttleArrived)
            return;

        if (!_emergencyShuttleEnabled)
        {
            _roundEnd.EndRound();
            return;
        }

        _consoleAccumulator = ConfigManager.GetCVar(CCVars.EmergencyShuttleDockTime);
        EmergencyShuttleArrived = true;

        var query = AllEntityQuery<StationEmergencyShuttleComponent>();

        var dockResults = new List<ShuttleDockResult>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (DockSingleEmergencyShuttle(uid, comp) is { } dockResult)
                dockResults.Add(dockResult);
        }

        // Make the shuttle wait longer if it couldn't dock in the normal spot.
        // We have to handle the possibility of there being multiple stations, so since the shuttle timer is global,
        // use the WORST value we have.
        var worstResult = dockResults.Max(x => x.ResultType);
        var multiplier = worstResult switch
        {
            ShuttleDockResultType.OtherDock => ConfigManager.GetCVar(
                CCVars.EmergencyShuttleDockTimeMultiplierOtherDock),
            ShuttleDockResultType.NoDock => ConfigManager.GetCVar(
                CCVars.EmergencyShuttleDockTimeMultiplierNoDock),
            // GoodLuck doesn't get a multiplier.
            // Quite frankly at that point the round is probably so fucked that you'd rather it be over ASAP.
            _ => 1,
        };

        _consoleAccumulator *= multiplier;

        foreach (var shuttleDockResult in dockResults)
        {
            AnnounceShuttleDock(shuttleDockResult, multiplier > 1);
        }

        _commsConsole.UpdateCommsConsoleInterface();
    }

    private void SetupEmergencyShuttle()
    {
        if (!_emergencyShuttleEnabled)
            return;

        var centcommQuery = AllEntityQuery<StationTransitHubComponent>(); // Sunrise-Edit

        while (centcommQuery.MoveNext(out var uid, out var centcomm))
        {
            AddTransitHub(uid, centcomm); // Sunrise-Edit
        }

        var query = AllEntityQuery<StationEmergencyShuttleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            AddEmergencyShuttle((uid, comp));
        }
    }

    // Sunrise edit start - AddTransitHub and GetTransitHubMaps moved to EmergencyShuttleSystem.TransitHub.cs
    // Sunrise edit end

    private void AddEmergencyShuttle(Entity<StationEmergencyShuttleComponent?, StationTransitHubComponent?> ent) // Sunrise-edit
    {
        if (!Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2))
            return;

        if (!_emergencyShuttleEnabled)
            return;

        if (ent.Comp1.EmergencyShuttle != null)
        {
            if (Exists(ent.Comp1.EmergencyShuttle))
            {
                Log.Error($"Attempted to add an emergency shuttle to {ToPrettyString(ent)}, despite a shuttle already existing?");
                return;
            }

            Log.Error($"Encountered deleted emergency shuttle during initialization of {ToPrettyString(ent)}");
            ent.Comp1.EmergencyShuttle = null;
        }

        if (!TryComp(ent.Comp2.MapEntity, out MapComponent? map))
        {
            Log.Error($"Failed to add emergency shuttle - transit hub has not been initialized? {ToPrettyString(ent)}");
            return;
        }

        // Load escape shuttle
        var shuttlePath = ent.Comp1.EmergencyShuttlePath;

        // Sunrise-start
        var mapId = _mapManager.CreateMap();

        var mapOptions = new DeserializationOptions {};
        if (!_loader.TryLoadGrid(mapId, shuttlePath, out var shuttle, mapOptions))
        {
            Log.Error($"Unable to spawn emergency shuttle {shuttlePath} for {ToPrettyString(ent)}");
            return;
        }

        ent.Comp1.EmergencyShuttle = shuttle;
        EnsureComp<ProtectedGridComponent>(shuttle.Value.Owner);
        EnsureComp<PreventPilotComponent>(shuttle.Value.Owner);
        EnsureComp<EmergencyShuttleComponent>(shuttle.Value.Owner);
        EnsureComp<UnbuildableGridComponent>(shuttle.Value.Owner); // Sunrise-edit

        // var docks = new HashSet<Entity<DockingComponent>>();
        // _lookup.GetChildEntities(shuttle.Value.Owner, docks);
        // foreach (var dock in docks)
        // {
        //     var airlock = EnsureComp<AirlockComponent>(dock);
        //     _airlockSystem.SetSafety(airlock, false);
        //     var door = EnsureComp<DoorComponent>(dock);
        //     door.ForcedCrushClose = true;
        //     door.CrushDamage = new DamageSpecifier(_prototypeManager.Index<DamageTypePrototype>("Blunt"), 1000);
        // }

        // Sunrise-end
        Log.Info($"Added emergency shuttle {ToPrettyString(shuttle)} for station {ToPrettyString(ent)} and centcomm {ToPrettyString(ent.Comp2.Entity)}");
    }

    /// <summary>
    /// Returns whether a target is escaping on the emergency shuttle, but only if evac has arrived.
    /// </summary>
    public bool IsTargetEscaping(EntityUid target)
    {
        // if evac isn't here then sitting in a pod doesn't return true
        if (!EmergencyShuttleArrived)
            return false;

        // check if target is on an emergency shuttle
        var xform = Transform(target);

        if (HasComp<EmergencyShuttleComponent>(xform.GridUid))
            return true;

        return false;
    }

    private bool IsOnGrid(TransformComponent xform, EntityUid shuttle, MapGridComponent? grid = null, TransformComponent? shuttleXform = null)
    {
        if (!Resolve(shuttle, ref grid, ref shuttleXform))
            return false;

        return _transformSystem.GetWorldMatrix(shuttleXform).TransformBox(grid.LocalAABB).Contains(_transformSystem.GetWorldPosition(xform));
    }

    /// <summary>
    /// A result of a shuttle dock operation done by <see cref="EmergencyShuttleSystem.DockSingleEmergencyShuttle"/>.
    /// </summary>
    /// <seealso cref="ShuttleDockResultType"/>
    public sealed class ShuttleDockResult
    {
        /// <summary>
        /// The station for which the emergency shuttle got docked.
        /// </summary>
        public Entity<StationEmergencyShuttleComponent> Station;

        /// <summary>
        /// The target grid of the station that the shuttle tried to dock to.
        /// </summary>
        /// <remarks>
        /// Not present if <see cref="ResultType"/> is <see cref="ShuttleDockResultType.GoodLuck"/>.
        /// </remarks>
        public EntityUid? TargetGrid;

        /// <summary>
        /// Enum code describing the dock result.
        /// </summary>
        public ShuttleDockResultType ResultType;

        /// <summary>
        /// The docking config used to actually dock to the station.
        /// </summary>
        /// <remarks>
        /// Only present if <see cref="ResultType"/> is <see cref="ShuttleDockResultType.PriorityDock"/>
        /// or <see cref="ShuttleDockResultType.NoDock"/>.
        /// </remarks>
        public DockingConfig? DockingConfig;
    }

    /// <summary>
    /// Emergency shuttle dock result codes used by <see cref="ShuttleDockResult"/>.
    /// </summary>
    public enum ShuttleDockResultType : byte
    {
        // This enum is ordered from "best" to "worst". This is used to sort the results.

        /// <summary>
        /// The shuttle was docked at a priority dock, which is the intended destination.
        /// </summary>
        PriorityDock,

        /// <summary>
        /// The shuttle docked at another dock on the station then the intended priority dock.
        /// </summary>
        OtherDock,

        /// <summary>
        /// The shuttle couldn't find any suitable dock on the station at all, it did not dock.
        /// </summary>
        NoDock,

        /// <summary>
        /// No station grid was found at all, shuttle did not get moved.
        /// </summary>
        GoodLuck,
    }
}
