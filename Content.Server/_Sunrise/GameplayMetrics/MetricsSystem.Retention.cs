using Content.Server.GameTicking;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Prometheus;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.GameplayMetrics;

/// <summary>
/// Метрики удержания: на каком этапе и по какой причине уходят игроки и как проходят первый день новые игроки.
/// </summary>
public sealed partial class MetricsSystem
{
    private static readonly TimeSpan FirstDayDuration = TimeSpan.FromDays(1);
    private static readonly TimeSpan FunnelEntryLifetime = TimeSpan.FromDays(2);

    // Явные границы, чтобы в Grafana можно было писать le="300" вместо подбора регулярок.
    private static readonly double[] DurationBuckets =
        [60, 120, 300, 600, 900, 1200, 1800, 2700, 3600, 5400, 7200, 10800, 14400];

    private static readonly double[] ShortDurationBuckets =
        [10, 30, 60, 120, 300, 600, 900, 1800, 3600];

    private static readonly Counter PlayerLeaveTotal = Metrics.CreateCounter(
        "ss14_player_leave_total",
        "Player disconnects by game stage, disconnect cause and player type.",
        new CounterConfiguration { LabelNames = ["stage", "cause", "player_type"] });

    private static readonly Histogram PlayerConnectionDurationSeconds = Metrics.CreateHistogram(
        "ss14_player_connection_duration_seconds",
        "Real time between connecting to the server and disconnecting.",
        new HistogramConfiguration { LabelNames = ["player_type"], Buckets = DurationBuckets });

    private static readonly Histogram PlayerJoiningBeforeLeaveSeconds = Metrics.CreateHistogram(
        "ss14_player_joining_before_leave_seconds",
        "Time spent connecting (queue, registration, loading) by players who left before reaching the lobby.",
        new HistogramConfiguration { LabelNames = ["player_type"], Buckets = ShortDurationBuckets });

    private static readonly Histogram PlayerLobbyWaitBeforeLeaveSeconds = Metrics.CreateHistogram(
        "ss14_player_lobby_wait_before_leave_seconds",
        "Time spent in the lobby by players who disconnected from the lobby.",
        new HistogramConfiguration { LabelNames = ["player_type"], Buckets = DurationBuckets });

    private static readonly Histogram PlayerLeaveAfterDeathSeconds = Metrics.CreateHistogram(
        "ss14_player_leave_after_death_seconds",
        "Time between the player's last death in the round and their disconnect.",
        new HistogramConfiguration { LabelNames = ["player_type"], Buckets = ShortDurationBuckets });

    private static readonly Histogram PlayerRoundTimeSeconds = Metrics.CreateHistogram(
        "ss14_player_round_time_seconds",
        "Time from spawning into the round until leaving it or the round ending.",
        new HistogramConfiguration { LabelNames = ["role", "is_antag", "outcome"], Buckets = DurationBuckets });

    private static readonly Histogram PlayerRoundTimeByTypeSeconds = Metrics.CreateHistogram(
        "ss14_player_round_time_by_type_seconds",
        "Time from spawning into the round until leaving it or the round ending, by player type.",
        new HistogramConfiguration { LabelNames = ["player_type", "outcome"], Buckets = DurationBuckets });

    private static readonly Counter PlayerRoundSpawnTotal = Metrics.CreateCounter(
        "ss14_player_round_spawn_total",
        "Player spawns into a round by gamemode, job preference fit and player type.",
        new CounterConfiguration { LabelNames = ["gamemode", "assignment", "player_type"] });

    private static readonly Counter PlayerRoundLeaveTotal = Metrics.CreateCounter(
        "ss14_player_round_leave_total",
        "Spawned players that disconnected before the round ended, by gamemode, job preference fit and player type.",
        new CounterConfiguration { LabelNames = ["gamemode", "assignment", "player_type"] });

    private static readonly Counter RoundEndPlayersTotal = Metrics.CreateCounter(
        "ss14_round_end_players_total",
        "Non-observer players in the round end summary by their final state.",
        new CounterConfiguration { LabelNames = ["state"] });

    private static readonly Counter NewPlayerFunnelTotal = Metrics.CreateCounter(
        "ss14_new_player_funnel_total",
        "Players on their first day on the server that reached each onboarding step, by account age.",
        new CounterConfiguration { LabelNames = ["step", "account"] });

    private const string StageJoining = "joining";
    private const string StageLobby = "lobby";
    private const string StageAlive = "alive";
    private const string StageCrit = "crit";
    private const string StageDead = "dead";
    private const string StageGhost = "ghost";
    private const string StagePostRound = "postround";

    private const string CauseQuit = "quit";
    private const string CauseTimeout = "timeout";
    private const string CauseAfk = "afk";
    private const string CauseRestart = "restart";
    private const string CauseOther = "other";

    private const string PlayerTypeNewAccount = "new_account";
    private const string PlayerTypeNewPlayer = "new_player";
    private const string PlayerTypeNewbie = "newbie";
    private const string PlayerTypeRegular = "regular";
    private const string PlayerTypeUnknown = "unknown";

    private const string OutcomeLeft = "left";
    private const string OutcomeRoundEnd = "round_end";

    private readonly Dictionary<NetUserId, PlayerRetentionState> _retention = new();
    private readonly Dictionary<NetUserId, NewPlayerFunnelProgress> _newPlayerFunnel = new();
    private readonly Dictionary<NetUserId, PendingLeave> _pendingLeaves = new();
    private readonly Dictionary<NetUserId, string> _pendingLeaveCauses = new();

    private TimeSpan _newAccountThreshold;
    private TimeSpan _newbiePlaytime;
    private string _afkKickReason = string.Empty;

    private void InitializeRetention()
    {
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
        _net.Disconnect += OnNetDisconnect;

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.CVar(_cfg, SunriseCCVars.MetricsNewAccountThreshold, value => _newAccountThreshold = value, true);
        Subs.CVar(_cfg, SunriseCCVars.MetricsNewbiePlaytime, value => _newbiePlaytime = value, true);

        _afkKickReason = Loc.GetString("afk-system-kick-reason");
    }

    private void ShutdownRetention()
    {
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
        _net.Disconnect -= OnNetDisconnect;

        _retention.Clear();
        _newPlayerFunnel.Clear();
        _pendingLeaves.Clear();
        _pendingLeaveCauses.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        switch (args.NewStatus)
        {
            case SessionStatus.Connected:
                OnPlayerConnected(args.Session);
                break;

            case SessionStatus.Disconnected:
                OnPlayerDisconnected(args.Session);
                break;
        }
    }

    private void OnPlayerConnected(ICommonSession session)
    {
        var userId = session.UserId;

        // Уход без события сети не должен потеряться и не должен получить чужую причину.
        if (_pendingLeaves.Remove(userId, out var staleLeave))
            PlayerLeaveTotal.WithLabels(staleLeave.Stage, CauseOther, staleLeave.PlayerType).Inc();

        _pendingLeaveCauses.Remove(userId);

        var state = new PlayerRetentionState { ConnectedAt = _gameTiming.RealTime };
        _retention[userId] = state;

        LoadFirstSeenTime(session, state);
    }

    private async void LoadFirstSeenTime(ICommonSession session, PlayerRetentionState state)
    {
        DateTime? firstSeen = null;

        try
        {
            var record = await _db.GetPlayerRecordByUserId(session.UserId);
            firstSeen = record?.FirstSeenTime.UtcDateTime;
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to load first seen time of {session} for retention metrics: {e}");
        }

        // Пока шёл запрос, игрок мог отключиться и переподключиться.
        if (!_retention.TryGetValue(session.UserId, out var current) || current != state)
            return;

        state.FirstSeenTime = firstSeen;
        state.FirstSeenLoaded = true;

        if (firstSeen is not { } firstSeenTime || DateTime.UtcNow - firstSeenTime >= FirstDayDuration)
            return;

        if (!_newPlayerFunnel.ContainsKey(session.UserId))
        {
            _newPlayerFunnel[session.UserId] = new NewPlayerFunnelProgress
            {
                FirstSeenTime = firstSeenTime,
                Account = GetAccountLabel(session),
            };
        }

        // Шаги, пройденные до ответа базы, засчитываем задним числом.
        AdvanceFunnel(session.UserId, NewPlayerFunnelStep.Connected);

        if (state.ReachedLobby)
            AdvanceFunnel(session.UserId, NewPlayerFunnelStep.Lobby);

        if (state.Round != null)
            AdvanceFunnel(session.UserId, NewPlayerFunnelStep.Spawned);
    }

    private void OnPlayerDisconnected(ICommonSession session)
    {
        var userId = session.UserId;
        if (!_retention.Remove(userId, out var state))
            return;

        var stage = GetLeaveStage(session, state);
        var playerType = GetPlayerType(session, state);
        var now = _gameTiming.RealTime;

        PlayerConnectionDurationSeconds.WithLabels(playerType).Observe((now - state.ConnectedAt).TotalSeconds);

        switch (stage)
        {
            case StageJoining:
                PlayerJoiningBeforeLeaveSeconds.WithLabels(playerType).Observe((now - state.ConnectedAt).TotalSeconds);
                break;

            case StageLobby when state.LobbyEnteredAt is { } lobbyEnteredAt:
                PlayerLobbyWaitBeforeLeaveSeconds.WithLabels(playerType).Observe((now - lobbyEnteredAt).TotalSeconds);
                break;

            case StageDead or StageGhost when state.LastDeathAt is { } lastDeathAt:
                PlayerLeaveAfterDeathSeconds.WithLabels(playerType)
                    .Observe((_gameTiming.CurTime - lastDeathAt).TotalSeconds);
                break;
        }

        // Уход посреди раунда закрывает участие в нём.
        if (state.Round is { } round)
        {
            PlayerRoundLeaveTotal.WithLabels(round.Gamemode, round.Assignment, playerType).Inc();
            FinishRoundParticipation(userId, state, round, OutcomeLeft);
        }

        // Причина приходит отдельным событием сети, порядок которого не гарантирован.
        if (_pendingLeaveCauses.Remove(userId, out var cause))
        {
            PlayerLeaveTotal.WithLabels(stage, cause, playerType).Inc();
            return;
        }

        _pendingLeaves[userId] = new PendingLeave(stage, playerType);
    }

    private void OnNetDisconnect(object? sender, NetDisconnectedArgs args)
    {
        var userId = args.Channel.UserId;
        var cause = GetLeaveCause(args);

        if (_pendingLeaves.Remove(userId, out var pending))
        {
            PlayerLeaveTotal.WithLabels(pending.Stage, cause, pending.PlayerType).Inc();
            return;
        }

        // Сохраняем причину, только если смена статуса сессии ещё впереди.
        if (_retention.ContainsKey(userId))
            _pendingLeaveCauses[userId] = cause;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!_retention.TryGetValue(ev.PlayerSession.UserId, out var state))
            return;

        state.ReachedLobby = true;
        state.LobbyEnteredAt = _gameTiming.RealTime;

        // Кешируем тип игрока, пока данные о наигранном времени точно загружены.
        GetPlayerType(ev.PlayerSession, state);
        AdvanceFunnel(ev.PlayerSession.UserId, NewPlayerFunnelStep.Lobby);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var userId = ev.Player.UserId;
        if (!_retention.TryGetValue(userId, out var state))
            return;

        state.ReachedLobby = true;

        if (state.Round == null)
        {
            var round = new RoundParticipation
            {
                SpawnedAt = _gameTiming.CurTime,
                Role = ev.JobId ?? "unknown",
                Assignment = GetAssignmentLabel(ev),
                PlayerType = GetPlayerType(ev.Player, state),
                Gamemode = GetGamemodeLabel(),
            };

            state.Round = round;
            PlayerRoundSpawnTotal.WithLabels(round.Gamemode, round.Assignment, round.PlayerType).Inc();
        }

        AdvanceFunnel(userId, NewPlayerFunnelStep.Spawned);
    }

    private void RecordRoundEndRetention(RoundEndMessageEvent ev)
    {
        foreach (var info in ev.AllPlayersEndInfo)
        {
            if (info.Observer)
                continue;

            RoundEndPlayersTotal.WithLabels(GetRoundEndStateLabel(info)).Inc();
        }

        foreach (var (userId, state) in _retention)
        {
            if (state.Round is not { } round)
                continue;

            FinishRoundParticipation(userId, state, round, OutcomeRoundEnd);
            AdvanceFunnel(userId, NewPlayerFunnelStep.RoundCompleted);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        var now = _gameTiming.RealTime;

        foreach (var (userId, state) in _retention)
        {
            // Раунд мог быть перезапущен без штатного завершения.
            if (state.Round is { } round)
                FinishRoundParticipation(userId, state, round, OutcomeRoundEnd);

            state.RoundDeaths = 0;
            state.HasDiedThisRound = false;
            state.LastDeathAt = null;

            if (state.ReachedLobby)
                state.LobbyEnteredAt = now;
        }

        var utcNow = DateTime.UtcNow;
        var expired = new List<NetUserId>();
        foreach (var (userId, progress) in _newPlayerFunnel)
        {
            if (utcNow - progress.FirstSeenTime > FunnelEntryLifetime)
                expired.Add(userId);
        }

        foreach (var userId in expired)
        {
            _newPlayerFunnel.Remove(userId);
        }
    }

    private void FinishRoundParticipation(
        NetUserId userId,
        PlayerRetentionState state,
        RoundParticipation round,
        string outcome)
    {
        var isAntag = _minds.TryGetMind(userId, out var mind) && _roles.MindIsAntagonist(mind.Value.Owner)
            ? "true"
            : "false";

        var seconds = (_gameTiming.CurTime - round.SpawnedAt).TotalSeconds;
        PlayerRoundTimeSeconds.WithLabels(round.Role, isAntag, outcome).Observe(seconds);
        PlayerRoundTimeByTypeSeconds.WithLabels(round.PlayerType, outcome).Observe(seconds);
        PlayerDeathsPerRound.WithLabels(round.Role).Observe(state.RoundDeaths);

        state.Round = null;
    }

    private void AdvanceFunnel(NetUserId userId, NewPlayerFunnelStep step)
    {
        if (!_newPlayerFunnel.TryGetValue(userId, out var progress) || (progress.Reached & step) != 0)
            return;

        progress.Reached |= step;
        NewPlayerFunnelTotal.WithLabels(GetFunnelStepLabel(step), progress.Account).Inc();
    }

    private string GetLeaveStage(ICommonSession session, PlayerRetentionState state)
    {
        if (!_gameTicker.UserHasJoinedGame(session.UserId))
            return state.ReachedLobby ? StageLobby : StageJoining;

        if (_gameTicker.RunLevel == GameRunLevel.PostRound)
            return StagePostRound;

        if (!_minds.TryGetMind(session.UserId, out var mind) || mind.Value.Comp.CurrentEntity is not { } entity)
            return StageGhost;

        if (HasComp<GhostComponent>(entity))
            return StageGhost;

        if (!TryComp<MobStateComponent>(entity, out var mobState))
            return StageAlive;

        return mobState.CurrentState switch
        {
            MobState.Critical => StageCrit,
            MobState.Dead => StageDead,
            _ => StageAlive,
        };
    }

    private string GetLeaveCause(NetDisconnectedArgs args)
    {
        if (args.Message.RedialFlag)
            return CauseRestart;

        var reason = args.Reason;

        // Строки клиента: закрытие игры и кнопка «Отключиться» в меню.
        if (reason.Contains("Client shutting down", StringComparison.Ordinal) ||
            reason.Contains("Disconnect command used", StringComparison.Ordinal))
            return CauseQuit;

        if (reason.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return CauseTimeout;

        if (_afkKickReason.Length > 0 && reason.Contains(_afkKickReason, StringComparison.Ordinal))
            return CauseAfk;

        return CauseOther;
    }

    private string GetPlayerType(ICommonSession session, PlayerRetentionState state)
    {
        var isNewAccount = IsNewAccount(session);

        if (state.FirstSeenTime is { } firstSeen && DateTime.UtcNow - firstSeen < FirstDayDuration)
            return isNewAccount ? PlayerTypeNewAccount : PlayerTypeNewPlayer;

        // Данные базы ещё не пришли, но свежий аккаунт однозначно новый игрок.
        if (!state.FirstSeenLoaded && isNewAccount)
            return PlayerTypeNewAccount;

        if (_playTime.TryGetTrackerTimes(session, out var trackers))
        {
            // Новичок без наигранного времени ещё не имеет записи общего трекера.
            trackers.TryGetValue(PlayTimeTrackingShared.TrackerOverall.Id, out var overall);
            state.CachedPlayerType = overall < _newbiePlaytime ? PlayerTypeNewbie : PlayerTypeRegular;
        }

        return state.CachedPlayerType ?? PlayerTypeUnknown;
    }

    private bool IsNewAccount(ICommonSession session)
    {
        return _accountCreation.TryGetAccountAge(session, out var age) && age <= _newAccountThreshold;
    }

    private string GetAccountLabel(ICommonSession session)
    {
        if (!_accountCreation.TryGetAccountAge(session, out var age))
            return "unknown";

        return age <= _newAccountThreshold ? "new" : "old";
    }

    private static string GetAssignmentLabel(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin)
            return "late_join";

        if (ev.JobId == null)
            return "none";

        if (!ev.Profile.JobPriorities.TryGetValue(new ProtoId<JobPrototype>(ev.JobId), out var priority))
            return "unwanted";

        return priority == JobPriority.Never ? "unwanted" : GetPriorityLabel(priority);
    }

    private string GetRoundEndStateLabel(RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        if (!info.Connected)
            return "disconnected";

        if (!TryGetEntity(info.PlayerNetEntity, out var entityNullable) || entityNullable is not { } entity)
            return "unknown";

        if (HasComp<GhostComponent>(entity))
            return StageGhost;

        if (!TryComp<MobStateComponent>(entity, out var mobState))
            return "unknown";

        return mobState.CurrentState switch
        {
            MobState.Alive => StageAlive,
            MobState.Critical => StageCrit,
            MobState.Dead => StageDead,
            _ => "unknown",
        };
    }

    private static string GetFunnelStepLabel(NewPlayerFunnelStep step)
    {
        return step switch
        {
            NewPlayerFunnelStep.Connected => "connected",
            NewPlayerFunnelStep.Lobby => "lobby",
            NewPlayerFunnelStep.Spawned => "spawned",
            NewPlayerFunnelStep.RoundCompleted => "round_completed",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Состояние подключённого игрока, нужное для метрик удержания.
    /// </summary>
    private sealed class PlayerRetentionState
    {
        public TimeSpan ConnectedAt;
        public TimeSpan? LobbyEnteredAt;
        public bool ReachedLobby;

        public DateTime? FirstSeenTime;
        public bool FirstSeenLoaded;
        public string? CachedPlayerType;

        public RoundParticipation? Round;
        public int RoundDeaths;
        public bool HasDiedThisRound;
        public TimeSpan? LastDeathAt;
    }

    /// <summary>
    /// Участие игрока в текущем раунде с момента спавна.
    /// </summary>
    private sealed class RoundParticipation
    {
        public TimeSpan SpawnedAt;
        public string Role = "unknown";
        public string Assignment = "none";
        public string PlayerType = PlayerTypeUnknown;
        public string Gamemode = "unknown";
    }

    /// <summary>
    /// Прогресс нового игрока по шагам первого дня.
    /// </summary>
    private sealed class NewPlayerFunnelProgress
    {
        public DateTime FirstSeenTime;
        public string Account = "unknown";
        public NewPlayerFunnelStep Reached;
    }

    [Flags]
    private enum NewPlayerFunnelStep : byte
    {
        None = 0,
        Connected = 1 << 0,
        Lobby = 1 << 1,
        Spawned = 1 << 2,
        RoundCompleted = 1 << 3,
    }

    private readonly record struct PendingLeave(string Stage, string PlayerType);
}
