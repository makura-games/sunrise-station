using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Store.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Content.Shared.Store.Components;
using Prometheus;
using Robust.Server.DataMetrics;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using PrometheusMetrics = Prometheus.Metrics;

namespace Content.Server._Sunrise.GameplayMetrics;

/// <summary>
/// Server-side system that reports gameplay metrics to Prometheus.
/// Covers: retention, balance, general stats, job popularity.
/// </summary>
public sealed partial class MetricsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    private static readonly Counter PlayerDisconnectsTotal = PrometheusMetrics.CreateCounter(
        "ss14_player_disconnect_total",
        "Player disconnects with context labels.",
        new CounterConfiguration
        {
            LabelNames = ["gamemode", "mob_state", "job", "is_antag"]
        });

    private static readonly Histogram PlayerSessionDurationSeconds = PrometheusMetrics.CreateHistogram(
        "ss14_player_session_duration_seconds",
        "Player in-game session duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = ["job", "is_antag"],
            // 60 s → ~4 h, factor ×1.7, 15 buckets
            Buckets = Histogram.ExponentialBuckets(60, 1.7, 15)
        });

    private static readonly Histogram PlayerDeathsPerRound = PrometheusMetrics.CreateHistogram(
        "ss14_player_deaths_per_round",
        "Number of deaths per player per round.",
        new HistogramConfiguration
        {
            LabelNames = ["job"],
            // 0, 1, 2 … 10 — linear step 1, 11 buckets
            Buckets = Histogram.LinearBuckets(0, 1, 11)
        });

    private static readonly Histogram PlayerTimeToFirstDeathSeconds = PrometheusMetrics.CreateHistogram(
        "ss14_player_time_to_first_death_seconds",
        "Time in seconds from round start until a player's first death.",
        new HistogramConfiguration
        {
            LabelNames = ["job", "gamemode"],
            // 30 s → ~1 h, factor ×1.8, 10 buckets
            Buckets = Histogram.ExponentialBuckets(30, 1.8, 10)
        });

    // RETENTION: round-end snapshot gauges
    private static readonly Gauge RoundEndSurvivalRateGauge = PrometheusMetrics.CreateGauge(
        "ss14_round_end_survival_rate",
        "Fraction of non-observer players still alive at round end (0–1).");

    private static readonly Gauge RoundEndConnectedRateGauge = PrometheusMetrics.CreateGauge(
        "ss14_round_end_connected_rate",
        "Fraction of players still connected when the round ended (0–1).");

    // BALANCE: store / uplink
    private static readonly Counter StoreCurrencySpentTotal = PrometheusMetrics.CreateCounter(
        "ss14_store_currency_spent_total",
        "Total currency units spent in stores, by currency type.",
        new CounterConfiguration { LabelNames = ["currency"] });

    // BALANCE: cargo
    private static readonly Counter CargoOrdersFulfilledTotal = PrometheusMetrics.CreateCounter(
        "ss14_cargo_order_fulfilled_total",
        "Cargo orders fulfilled, by product prototype ID.",
        new CounterConfiguration { LabelNames = ["product_id"] });

    // BALANCE: antag objectives
    private static readonly Counter AntagObjectivesCompletedTotal = PrometheusMetrics.CreateCounter(
        "ss14_antag_objectives_completed_total",
        "Antag objectives completed at round end, by antag role.",
        new CounterConfiguration { LabelNames = ["antag_role"] });

    private static readonly Counter AntagObjectivesTotalCount = PrometheusMetrics.CreateCounter(
        "ss14_antag_objectives_all_total",
        "All antag objectives assigned at round end, by antag role.",
        new CounterConfiguration { LabelNames = ["antag_role"] });

    private static readonly Counter RoundsByGamemodeTotal = PrometheusMetrics.CreateCounter(
        "ss14_rounds_by_gamemode_total",
        "Total finished rounds per gamemode.",
        new CounterConfiguration { LabelNames = ["gamemode"] });

    // GENERAL: damage & healing
    private static readonly Counter DamageDealtToPlayersTotal = PrometheusMetrics.CreateCounter(
        "ss14_damage_dealt_to_players_total",
        "Total raw damage dealt to player-controlled entities.");

    private static readonly Counter HealingDoneToPlayersTotal = PrometheusMetrics.CreateCounter(
        "ss14_healing_done_to_players_total",
        "Total healing done to player-controlled entities.");

    private static readonly Counter PlayerDeathsTotal = PrometheusMetrics.CreateCounter(
        "ss14_player_deaths_total",
        "Total player deaths during rounds.",
        new CounterConfiguration { LabelNames = ["job", "gamemode"] });

    // JOB POPULARITY
    private static readonly Counter JobAssignedTotal = PrometheusMetrics.CreateCounter(
        "ss14_job_assigned_total",
        "Job assignments at round end (one per player-role pair), by job ID.",
        new CounterConfiguration { LabelNames = ["job_id"] });

    private static readonly Counter JobPriorityVotesTotal = PrometheusMetrics.CreateCounter(
        "ss14_job_priority_votes_total",
        "How many players had each job at each priority when the round started.",
        new CounterConfiguration { LabelNames = ["job_id", "priority"] });

    // Session state tracking (per NetUserId)
    private readonly Dictionary<NetUserId, PlayerSessionData> _sessions = new();


    public override void Initialize()
    {
        base.Initialize();

        _player.PlayerStatusChanged += OnPlayerStatusChanged;

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);

        // Damage / healing — MindContainerComponent is on every player body and is free
        SubscribeLocalEvent<MindContainerComponent, DamageChangedEvent>(OnActorDamageChanged);

        // Deaths
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnActorMobStateChanged);

        // Store purchases – fires on successful currency deduction
        SubscribeLocalEvent<MindContainerComponent, SubtractCashEvent>(OnCurrencySpent);

        // Cargo
        SubscribeLocalEvent<FulfillCargoOrderEvent>(OnCargoFulfilled);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
        _sessions.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        var session = args.Session;

        switch (args.NewStatus)
        {
            case SessionStatus.InGame:
                _sessions[session.UserId] = new PlayerSessionData
                {
                    SessionStart = _gameTiming.CurTime
                };
                break;

            case SessionStatus.Disconnected:
                RecordDisconnect(session);
                _sessions.Remove(session.UserId);
                break;
        }
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        ResetRoundSessionData();

        // Count job priority votes from all ready players' preferences.
        foreach (var session in _player.Sessions)
        {
            if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
                continue;

            if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            foreach (var (jobId, priority) in profile.JobPriorities)
            {
                var priorityLabel = priority switch
                {
                    JobPriority.High => "high",
                    JobPriority.Medium => "medium",
                    JobPriority.Low => "low",
                    _ => "never"
                };
                JobPriorityVotesTotal.WithLabels(jobId.Id, priorityLabel).Inc();
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        ResetRoundSessionData();
    }

    private void ResetRoundSessionData()
    {
        _sessions.Clear();

        var now = _gameTiming.CurTime;
        foreach (var session in _player.Sessions)
        {
            _sessions[session.UserId] = new PlayerSessionData
            {
                SessionStart = now
            };
        }
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        var gamemode = _gameTicker.CurrentPreset?.ID ?? "unknown";

        RoundsByGamemodeTotal.WithLabels(gamemode).Inc();

        // Survival / connection rate
        var nonObservers = ev.AllPlayersEndInfo.Where(p => !p.Observer).ToArray();
        double survivalRate = 0;
        double connectedRate = 0;

        if (nonObservers.Length > 0)
        {
            var alivePlayers = 0;
            var connectedPlayers = 0;

            foreach (var info in nonObservers)
            {
                if (info.Connected)
                    connectedPlayers++;

                if (info.PlayerNetEntity != null
                    && TryGetEntity(info.PlayerNetEntity, out EntityUid? entityNullable)
                    && entityNullable.HasValue
                    && TryComp<MobStateComponent>(entityNullable.Value, out var mobState)
                    && mobState.CurrentState == MobState.Alive)
                {
                    alivePlayers++;
                }
            }

            survivalRate = (double)alivePlayers / nonObservers.Length;
            connectedRate = (double)connectedPlayers / nonObservers.Length;
        }

        RoundEndSurvivalRateGauge.Set(survivalRate);
        RoundEndConnectedRateGauge.Set(connectedRate);

        // Job assignment counts
        foreach (var info in ev.AllPlayersEndInfo)
        {
            foreach (var jobProto in info.JobPrototypes)
                JobAssignedTotal.WithLabels(jobProto).Inc();
        }

        var antagRolesList = new List<string>();

        // Antag objective completion
        var allMinds = EntityQueryEnumerator<MindComponent>();
        while (allMinds.MoveNext(out var mindId, out var mind))
        {
            if (!_roles.MindIsAntagonist(mindId))
                continue;

            antagRolesList.Clear();
            foreach (var roleInfo in _roles.MindGetAllRoleInfo(mindId))
            {
                if (roleInfo.Antagonist)
                    antagRolesList.Add(roleInfo.Prototype);
            }

            var antagLabel = antagRolesList.Count > 0 ? antagRolesList[0] : "unknown_antag";
            var mindEntity = new Entity<MindComponent>(mindId, mind);

            foreach (var objectiveUid in mind.Objectives)
            {
                if (!TryComp<ObjectiveComponent>(objectiveUid, out _))
                    continue;

                AntagObjectivesTotalCount.WithLabels(antagLabel).Inc();

                var progress = _objectives.GetProgress(objectiveUid, mindEntity);
                if (progress >= 0.999f)
                    AntagObjectivesCompletedTotal.WithLabels(antagLabel).Inc();
            }
        }

        // Deaths-per-round histogram (flush per-session data at round end)
        foreach (var (userId, data) in _sessions)
        {
            if (!_minds.TryGetMind(userId, out var mId, out _) || mId == null)
                continue;

            var job = GetJobLabel(mId.Value);
            var isAntag = _roles.MindIsAntagonist(mId.Value) ? "true" : "false";

            var sessionSeconds = (_gameTiming.CurTime - data.SessionStart).TotalSeconds;
            PlayerSessionDurationSeconds.WithLabels(job, isAntag).Observe(sessionSeconds);
            PlayerDeathsPerRound.WithLabels(job).Observe(data.DeathCount);
        }
    }

    private void OnActorDamageChanged(Entity<MindContainerComponent> ent, ref DamageChangedEvent args)
    {
        if (!HasComp<ActorComponent>(ent) || args.DamageDelta == null)
            return;

        var total = args.DamageDelta.GetTotal();
        if (total > 0)
            DamageDealtToPlayersTotal.Inc((double)total);
        else if (total < 0)
            HealingDoneToPlayersTotal.Inc((double)-total);
    }

    private void OnActorMobStateChanged(Entity<MindContainerComponent> ent, ref MobStateChangedEvent args)
    {
        if (!HasComp<ActorComponent>(ent))
            return;

        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var gamemode = _gameTicker.CurrentPreset?.ID ?? "unknown";
        var jobLabel = "unknown";

        if (_minds.TryGetMind(ent, out var mindId, out _))
            jobLabel = GetJobLabel(mindId);

        PlayerDeathsTotal.WithLabels(jobLabel, gamemode).Inc();

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;
        if (!_sessions.TryGetValue(userId, out var data))
            return;

        data.DeathCount++;

        // Record time-to-first-death
        if (!data.HasDied)
        {
            data.HasDied = true;
            var secondsSinceStart = (_gameTiming.CurTime - _gameTicker.RoundStartTimeSpan).TotalSeconds;
            PlayerTimeToFirstDeathSeconds.WithLabels(jobLabel, gamemode).Observe(secondsSinceStart);
        }
    }

    private void OnCurrencySpent(Entity<MindContainerComponent> _, ref SubtractCashEvent args)
    {
        StoreCurrencySpentTotal.WithLabels(args.Currency).Inc((double)args.Cost);
    }


    private void OnCargoFulfilled(ref FulfillCargoOrderEvent args)
    {
        CargoOrdersFulfilledTotal.WithLabels(args.Order.Product.Id).Inc(args.Order.OrderQuantity);
    }

    // Helpers

    private void RecordDisconnect(ICommonSession session)
    {
        var gamemode = _gameTicker.CurrentPreset?.ID ?? "unknown";
        var mobStateLabel = "unknown";
        var jobLabel = "unknown";
        var isAntagLabel = "false";

        if (_minds.TryGetMind(session.UserId, out var mindId, out var mind))
        {
            isAntagLabel = _roles.MindIsAntagonist(mindId) ? "true" : "false";
            jobLabel = GetJobLabel(mindId.Value);

            // Mob state of the currently attached entity
            if (mind.CurrentEntity.HasValue
                && TryComp<MobStateComponent>(mind.CurrentEntity.Value, out var mobState))
            {
                mobStateLabel = mobState.CurrentState switch
                {
                    MobState.Alive => "alive",
                    MobState.Critical => "critical",
                    MobState.Dead => "dead",
                    _ => "unknown"
                };
            }
        }

        PlayerDisconnectsTotal.WithLabels(gamemode, mobStateLabel, jobLabel, isAntagLabel).Inc();

        // Session duration
        if (_sessions.TryGetValue(session.UserId, out var data))
        {
            var durationSeconds = (_gameTiming.CurTime - data.SessionStart).TotalSeconds;
            PlayerSessionDurationSeconds.WithLabels(jobLabel, isAntagLabel).Observe(durationSeconds);
            PlayerDeathsPerRound.WithLabels(jobLabel).Observe(data.DeathCount);
        }
    }

    private string GetJobLabel(EntityUid mindId)
    {
        return _jobs.MindTryGetJobId(mindId, out var jobId) && jobId.HasValue
            ? jobId.Value.Id
            : "unknown";
    }

    // Nested types

    private sealed class PlayerSessionData
    {
        public TimeSpan SessionStart;
        public int DeathCount;
        public bool HasDied;
    }
}
