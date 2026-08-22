using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server._Sunrise.TTS;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server._Sunrise.Auth;
using Content.Shared._Sunrise.TTS;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Eui;
using Content.Shared._Sunrise.Tutorial.EntitySystems;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Robust.Shared.Map.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Configuration;
using Robust.Server.GameStates;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;

namespace Content.Server._Sunrise.Tutorial;

/// <summary>
/// Server-side tutorial controller for session creation, map loading, completion persistence, chat, and TTS.
/// </summary>
public sealed partial class TutorialSystem : SharedTutorialSystem
{
    [Dependency] private TTSSystem _tts = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private AccountCreationManager _accountCreation = default!;
    [Dependency] private TutorialMetricsSystem _tutorialMetrics = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _cooldown;
    private int _maxTutorials;
    private Dictionary<NetUserId, TimeSpan> _cooldownData = new();
    private EntityUid? _tutorialMap;
    private readonly Dictionary<ICommonSession, TutorialCompletionEui> _completionEuis = new();
    private readonly Dictionary<NetUserId, int> _tutorialTtsRevisions = new();
    private readonly HashSet<NetUserId> _pendingCompletionRespawns = [];
    private readonly HashSet<NetUserId> _activeTutorialTransitions = [];
    private readonly Dictionary<NetUserId, EntityUid> _pendingDetachedTutorialLobbies = [];
    private readonly List<DetachedTutorialLobbyRequest> _pendingDetachedTutorialLobbyBuffer = [];

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, SunriseCCVars.TutorialCooldown, v => _cooldown = v, true);
        Subs.CVar(_cfg, SunriseCCVars.TutorialMaxActive, v => _maxTutorials = v, true);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<TutorialPlayerComponent, TutorialStepChangedEvent>(OnStepChanged);
        SubscribeLocalEvent<TutorialPlayerComponent, TutorialStepsCompletedEvent>(OnStepsCompleted);
        SubscribeLocalEvent<TutorialPlayerComponent, TutorialEndedEvent>(OnTutorialComplete);

        SubscribeLocalEvent<TutorialPlayerComponent, ExpandPvsEvent>(OnExpandPvs);

        SubscribeNetworkEvent<TutorialQuitRequestEvent>(OnTutorialQuitRequest);
        SubscribeNetworkEvent<TutorialStartRequestEvent>(OnStartRequest);
        SubscribeNetworkEvent<TutorialWindowDataRequestEvent>(OnWindowDataRequest);
    }
    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
        _pendingCompletionRespawns.Clear();
        _completionEuis.Clear();
        _tutorialTtsRevisions.Clear();
        _cooldownData.Clear();
        _activeTutorialTransitions.Clear();
        _pendingDetachedTutorialLobbies.Clear();
        _pendingDetachedTutorialLobbyBuffer.Clear();
    }

    public override void Update(float frameTime)
    {
        ProcessPendingDetachedTutorialLobbies();

        base.Update(frameTime);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        TryQueueCompletedTutorialRespawn(args.Session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        SchedulePendingRespawnAfterAttach(args.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (_activeTutorialTransitions.Contains(args.Player.UserId))
            return;

        if (!TryComp(args.Entity, out TutorialPlayerComponent? tutorialPlayer) ||
            !tutorialPlayer.TutorialInitialized)
        {
            return;
        }

        QueueDetachedTutorialLobby(args.Entity, args.Player);
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        _pendingCompletionRespawns.Clear();
        _activeTutorialTransitions.Clear();
        _pendingDetachedTutorialLobbies.Clear();
        _pendingDetachedTutorialLobbyBuffer.Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _pendingCompletionRespawns.Clear();
        _activeTutorialTransitions.Clear();
        _pendingDetachedTutorialLobbies.Clear();
        _pendingDetachedTutorialLobbyBuffer.Clear();
    }

    private void OnExpandPvs(Entity<TutorialPlayerComponent> ent, ref ExpandPvsEvent args)
    {
        args.Entities ??= [];

        // Tutorial bubbles and highlighted targets may be outside normal PVS,
        // but the client still needs them for UI anchors and path rendering.
        if (Exists(ent.Comp.CurrentBubbleTarget))
            args.Entities.Add(ent.Comp.CurrentBubbleTarget.Value);

        if (Exists(ent.Comp.Target))
            args.Entities.Add(ent.Comp.Target.Value);
    }

    private void OnStepsCompleted(Entity<TutorialPlayerComponent> ent, ref TutorialStepsCompletedEvent args)
    {
        if (!_player.TryGetSessionByEntity(ent, out var session) || session.Status != SessionStatus.InGame)
        {
            if (TryGetTutorialUserId(ent, out var userId))
            {
                SaveTutorialCompletion(userId, ent.Comp.SequenceId);
                QueueRespawnAfterTutorialCompletion(userId);
                EndTutorial(ent);
            }

            return;
        }

        _tutorialTtsRevisions.Remove(session.UserId);
        StopTutorialTts(session);
        SaveTutorialCompletion(session.UserId, ent.Comp.SequenceId);
        ShowCompletionEui(ent.Owner, session);
    }

    private void OnTutorialComplete(Entity<TutorialPlayerComponent> ent, ref TutorialEndedEvent args)
    {
        var hasUserId = TryGetTutorialUserId(ent, out var userId);

        QueueDel(ent.Comp.Grid);
        QueueDel(ent);

        if (!_player.TryGetSessionByEntity(ent, out var session) || session.Status != SessionStatus.InGame)
        {
            if (hasUserId)
                QueueRespawnAfterTutorialCompletion(userId);

            return;
        }

        _cooldownData[userId] = _timing.CurTime + _cooldown;

        _tutorialTtsRevisions.Remove(session.UserId);
        RespawnAfterTutorialCompletion(session);
    }

    private void RespawnAfterTutorialCompletion(ICommonSession session)
    {
        _pendingCompletionRespawns.Remove(session.UserId);
        StopTutorialTts(session);
        CloseCompletionEui(session);
        _ticker.Respawn(session);
    }

    private bool TryRespawnAfterTutorialCompletion(ICommonSession session)
    {
        if (!CanRespawnAfterTutorialCompletion(session))
            return false;

        RespawnAfterTutorialCompletion(session);
        return true;
    }

    private void SchedulePendingRespawnAfterAttach(ICommonSession session)
    {
        if (!_pendingCompletionRespawns.Contains(session.UserId))
            return;

        RespawnAfterTutorialCompletionWhenReady(session);
    }

    private async void RespawnAfterTutorialCompletionWhenReady(ICommonSession session)
    {
        if (!CanRespawnAfterTutorialCompletion(session))
            return;

        // When respawn, we have to wait for DB
        try
        {
            await _userDb.WaitLoadComplete(session);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to wait for user DB load for tutorial respawn: {e}");
            return;
        }

        TryRespawnAfterTutorialCompletion(session);
    }

    private bool CanRespawnAfterTutorialCompletion(ICommonSession session)
    {
        return session.Status == SessionStatus.InGame &&
               _pendingCompletionRespawns.Contains(session.UserId);
    }

    private void QueueRespawnAfterTutorialCompletion(NetUserId userId)
    {
        _pendingCompletionRespawns.Add(userId);
    }

    private bool TryQueueCompletedTutorialRespawn(ICommonSession session)
    {
        if (session.AttachedEntity is not { } uid)
            return false;

        if (!TryComp(uid, out TutorialPlayerComponent? comp))
            return false;

        if (!comp.TutorialInitialized || !IsTutorialStepsCompleted((uid, comp)))
            return false;

        QueueRespawnAfterTutorialCompletion(session.UserId);
        EndTutorial((uid, comp));
        return true;
    }

    private void QueueDetachedTutorialLobby(EntityUid uid, ICommonSession session)
    {
        _pendingDetachedTutorialLobbies[session.UserId] = uid;
    }

    private void ProcessPendingDetachedTutorialLobbies()
    {
        if (_pendingDetachedTutorialLobbies.Count == 0)
            return;

        _pendingDetachedTutorialLobbyBuffer.Clear();
        foreach (var (userId, uid) in _pendingDetachedTutorialLobbies)
        {
            _pendingDetachedTutorialLobbyBuffer.Add(new DetachedTutorialLobbyRequest(userId, uid));
        }

        _pendingDetachedTutorialLobbies.Clear();

        for (var i = 0; i < _pendingDetachedTutorialLobbyBuffer.Count; i++)
        {
            var pending = _pendingDetachedTutorialLobbyBuffer[i];

            if (!_player.TryGetSessionById(pending.UserId, out var session))
                continue;

            SendDetachedTutorialPlayerToLobby(pending.TutorialEntity, session);
        }

        _pendingDetachedTutorialLobbyBuffer.Clear();
    }

    private void SendDetachedTutorialPlayerToLobby(EntityUid uid, ICommonSession session)
    {
        if (session.Status != SessionStatus.InGame)
            return;

        _pendingCompletionRespawns.Remove(session.UserId);
        _cooldownData[session.UserId] = _timing.CurTime + _cooldown;
        StopTutorialTts(session);
        _tutorialTtsRevisions.Remove(session.UserId);
        CloseCompletionEui(session);

        if (TryComp(uid, out TutorialPlayerComponent? comp) && comp.TutorialInitialized)
            EndTutorial((uid, comp));

        _pendingCompletionRespawns.Remove(session.UserId);
        _ticker.Respawn(session);
    }

    private bool IsTutorialStepsCompleted(Entity<TutorialPlayerComponent> ent)
    {
        return _proto.TryIndex(ent.Comp.SequenceId, out var sequence) &&
               ent.Comp.StepIndex >= sequence.Steps.Count;
    }

    private bool TryGetTutorialUserId(EntityUid uid, out NetUserId userId)
    {
        if (_player.TryGetSessionByEntity(uid, out var session))
        {
            userId = session.UserId;
            return true;
        }

        if (_mind.TryGetMind(uid, out _, out var mind) && mind.UserId is { } mindUserId)
        {
            userId = mindUserId;
            return true;
        }

        userId = default;
        return false;
    }

    private async void SaveTutorialCompletion(NetUserId userId, ProtoId<TutorialSequencePrototype> sequenceId)
    {
        try
        {
            var createdTime = await _accountCreation.TryGetAccountCreatedTimeAsync(userId);

            TimeSpan? accountAge = null;
            if (createdTime.HasValue)
                accountAge = DateTimeOffset.UtcNow - createdTime.Value;

            await _db.AddTutorial(userId.UserId, sequenceId, accountAge);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save tutorial completion for {userId}: {e}");
        }
    }

    private void ShowCompletionEui(EntityUid player, ICommonSession session)
    {
        CloseCompletionEui(session);

        var eui = new TutorialCompletionEui(player);
        _completionEuis[session] = eui;
        _eui.OpenEui(eui, session);
    }

    private void CloseCompletionEui(ICommonSession session)
    {
        if (!_completionEuis.Remove(session, out var eui))
            return;

        if (!eui.IsShutDown)
            eui.Close();
    }

    /// <summary>
    /// Removes the tracked completion EUI when the client closes it.
    /// </summary>
    public void OnCompletionEuiClosed(ICommonSession session)
    {
        _completionEuis.Remove(session);
    }

    /// <summary>
    /// Handles actions sent by the tutorial completion EUI.
    /// </summary>
    public bool HandleCompletionAction(EntityUid player, string actionId)
    {
        if (actionId == TutorialCompletionActions.Stay)
            return true;

        if (actionId == TutorialCompletionActions.Next)
            return TryStartNextTutorial(player);

        if (actionId != TutorialCompletionActions.Leave)
            return false;

        if (!TryComp(player, out TutorialPlayerComponent? comp))
            return false;

        EndTutorial((player, comp));
        return true;
    }

    public bool TryGetNextTutorial(EntityUid player, out TutorialSequencePrototype? nextTutorial)
    {
        nextTutorial = null;

        if (!TryComp(player, out TutorialPlayerComponent? comp))
            return false;

        return TutorialPrototypeOrdering.TryGetNextTutorial(_proto, comp.SequenceId, out nextTutorial);
    }

    private bool TryStartNextTutorial(EntityUid player)
    {
        if (!TryGetNextTutorial(player, out var nextTutorial) || nextTutorial == null)
            return false;

        if (!_player.TryGetSessionByEntity(player, out var session) ||
            session.Status != SessionStatus.InGame ||
            !_mind.TryGetMind(player, out var mindId, out _))
        {
            return false;
        }

        if (!TryCreateNextTutorialPlayer(nextTutorial, out var nextPlayer, out var nextGrid))
            return false;

        if (!TryComp(player, out TutorialPlayerComponent? currentComp))
            return false;

        StopTutorialTts(session);
        _activeTutorialTransitions.Add(session.UserId);
        try
        {
            _mind.TransferTo(mindId, nextPlayer);
        }
        finally
        {
            _activeTutorialTransitions.Remove(session.UserId);
        }

        var tutorial = EnsureComp<TutorialPlayerComponent>(nextPlayer);
        tutorial.SequenceId = nextTutorial.ID;
        tutorial.Grid = nextGrid;
        EnsureComp<TutorialProgressBarComponent>(nextPlayer);
        InitializeTutorial((nextPlayer, tutorial));
        _tutorialMetrics.RecordTutorialStarted(nextTutorial.ID);

        QueueDel(currentComp.Grid);
        QueueDel(player);
        return true;
    }

    private async void OnWindowDataRequest(TutorialWindowDataRequestEvent msg, EntitySessionEventArgs args)
    {
        List<string>? completed = null;
        try
        {
            completed = await _db.GetTutorial(args.SenderSession.UserId.UserId);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to fetch tutorial completion list for {args.SenderSession.UserId}: {e}");
        }

        RaiseNetworkEvent(
            new TutorialWindowDataResponseEvent(completed ?? []),
            Filter.SinglePlayer(args.SenderSession));
    }

    private void OnStartRequest(TutorialStartRequestEvent msg, EntitySessionEventArgs args)
    {
        if (!_proto.TryIndex(msg.SequenceId, out var sequence))
            return;

        if (!CanStartTutorial(args.SenderSession, out var reason))
        {
            RaiseNetworkEvent(new TutorialStartDeniedEvent(reason), Filter.SinglePlayer(args.SenderSession));
            return;
        }

        TryCreateMap();
        var gridUid = LoadLocation(sequence.Grid);
        if (gridUid == EntityUid.Invalid)
            return;

        var spawnPoint = GetSpawnPoint(gridUid);
        if (spawnPoint == EntityUid.Invalid ||
            !TrySpawnNextTo(sequence.PlayerEntity, spawnPoint, out var uid) ||
            uid is not { } playerUid)
        {
            QueueDel(gridUid);
            return;
        }

        var (mindId, _) = _mind.CreateMind(args.SenderSession.UserId);
        _mind.SetUserId(mindId, args.SenderSession.UserId);
        _mind.TransferTo(mindId, playerUid);
        _ticker.PlayerJoinGame(args.SenderSession, true);

        var tutorial = EnsureComp<TutorialPlayerComponent>(playerUid);
        tutorial.SequenceId = msg.SequenceId;
        tutorial.Grid = gridUid;
        EnsureComp<TutorialProgressBarComponent>(playerUid);
        InitializeTutorial((playerUid, tutorial));
        _tutorialMetrics.RecordTutorialStarted(sequence.ID);
    }

    private bool TryCreateNextTutorialPlayer(
        TutorialSequencePrototype sequence,
        out EntityUid player,
        out EntityUid grid)
    {
        player = EntityUid.Invalid;
        grid = EntityUid.Invalid;

        TryCreateMap();
        grid = LoadLocation(sequence.Grid);
        if (grid == EntityUid.Invalid)
            return false;

        var spawnPoint = GetSpawnPoint(grid);
        if (spawnPoint == EntityUid.Invalid ||
            !TrySpawnNextTo(sequence.PlayerEntity, spawnPoint, out var spawned) ||
            spawned is not { } spawnedPlayer)
        {
            QueueDel(grid);
            return false;
        }

        player = spawnedPlayer;
        return true;
    }

    private void OnTutorialQuitRequest(TutorialQuitRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } entity)
            return;

        if (TryComp(entity, out TutorialPlayerComponent? comp))
        {
            EndTutorial((entity, comp));
            return;
        }

        return;
    }

    private bool CanStartTutorial(ICommonSession session, out string reason)
    {
        reason = string.Empty;

        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            reason = Loc.GetString("round-is-not-ready");
            return false;
        }

        if (session.AttachedEntity != null)
        {
            return false;
        }

        var cooldown = _cooldownData.GetValueOrDefault(session.UserId);

        if (_timing.CurTime < cooldown)
        {
            var remaining = cooldown - _timing.CurTime;
            reason = Loc.GetString("tutorial-cooldown-denied", ("cooldown", Math.Ceiling(remaining.TotalSeconds)));
            return false;
        }

        if (_maxTutorials <= 0)
            return true;

        var count = 0;
        var query = EntityQueryEnumerator<TutorialPlayerComponent>();
        while (query.MoveNext(out _))
        {
            count++;
            if (count >= _maxTutorials)
            {
                reason = Loc.GetString("tutorial-start-denied-max-active");
                return false;
            }
        }

        return true;
    }

    private async void OnStepChanged(EntityUid uid, TutorialPlayerComponent comp, TutorialStepChangedEvent args)
    {
        try
        {
            if (!_player.TryGetSessionByEntity(uid, out var session))
                return;

            var ttsRevision = StopTutorialTts(session);

            if (!TryGetCurrentStep((uid, comp), out var step))
                return;

            if (!_proto.TryIndex(step.VoiceId, out var voice))
                return;

            var sequenceId = comp.SequenceId;
            var stepIndex = comp.StepIndex;
            var senderText = Loc.GetString(step.Sender);
            var messageText = Loc.GetString(step.ChatMessage);
            var plainMessage = $"{senderText} {messageText}";
            var wrappedMessage = $"[color=#B8860B][bold]{FormattedMessage.EscapeText(senderText)}[/bold][/color] {FormattedMessage.EscapeText(messageText)}";
            _chat.ChatMessageToOne(ChatChannel.Emotes, plainMessage, wrappedMessage, EntityUid.Invalid, false, session.Channel);

            RemComp<TutorialDistanceTrackerComponent>(uid);

            var tts = await GenerateTtsForTutorial(step.TtsMessage, voice);
            if (tts == null)
                return;

            if (!IsCurrentTutorialTts(session.UserId, ttsRevision))
                return;

            if (!TryComp(uid, out TutorialPlayerComponent? currentComp))
                return;

            if (!currentComp.TutorialInitialized ||
                currentComp.StepIndex != stepIndex ||
                !currentComp.SequenceId.Equals(sequenceId))
                return;

            var ev = new PlayTTSEvent(tts, playbackGroup: TTSPlaybackGroup.Tutorial);
            RaiseNetworkEvent(ev, Filter.SinglePlayer(session));
        }
        catch (Exception e)
        {
            Log.Error($"Error in OnStepChanged: {e}");
        }
    }

    private int StopTutorialTts(ICommonSession session)
    {
        var revision = NextTutorialTtsRevision(session.UserId);
        RaiseNetworkEvent(new StopTTSEvent(TTSPlaybackGroup.Tutorial), Filter.SinglePlayer(session));
        return revision;
    }

    private int NextTutorialTtsRevision(NetUserId userId)
    {
        _tutorialTtsRevisions.TryGetValue(userId, out var revision);
        revision++;
        _tutorialTtsRevisions[userId] = revision;
        return revision;
    }

    private bool IsCurrentTutorialTts(NetUserId userId, int revision)
    {
        return _tutorialTtsRevisions.TryGetValue(userId, out var currentRevision) &&
               currentRevision == revision;
    }

    private async Task<byte[]?> GenerateTtsForTutorial(string text, TTSVoicePrototype voicePrototype)
    {
        if (!_cfg.GetCVar(SunriseCCVars.TTSEnabled))
            return null;

        try
        {
            return await _tts.GenerateTTS(Loc.GetString(text), voicePrototype, null);
        }
        catch (Exception e)
        {
            Log.Error($"TTS System error in tutorial generation: {e.Message}");
        }
        return null;
    }
    protected override void UpdateTimeCounter(Entity<TutorialPlayerComponent> ent, TimeSpan? endTime)
    {
        base.UpdateTimeCounter(ent, endTime);

        if (endTime == null)
        {
            RemComp<TimeCounterComponent>(ent);
            return;
        }

        var counter = EnsureComp<TimeCounterComponent>(ent);
        counter.EndTime = endTime;
        Dirty(ent, counter);
    }

    private EntityUid? TryCreateMap()
    {
        if (Exists(_tutorialMap))
            return _tutorialMap;

        var mapUid = _mapSystem.CreateMap();

        var comp = EnsureComp<TutorialMapComponent>(mapUid);
        _meta.SetEntityName(mapUid, comp.MapName);
        _tutorialMap = mapUid;

        return mapUid;
    }

    private EntityUid LoadLocation(ResPath gridPath)
    {
        if (!TryComp<MapComponent>(_tutorialMap, out var mapComp) ||
            !TryComp<TutorialMapComponent>(_tutorialMap, out var tutorialMap))
            return EntityUid.Invalid;

        CleanupDeletedGrids(tutorialMap);

        var offset = Vector2.Zero;

        if (tutorialMap.LoadedGrids.Count != 0)
        {
            var lastGrid = tutorialMap.LoadedGrids[^1];
            offset = tutorialMap.GridOffsets[lastGrid] + tutorialMap.CoordinateStep;
        }

        // Each tutorial grid is placed at a stable offset on one shared map so
        // multiple active tutorial sessions do not overlap.
        if (!_mapLoader.TryLoadGrid(mapComp.MapId, gridPath, out var grid, null, offset))
            return EntityUid.Invalid;

        tutorialMap.LoadedGrids.Add(grid.Value);
        tutorialMap.GridOffsets.Add(grid.Value, offset);

        return grid.Value;
    }

    private EntityUid GetSpawnPoint(EntityUid grid)
    {
        var query = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            return uid;
        }

        return EntityUid.Invalid;
    }

    private void CleanupDeletedGrids(TutorialMapComponent comp)
    {
        if (comp.LoadedGrids.Count == 0)
            return;

        for (var i = comp.LoadedGrids.Count - 1; i >= 0; i--)
        {
            var grid = comp.LoadedGrids[i];

            if (Exists(grid))
                continue;

            comp.LoadedGrids.RemoveAt(i);
            comp.GridOffsets.Remove(grid);
        }
    }

    private readonly record struct DetachedTutorialLobbyRequest(NetUserId UserId, EntityUid TutorialEntity);
}
