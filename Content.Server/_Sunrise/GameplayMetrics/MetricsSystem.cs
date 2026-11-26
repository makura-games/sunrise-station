using Content.Server._Sunrise.Auth;
using Content.Server.Cargo.Components;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Store.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.GameplayMetrics;

/// <summary>
/// Серверная система, отдающая игровые метрики в Prometheus.
/// Покрывает удержание, аудиторию, баланс, общую статистику и популярность профессий.
/// </summary>
public sealed partial class MetricsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ServerDbEntryManager _dbEntry = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AccountCreationManager _accountCreation = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    private static readonly Histogram PlayerDeathsPerRound = Metrics.CreateHistogram(
        "ss14_player_deaths_per_round",
        "Number of deaths per player per round.",
        new HistogramConfiguration
        {
            LabelNames = ["job"],
            // 0, 1, 2 … 10 — linear step 1, 11 buckets
            Buckets = Histogram.LinearBuckets(0, 1, 11)
        });

    private static readonly Histogram PlayerTimeToFirstDeathSeconds = Metrics.CreateHistogram(
        "ss14_player_time_to_first_death_seconds",
        "Time in seconds from round start until a player's first death.",
        new HistogramConfiguration
        {
            LabelNames = ["job", "gamemode"],
            // 30 s → ~1 h, factor ×1.8, 10 buckets
            Buckets = Histogram.ExponentialBuckets(30, 1.8, 10)
        });

    // BALANCE: store / uplink
    private static readonly Counter StoreCurrencySpentTotal = Metrics.CreateCounter(
        "ss14_store_currency_spent_total",
        "Total currency units spent in stores, by currency type.",
        new CounterConfiguration { LabelNames = ["currency"] });

    // BALANCE: cargo
    private static readonly Counter CargoOrdersFulfilledTotal = Metrics.CreateCounter(
        "ss14_cargo_order_fulfilled_total",
        "Cargo orders fulfilled, by product prototype ID.",
        new CounterConfiguration { LabelNames = ["product_id"] });

    // BALANCE: antag objectives
    private static readonly Counter AntagObjectivesCompletedTotal = Metrics.CreateCounter(
        "ss14_antag_objectives_completed_total",
        "Antag objectives completed at round end, by antag role.",
        new CounterConfiguration { LabelNames = ["antag_role"] });

    private static readonly Counter AntagObjectivesTotalCount = Metrics.CreateCounter(
        "ss14_antag_objectives_all_total",
        "All antag objectives assigned at round end, by antag role.",
        new CounterConfiguration { LabelNames = ["antag_role"] });

    private static readonly Counter RoundsByGamemodeTotal = Metrics.CreateCounter(
        "ss14_rounds_by_gamemode_total",
        "Total finished rounds per gamemode.",
        new CounterConfiguration { LabelNames = ["gamemode"] });

    // GENERAL: damage & healing
    private static readonly Counter DamageDealtToPlayersTotal = Metrics.CreateCounter(
        "ss14_damage_dealt_to_players_total",
        "Total raw damage dealt to player-controlled entities.");

    private static readonly Counter HealingDoneToPlayersTotal = Metrics.CreateCounter(
        "ss14_healing_done_to_players_total",
        "Total healing done to player-controlled entities.");

    private static readonly Counter PlayerDeathsTotal = Metrics.CreateCounter(
        "ss14_player_deaths_total",
        "Total player deaths during rounds.",
        new CounterConfiguration { LabelNames = ["job", "gamemode"] });

    // JOB POPULARITY
    private static readonly Counter JobAssignedTotal = Metrics.CreateCounter(
        "ss14_job_assigned_total",
        "Job assignments at round end (one per player-role pair), by job ID.",
        new CounterConfiguration { LabelNames = ["job_id"] });

    private static readonly Counter JobPriorityVotesTotal = Metrics.CreateCounter(
        "ss14_job_priority_votes_total",
        "How many players had each job at each priority when the round started.",
        new CounterConfiguration { LabelNames = ["job_id", "priority"] });

    public override void Initialize()
    {
        base.Initialize();

        InitializeRetention();
        InitializeAudience();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
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

        ShutdownRetention();
        ShutdownAudience();
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Count job priority votes from all ready players' preferences.
        foreach (var session in _player.Sessions)
        {
            if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
                continue;

            if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            foreach (var (jobId, priority) in profile.JobPriorities)
            {
                JobPriorityVotesTotal.WithLabels(jobId.Id, GetPriorityLabel(priority)).Inc();
            }
        }
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        var gamemode = GetGamemodeLabel();

        RoundsByGamemodeTotal.WithLabels(gamemode).Inc();

        RecordRoundEndRetention(ev);

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
                if (!HasComp<ObjectiveComponent>(objectiveUid))
                    continue;

                AntagObjectivesTotalCount.WithLabels(antagLabel).Inc();

                var progress = _objectives.GetProgress(objectiveUid, mindEntity);
                if (progress >= 0.999f)
                    AntagObjectivesCompletedTotal.WithLabels(antagLabel).Inc();
            }
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
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var gamemode = GetGamemodeLabel();
        var jobLabel = "unknown";

        if (_minds.TryGetMind(ent, out var mindId, out _))
            jobLabel = GetJobLabel(mindId);

        PlayerDeathsTotal.WithLabels(jobLabel, gamemode).Inc();

        if (!_retention.TryGetValue(actor.PlayerSession.UserId, out var state))
            return;

        state.RoundDeaths++;
        state.LastDeathAt = _gameTiming.CurTime;

        // Время до первой смерти в раунде
        if (!state.HasDiedThisRound)
        {
            state.HasDiedThisRound = true;
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

    private string GetGamemodeLabel()
    {
        return _gameTicker.CurrentPreset?.ID ?? "unknown";
    }

    private string GetJobLabel(EntityUid mindId)
    {
        return _jobs.MindTryGetJobId(mindId, out var jobId) && jobId.HasValue
            ? jobId.Value.Id
            : "unknown";
    }

    private static string GetPriorityLabel(JobPriority priority)
    {
        return priority switch
        {
            JobPriority.High => "high",
            JobPriority.Medium => "medium",
            JobPriority.Low => "low",
            _ => "never"
        };
    }
}
