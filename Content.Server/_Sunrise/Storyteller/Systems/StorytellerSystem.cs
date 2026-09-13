using System.Linq;
using Robust.Shared.Enums;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Server.Administration.Managers;
using Content.Shared._Sunrise.Footprints;
using Content.Shared._Sunrise.Storyteller.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Research.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tag;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Humanoid;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Containers;
using Content.Server.Ghost.Roles;
using Content.Server.Power.Components;
using Content.Server.Jobs;
using Content.Server.NPC.HTN;
using Content.Server.Revolutionary.Components;
using Content.Server.Tesla.Components;
using Content.Shared.Materials;
using Content.Shared.Singularity.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Anomaly.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Fluids.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Server.AlertLevel;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Station.Components;
using Robust.Shared.Map;


namespace Content.Server._Sunrise.Storyteller.Systems;

/// <summary>
/// The core EntitySystem managing the Storyteller (Game Director).
/// Computes crew stress, manages pacing states, and triggers balanced events.
/// </summary>
public sealed partial class StorytellerSystem : GameRuleSystem<StorytellerRuleComponent>
{
    private const string SecurityDepartmentId = "Security";

    private const float StationStrengthMax = 100f;
    private const float StrengthCapArmedCrew = 25f;
    private const float StrengthCapSecurity = 25f;
    private const float StrengthCapTechnology = 20f;
    private const float StrengthCapEconomy = 15f;
    private const float StrengthCapMaterials = 15f;

    private const float ArmedCrewFullScale = 8f;
    private const float SecurityFullScale = 6f;
    private const float CargoFullScale = 200_000f;
    private const float MaterialsFullScale = 50_000f;
    private const float MaterialStrengthPriceDivisor = 3f;
    private const float MaterialStrengthMinFallback = 0f;
    private const float MaterialStrengthUnknownFallback = 0.5f;

    private const float StressDeadMax = 35f;
    private const float StressDeadCommandPool = StressDeadMax * 0.4f;
    private const float StressDeadCrewPool = StressDeadMax * 0.6f;
    private const float StressAtmosphereMax = 15f;
    private const float StressPowerMax = 15f;
    private const float StressMessMax = 5f;
    private const float StressEconomyMax = 5f;
    private const float StressEconomyCargoThreshold = 20_000f;
    private const float StressContainmentMax = 10f;

    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private SharedJobSystem _jobSystem = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private EventManagerSystem _eventManager = default!;
    [Dependency] private SharedCargoSystem _cargoSystem = default!;
    [Dependency] private GhostRoleSystem _ghostRoleSystem = default!;
    [Dependency] private SharedRoleSystem _roleSystem = default!;
    [Dependency] private SharedBatterySystem _batterySystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;

    private readonly List<TimeSpan> _joinTimestamps = new();
    private readonly List<TimeSpan> _leaveTimestamps = new();

    private float _maxResearchStorytellerScore;
    private int _totalTechnologyCount;

    private TimeSpan _lastStarvationWarningTime = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        InitializeMetrics();
        InitializeAI();

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _joinTimestamps.Clear();
        _leaveTimestamps.Clear();
        _lastStarvationWarningTime = TimeSpan.Zero;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }



    public bool ForceTriggerEvent(string eventId)
    {
        var query = EntityQueryEnumerator<StorytellerRuleComponent>();
        if (!query.MoveNext(out var uid, out var comp))
            return false;

        if (!_protoManager.TryIndex<EntityPrototype>(eventId, out var proto))
            return false;

        if (!_protoManager.TryIndex<StorytellerMetadataPrototype>(eventId, out var metadata))
            return false;

        TriggerEvent((uid, comp), proto, metadata);
        return true;
    }

    protected override void Added(EntityUid uid, StorytellerRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.NextCheckTime = Timing.CurTime + TimeSpan.FromSeconds(10);
        component.LastAnyEventTime = Timing.CurTime;
        component.LastHelpfulEventTime = Timing.CurTime;
        component.LastNeutralEventTime = Timing.CurTime;
        component.LastMajorEventTime = Timing.CurTime;

        component.StateTransitionTime = Timing.CurTime + TimeSpan.FromMinutes(_random.Next(15, 30));
        component.PacingState = StorytellerPacingState.Relaxation;
        component.ThreatBudget = 30f;
        component.MajorThreatBudget = 30f;

        if (component.ConfiguredStorytellerType.HasValue)
        {
            component.StorytellerType = component.ConfiguredStorytellerType.Value;
        }
        else
        {
            component.StorytellerType = _random.Pick(new[] { StorytellerType.Calm, StorytellerType.Classic, StorytellerType.Insane });
        }

        if (_protoManager.TryIndex<StorytellerTypePrototype>(component.StorytellerType.ToString(), out var typeProto))
        {
            component.GlobalEventCooldownMinutes = typeProto.GlobalEventCooldownMinutes;
            component.HelpfulEventCooldownMinutes = typeProto.HelpfulEventCooldownMinutes;
            component.NeutralEventCooldownMinutes = typeProto.NeutralEventCooldownMinutes;
            component.MajorEventCooldownMinutes = typeProto.MajorEventCooldownMinutes;
        }
    }

    protected override void Started(EntityUid uid, StorytellerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        LogStorytellerState(component, null);

        component.RuleStartTime = Timing.CurTime;
        component.LastAnyEventTime = Timing.CurTime;
        component.LastHelpfulEventTime = Timing.CurTime;
        component.LastNeutralEventTime = Timing.CurTime;
        component.LastMajorEventTime = Timing.CurTime;
        component.AlertLevelHistory.Clear();
        var query = EntityQueryEnumerator<AlertLevelComponent, MainStationComponent>();
        while (query.MoveNext(out var stationUid, out var alertComp, out _))
        {
            RecordAlertLevelChange(component, stationUid, alertComp.CurrentLevel);
        }
    }

    protected override void ActiveTick(EntityUid uid, StorytellerRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var budgetModifier = 1f;
        var maxBudgetModifier = 1f;
        if (_protoManager.TryIndex<StorytellerTypePrototype>(component.StorytellerType.ToString(), out var typeProto))
        {
            budgetModifier = typeProto.BudgetModifier;
            maxBudgetModifier = typeProto.MaxBudgetModifier;
        }

        var baseInc = component.BaseBudgetPerSecond * frameTime * budgetModifier;
        var maxBudget = component.MaxThreatBudget * maxBudgetModifier;

        // Stress dampens threat budget generation (higher stress -> crew needs relief -> slow down threat budget)
        var stressModifier = MathF.Max(0.1f, 1f - (component.CrewStress / 80f));
        component.ThreatBudget = MathF.Min(maxBudget, component.ThreatBudget + baseInc * stressModifier);
        component.MajorThreatBudget = MathF.Min(maxBudget, component.MajorThreatBudget + baseInc * stressModifier);

        // Check pacing state transitions
        if (Timing.CurTime >= component.StateTransitionTime)
        {
            TransitionPacingState(component);
        }

        // Periodic evaluation
        if (Timing.CurTime >= component.NextCheckTime)
        {
            var interval = _cfg.GetCVar(SunriseCCVars.StorytellerCheckInterval);
            component.NextCheckTime = Timing.CurTime + TimeSpan.FromSeconds(interval);
            EvaluateStoryteller((uid, component));
        }
    }

    private void TransitionPacingState(StorytellerRuleComponent comp)
    {
        var oldState = comp.PacingState;

        var durationMult = 1f;
        var relMin = 20f;
        var relMax = 40f;
        var bldMin = 15f;
        var bldMax = 30f;
        var pkMin = 12f;
        var pkMax = 24f;
        var recMin = 10f;
        var recMax = 20f;

        if (_protoManager.TryIndex<StorytellerTypePrototype>(comp.StorytellerType.ToString(), out var typeProto))
        {
            durationMult = typeProto.DurationMultiplier;
            relMin = typeProto.RelaxationMinMinutes;
            relMax = typeProto.RelaxationMaxMinutes;
            bldMin = typeProto.BuildUpMinMinutes;
            bldMax = typeProto.BuildUpMaxMinutes;
            pkMin = typeProto.PeakMinMinutes;
            pkMax = typeProto.PeakMaxMinutes;
            recMin = typeProto.RecoveryMinMinutes;
            recMax = typeProto.RecoveryMaxMinutes;
        }

        switch (comp.PacingState)
        {
            case StorytellerPacingState.Relaxation:
                comp.PacingState = StorytellerPacingState.BuildUp;
                comp.StateTransitionTime = Timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(bldMin, bldMax) * durationMult);
                break;
            case StorytellerPacingState.BuildUp:
                comp.PacingState = StorytellerPacingState.Peak;
                comp.StateTransitionTime = Timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(pkMin, pkMax) * durationMult);
                break;
            case StorytellerPacingState.Peak:
                comp.PacingState = StorytellerPacingState.Recovery;
                comp.StateTransitionTime = Timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(recMin, recMax) * durationMult);
                break;
            case StorytellerPacingState.Recovery:
                comp.PacingState = StorytellerPacingState.Relaxation;
                comp.StateTransitionTime = Timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(relMin, relMax) * durationMult);
                break;
        }

        LogStorytellerState(comp, oldState);
    }

    private void EvaluateStoryteller(Entity<StorytellerRuleComponent> entity)
    {
        var roundEndRequested = _roundEnd.IsRoundEndRequested();
        if (roundEndRequested)
        {
            return;
        }

        var metrics = CalculateStationMetrics(entity.Comp);
        entity.Comp.CrewStress = CalculateCrewStress(ref metrics);

        // Update Prometheus gauges
        UpdatePrometheusGauges(entity.Comp, metrics);

        // Loki structured logs
        LogTelemetryTick(entity.Comp, metrics);

        // AI storyteller branch
        if (_cfg.GetCVar(SunriseCCVars.StorytellerAiEnabled) && !string.IsNullOrWhiteSpace(_cfg.GetCVar(SunriseCCVars.StorytellerAiUrl)))
        {
            RequestAiEventRecommendation(entity, metrics);
            return;
        }

        // Default heuristic branch
        ExecuteHeuristicStoryteller(entity, metrics);
    }

    private void ExecuteHeuristicStoryteller(Entity<StorytellerRuleComponent> entity, StationMetrics metrics)
    {
        if (!_cfg.GetCVar(CCVars.EventsEnabled))
        {
            if (Timing.CurTime - entity.Comp.LastDisabledWarningTime > TimeSpan.FromMinutes(5))
            {
                _sawmill.Warning($"Storyteller evaluated, but {CCVars.EventsEnabled.Name} is false! Events are disabled.");
                entity.Comp.LastDisabledWarningTime = Timing.CurTime;
            }
            return;
        }

        // Sunrise-Edit: Split regular and major event flows to prevent minor event spam from locking out major threats
        ExecuteRegularEventsFlow(entity, metrics);
        ExecuteMajorEventsFlow(entity, metrics);
    }

    private void ExecuteRegularEventsFlow(Entity<StorytellerRuleComponent> entity, StationMetrics metrics)
    {
        if (Timing.CurTime - entity.Comp.LastAnyEventTime < TimeSpan.FromMinutes(entity.Comp.GlobalEventCooldownMinutes))
            return;

        // Pacing & budget check
        var rollChance = 0f;
        switch (entity.Comp.PacingState)
        {
            case StorytellerPacingState.Relaxation:
                rollChance = 0.15f; // low pop/relaxing events
                break;
            case StorytellerPacingState.BuildUp:
                rollChance = 0.33f; // building events
                break;
            case StorytellerPacingState.Peak:
                rollChance = 0.66f;
                break;
            case StorytellerPacingState.Recovery:
                // Recovery is meant for quiet breathing room unless crew is severely stressed, in which case we trigger Helpful events
                rollChance = entity.Comp.CrewStress > 40f ? 0.40f : 0.05f;
                break;
        }

        if (!_random.Prob(rollChance))
            return;

        var eligibleEvents = GetEligibleHeuristicEvents(entity.Comp, metrics, isMajor: false);
        if (eligibleEvents.Count == 0)
            return;

        // Weighted random pick
        var selected = PickEventFromEligible(eligibleEvents);
        if (selected == null)
            return;

        TriggerEvent(entity, selected.Value.Item1, selected.Value.Item2);
    }

    private void ExecuteMajorEventsFlow(Entity<StorytellerRuleComponent> entity, StationMetrics metrics)
    {
        if (Timing.CurTime - entity.Comp.LastMajorEventTime < TimeSpan.FromMinutes(entity.Comp.MajorEventCooldownMinutes))
            return;

        var rollChance = 0f;
        switch (entity.Comp.PacingState)
        {
            case StorytellerPacingState.Relaxation:
                rollChance = 0f;
                break;
            case StorytellerPacingState.BuildUp:
                rollChance = 0f;
                break;
            case StorytellerPacingState.Peak:
                rollChance = 0.33f;
                break;
            case StorytellerPacingState.Recovery:
                rollChance = 0f;
                break;
        }

        if (rollChance <= 0f || !_random.Prob(rollChance))
            return;

        var eligibleEvents = GetEligibleHeuristicEvents(entity.Comp, metrics, isMajor: true);
        if (eligibleEvents.Count == 0)
        {
            if ((entity.Comp.ThreatBudget > 80f || entity.Comp.MajorThreatBudget > 80f) && Timing.CurTime - _lastStarvationWarningTime > TimeSpan.FromMinutes(5))
            {
                _lastStarvationWarningTime = Timing.CurTime;
                LogStarvationDiagnostics(entity.Comp, metrics);
            }
            return;
        }

        // Weighted random pick
        var selected = PickEventFromEligible(eligibleEvents);
        if (selected == null)
        {
            if ((entity.Comp.ThreatBudget > 80f || entity.Comp.MajorThreatBudget > 80f) && Timing.CurTime - _lastStarvationWarningTime > TimeSpan.FromMinutes(5))
            {
                _lastStarvationWarningTime = Timing.CurTime;
                LogStarvationDiagnostics(entity.Comp, metrics, eligibleEvents);
            }
            return;
        }

        TriggerEvent(entity, selected.Value.Item1, selected.Value.Item2);
    }

    public StationMetrics CalculateStationMetrics(StorytellerRuleComponent? comp = null)
    {
        var totalPlayers = 0;
        var aliveCount = 0;
        var deadCount = 0;
        var ghostCount = 0;
        var securityCount = 0;
        var aliveCrewCount = 0;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            totalPlayers++;

            if (session.AttachedEntity is not { Valid: true } entity || Deleted(entity))
            {
                continue;
            }

            if (HasComp<GhostComponent>(entity))
            {
                if (_adminManager.IsAdmin(session))
                    continue;

                if (TryComp<GhostComponent>(entity, out var ghost) && ghost.CanReturnToBody)
                {
                    if (_mindSystem.TryGetMind(session, out _, out var mind) && mind.OwnedEntity != null)
                    {
                        if (TryComp<MobStateComponent>(mind.OwnedEntity.Value, out var bodyMob) &&
                            (bodyMob.CurrentState == MobState.Alive || bodyMob.CurrentState == MobState.Critical))
                        {
                            var onMainStation = true;
                            if (TryComp(mind.OwnedEntity.Value, out TransformComponent? xform) && xform.GridUid != null)
                            {
                                var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                                if (station != null && !HasComp<MainStationComponent>(station.Value))
                                    onMainStation = false;
                            }
                            if (onMainStation)
                                ghostCount++;
                        }
                    }
                }

                if (_mindSystem.TryGetMind(session, out _, out var mindEntity))
                {
                    EntityUid? bodyUid = null;
                    if (mindEntity.VisitingEntity != null)
                    {
                        bodyUid = mindEntity.OwnedEntity;
                    }
                    else if (mindEntity.OriginalOwnedEntity != null)
                    {
                        bodyUid = GetEntity(mindEntity.OriginalOwnedEntity.Value);
                    }

                    if (bodyUid != null && Exists(bodyUid.Value))
                    {
                        if (TryComp<MobStateComponent>(bodyUid.Value, out var bodyMob) && bodyMob.CurrentState == MobState.Dead)
                        {
                            var onMainStation = true;
                            if (TryComp(bodyUid.Value, out TransformComponent? xform) && xform.GridUid != null)
                            {
                                var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                                if (station != null && !HasComp<MainStationComponent>(station.Value))
                                    onMainStation = false;
                            }
                            if (onMainStation)
                                deadCount++;
                        }
                    }
                }

                continue;
            }

            if (TryComp<MobStateComponent>(entity, out var mobState))
            {
                if (TryComp(entity, out TransformComponent? xform) && xform.GridUid != null)
                {
                    var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                    if (station != null && !HasComp<MainStationComponent>(station.Value))
                        continue;
                }

                if (mobState.CurrentState == MobState.Dead)
                    deadCount++;
                else if (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical)
                {
                    aliveCount++;

                    if (_mindSystem.TryGetMind(entity, out var mindId, out _) && _jobSystem.MindTryGetJob(mindId, out var jobPrototype))
                    {
                        if (IsSecurityJob(jobPrototype))
                        {
                            securityCount++;
                        }
                    }

                    if (IsStationCrewMob(entity, excludeAntags: true))
                    {
                        aliveCrewCount++;
                    }
                }
            }
            else
            {
                if (TryComp(entity, out TransformComponent? xform) && xform.GridUid != null)
                {
                    var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                    if (station != null && !HasComp<MainStationComponent>(station.Value))
                        continue;
                }

                aliveCount++;
                if (IsStationCrewMob(entity, excludeAntags: true))
                {
                    aliveCrewCount++;
                }
            }
        }

        var cargoBalance = 0;
        var bankQuery = EntityQueryEnumerator<StationBankAccountComponent>();
        while (bankQuery.MoveNext(out var bankUid, out var bankComp))
        {
            if (!HasComp<MainStationComponent>(bankUid))
                continue;

            var accounts = _cargoSystem.GetAccounts((bankUid, bankComp));
            if (accounts.TryGetValue(bankComp.PrimaryAccount, out var balance))
            {
                cargoBalance += balance;
            }
            else
            {
                cargoBalance += accounts.Values.Sum();
            }
        }

        var sciencePoints = 0;
        var serverQuery = EntityQueryEnumerator<ResearchServerComponent, TransformComponent>();
        while (serverQuery.MoveNext(out _, out var serverComp, out var serverXform))
        {
            if (serverXform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(serverXform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            sciencePoints += serverComp.Points;
        }

        // Calculate expanded storyteller metrics
        CalculateGridAtmosMetrics(out var atmosUnsafeRatio, out var totalTiles);
        CalculatePowerGridMetrics(out var powerDeficit);
        var weaponsCount = CountCrewWeapons();
        CountAntagAndErt(out var antagCount, out var antagStress, out var ertCount, comp);
        GetSingularityTeslaStatus(out var singActive, out var singCont, out var tesActive, out var tesCont);
        var researchScore = CalculateResearchStorytellerScore(out var unlockedTechnologyCount);
        EnsureResearchStorytellerBoundsCache();
        CountCrewRosterDeaths(
            out var crewRosterCount,
            out var rosterCommand,
            out var rosterCrew,
            out var deadCommand,
            out var deadCrew);
        var materialStrengthScore = CalculateMaterialStrengthScore();
        var distribution = GetCrewDistribution();
        GetPlayerFlowRates(out var joinRate, out var leaveRate);
        var ghostRolesCount = _ghostRoleSystem.GetGhostRoleCount();


        var anomaliesCount = 0;
        var anomalyQuery = EntityQueryEnumerator<AnomalyComponent, TransformComponent>();
        while (anomalyQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            anomaliesCount++;
        }

        var dangerousArtifactNodes = 0;
        var artifactQuery = EntityQueryEnumerator<XenoArtifactComponent, TransformComponent>();
        while (artifactQuery.MoveNext(out _, out var artifact, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            // Artifacts in suppressing containers (stasis boxes etc.) are neutralized
            if (artifact.Suppressed)
                continue;

            // Count each unlocked node that still has research value remaining (not exhausted)
            foreach (var netNode in artifact.CachedActiveNodes)
            {
                var nodeUid = GetEntity(netNode);
                if (!TryComp<XenoArtifactNodeComponent>(nodeUid, out var nodeComp))
                    continue;

                // Skip degraded nodes (no durability left = exhausted)
                if (nodeComp.Degraded)
                    continue;

                // Skip nodes whose research points are fully extracted
                if (nodeComp.ResearchValue > 0 && nodeComp.ConsumedResearchValue >= nodeComp.ResearchValue)
                    continue;

                dangerousArtifactNodes++;
            }
        }

        var puddlesCount = 0;
        var puddleQuery = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (puddleQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            if (_tagSystem.HasTag(uid, StorytellerIgnoreMessTag))
                continue;

            if (HasComp<FootprintComponent>(uid))
                continue;

            puddlesCount++;
        }

        var footprintsCount = 0;
        var stationMaps = new HashSet<MapId>();
        var stationQuery = EntityQueryEnumerator<StationDataComponent, MainStationComponent>();
        while (stationQuery.MoveNext(out _, out var stationData, out _))
        {
            foreach (var grid in stationData.Grids)
            {
                var gridXform = Transform(grid);
                stationMaps.Add(gridXform.MapID);
            }
        }

        var footprintQuery = EntityQueryEnumerator<FootprintComponent, TransformComponent>();
        while (footprintQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid != null)
            {
                var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                if (station != null && HasComp<MainStationComponent>(station.Value))
                {
                    footprintsCount++;
                    continue;
                }
            }

            if (stationMaps.Contains(xform.MapID))
            {
                footprintsCount++;
            }
        }

        var trashCount = 0;
        var tagQuery = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (tagQuery.MoveNext(out var tagUid, out var tag, out var xform))
        {
            if (!tag.Tags.Contains(TrashTag))
                continue;

            if (xform.GridUid == null || xform.ParentUid != xform.GridUid)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            if (_tagSystem.HasTag(tagUid, StorytellerIgnoreMessTag))
                continue;

            if (_containerSystem.IsEntityInContainer(tagUid))
                continue;

            trashCount++;
        }

        var totalCrewDamage = 0f;
        var crewWithMindCount = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (session.AttachedEntity is not { Valid: true } entityUid || Deleted(entityUid))
                continue;

            if (TryComp<MobStateComponent>(entityUid, out var mobState) &&
                (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical))
            {
                if (TryComp<DamageableComponent>(entityUid, out var damageable))
                {
                    totalCrewDamage += _damageableSystem.GetTotalDamage((entityUid, damageable)).Float();
                    crewWithMindCount++;
                }
            }
        }
        var averageCrewDamage = crewWithMindCount > 0 ? totalCrewDamage / crewWithMindCount : 0f;

        var alertLevelStress = 0f;
        if (comp != null)
        {
            alertLevelStress = CalculateAlertLevelStress(comp);
        }

        _protoManager.TryIndex<StorytellerTypePrototype>(comp?.StorytellerType.ToString() ?? string.Empty, out var storytellerType);
        var armedCrewScore = CountArmedCrewNotAntags();
        var strength = CalculateNormalizedStationStrength(
            aliveCount,
            aliveCrewCount,
            armedCrewScore,
            securityCount,
            cargoBalance,
            researchScore,
            materialStrengthScore,
            storytellerType);

        return new StationMetrics
        {
            TotalPlayers = totalPlayers,
            AliveCount = aliveCount,
            DeadCount = deadCount,
            GhostCount = ghostCount,
            SecurityCount = securityCount,
            CargoBalance = cargoBalance,
            SciencePoints = sciencePoints,

            AtmosphereUnsafeRatio = atmosUnsafeRatio,
            PowerGridDeficitRatio = powerDeficit,
            CrewWeaponCount = weaponsCount,
            ActiveAntagonistCount = antagCount,
            AntagonistStressScore = antagStress,
            ActiveErtCount = ertCount,
            SingularityActive = singActive,
            SingularityContained = singCont,
            TeslaActive = tesActive,
            TeslaContained = tesCont,
            ResearchStorytellerScore = researchScore,
            UnlockedTechnologyCount = unlockedTechnologyCount,
            TotalTechnologyCount = _totalTechnologyCount,
            MaxResearchStorytellerScore = _maxResearchStorytellerScore,
            CrewDistribution = distribution,
            PlayerJoinRate = joinRate,
            PlayerLeaveRate = leaveRate,
            AvailableGhostRoles = ghostRolesCount,

            CrewRosterCount = crewRosterCount,
            RosterCommandCount = rosterCommand,
            RosterCrewCount = rosterCrew,
            DeadCommandCount = deadCommand,
            DeadCrewCount = deadCrew,

            AnomaliesCount = anomaliesCount,
            ActiveArtifactsCount = dangerousArtifactNodes,
            PuddlesCount = puddlesCount,
            FootprintsCount = footprintsCount,
            TrashCount = trashCount,
            AverageCrewDamage = averageCrewDamage,
            TotalStationTiles = totalTiles,
            MaterialStrengthScore = materialStrengthScore,

            StrengthArmedCrew = strength.Armed,
            StrengthSecurity = strength.Security,
            StrengthCargo = strength.Economy,
            StrengthTechnology = strength.Technology,
            StrengthMaterials = strength.Materials,
            StationStrength = strength.Total,
            StressAlertLevel = alertLevelStress,
        };
    }

    public float CalculateCrewStress(ref StationMetrics metrics)
    {
        if (metrics.TotalPlayers == 0 && metrics.CrewRosterCount == 0)
            return 0f;

        // Command staff deaths: 6 stress per dead head, max StressDeadCommandPool (14f)
        var deadCommandStress = MathF.Min(StressDeadCommandPool, metrics.DeadCommandCount * 6f);
        // Regular crew deaths: 2 stress per dead crew member, max StressDeadCrewPool (21f)
        var deadCrewStress = MathF.Min(StressDeadCrewPool, metrics.DeadCrewCount * 2f);
        var deadStress = deadCommandStress + deadCrewStress;

        var ghostStress = Math.Clamp(metrics.GhostCount * 1.5f, 0f, 10f);

        var containmentStress = 0f;
        if (metrics.SingularityActive || metrics.TeslaActive)
            containmentStress += 5f;

        if ((metrics.SingularityActive && !metrics.SingularityContained)
            || (metrics.TeslaActive && !metrics.TeslaContained))
        {
            containmentStress += 5f;
        }

        containmentStress = Math.Clamp(containmentStress, 0f, StressContainmentMax);

        var economyStress = 0f;
        if (metrics.CargoBalance < StressEconomyCargoThreshold)
        {
            var deficitRatio = 1f - metrics.CargoBalance / StressEconomyCargoThreshold;
            economyStress = Math.Clamp(deficitRatio * StressEconomyMax, 0f, StressEconomyMax);
        }

        var damageStress = Math.Clamp(metrics.AverageCrewDamage * 0.4f, 0f, 15f);

        var anomalyStress = Math.Clamp(
            metrics.AnomaliesCount * 2f + metrics.ActiveArtifactsCount * 0.3f,
            0f,
            6f);

        var totalStationTiles = MathF.Max(100f, metrics.TotalStationTiles);
        var adjustedPuddles = MathF.Max(0f, metrics.PuddlesCount - 10f);
        var adjustedTrash = MathF.Max(0f, metrics.TrashCount - 100f);
        var adjustedFootprints = MathF.Max(0f, metrics.FootprintsCount - 200f);
        var puddleDensity = adjustedPuddles / totalStationTiles;
        var trashDensity = adjustedTrash / totalStationTiles;
        var footprintDensity = adjustedFootprints / totalStationTiles;
        var messStress = Math.Clamp(
            (puddleDensity / 0.3f * 2f) + (trashDensity / 3.0f * 2f) + (footprintDensity / 6.0f * 1f),
            0f,
            StressMessMax);

        var adjustedPowerDeficit = MathF.Max(0f, metrics.PowerGridDeficitRatio - 0.10f) / 0.90f;
        var powerStress = Math.Clamp(adjustedPowerDeficit * StressPowerMax, 0f, StressPowerMax);

        var atmosphereStress = Math.Clamp(
            metrics.AtmosphereUnsafeRatio * StressAtmosphereMax,
            0f,
            StressAtmosphereMax);

        var antagonistStress = Math.Clamp(metrics.AntagonistStressScore, 0f, 20f);
        var alertLevelStress = metrics.StressAlertLevel;

        metrics.StressDead = deadStress;
        metrics.StressGhost = ghostStress;
        metrics.StressContainment = containmentStress;
        metrics.StressEconomy = economyStress;
        metrics.StressDamage = damageStress;
        metrics.StressAnomaly = anomalyStress;
        metrics.StressMess = messStress;
        metrics.StressPower = powerStress;
        metrics.StressAtmosphere = atmosphereStress;
        metrics.StressAntagonist = antagonistStress;

        var totalStress = deadStress + ghostStress + containmentStress + economyStress + damageStress
                          + anomalyStress + messStress + powerStress + atmosphereStress + antagonistStress
                          + alertLevelStress;
        return Math.Clamp(totalStress, 0f, 100f);
    }

    private readonly record struct NormalizedStationStrength(
        float Armed,
        float Security,
        float Economy,
        float Technology,
        float Materials,
        float Total);

    private NormalizedStationStrength CalculateNormalizedStationStrength(
        int aliveCount,
        int aliveCrewCount,
        float armedCrewScore,
        int securityCount,
        int cargoBalance,
        float researchScore,
        float materialScore,
        StorytellerTypePrototype? typeProto)
    {
        var armedScale = typeProto?.StrengthArmedCrewCoefficient ?? ArmedCrewFullScale;
        var securityScale = typeProto?.StrengthSecurityCoefficient ?? SecurityFullScale;
        var cargoScale = typeProto?.StrengthCargoFullScale ?? CargoFullScale;
        var materialsScale = typeProto?.StrengthMaterialsFullScale ?? MaterialsFullScale;

        EnsureResearchStorytellerBoundsCache();
        var techMax = typeProto?.StrengthTechnologyFullScale > 0f
            ? typeProto.StrengthTechnologyFullScale
            : _maxResearchStorytellerScore;

        var dynamicArmedScale = MathF.Max(armedScale, aliveCrewCount);

        var dynamicSecurityScale = MathF.Max(securityScale, aliveCount * 0.20f);

        var armed = StrengthCapArmedCrew * Math.Clamp(armedCrewScore / MathF.Max(1f, dynamicArmedScale), 0f, 1f);
        var security = StrengthCapSecurity * Math.Clamp(securityCount / MathF.Max(1f, dynamicSecurityScale), 0f, 1f);
        var economy = StrengthCapEconomy * Math.Clamp(cargoBalance / MathF.Max(1f, cargoScale), 0f, 1f);
        var technology = StrengthCapTechnology * Math.Clamp(researchScore / MathF.Max(1f, techMax), 0f, 1f);
        var materials = StrengthCapMaterials * Math.Clamp(materialScore / MathF.Max(1f, materialsScale), 0f, 1f);

        var total = Math.Clamp(armed + security + economy + technology + materials, 0f, StationStrengthMax);
        return new NormalizedStationStrength(armed, security, economy, technology, materials, total);
    }

    private float CalculateMaterialStrengthScore()
    {
        var aggregated = new Dictionary<ProtoId<MaterialPrototype>, int>();
        var storageQuery = EntityQueryEnumerator<MaterialStorageComponent, TransformComponent>();
        while (storageQuery.MoveNext(out var uid, out var storage, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            foreach (var (material, amount) in _materialStorage.GetStoredMaterials((uid, storage), localOnly: false))
            {
                aggregated[material] = aggregated.GetValueOrDefault(material) + amount;
            }
        }

        var score = 0f;
        foreach (var (material, amount) in aggregated)
        {
            score += amount * GetMaterialStrengthWeight(material);
        }

        return score;
    }

    private float GetMaterialStrengthWeight(ProtoId<MaterialPrototype> materialId)
    {
        if (_protoManager.TryIndex<StorytellerMaterialWeightPrototype>(materialId, out var weightOverride))
            return weightOverride.Weight;

        if (!_protoManager.TryIndex(materialId, out MaterialPrototype? proto))
            return MaterialStrengthUnknownFallback;

        if (proto.StorytellerStrengthWeight > 0f)
            return proto.StorytellerStrengthWeight;

        return MathF.Max(MaterialStrengthMinFallback, (float) (proto.Price / MaterialStrengthPriceDivisor));
    }

    private void CountCrewRosterDeaths(
        out int rosterCount,
        out int rosterCommand,
        out int rosterCrew,
        out int deadCommand,
        out int deadCrew)
    {
        rosterCount = 0;
        rosterCommand = 0;
        rosterCrew = 0;
        deadCommand = 0;
        deadCrew = 0;

        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindId, out var mind))
        {
            if (!_jobSystem.MindTryGetJob(mindId, out var job))
                continue;

            if (mind.OwnedEntity is { } ownedBody && Exists(ownedBody))
            {
                if (TryComp(ownedBody, out TransformComponent? xform) && xform.GridUid != null)
                {
                    var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
                    if (station != null && !HasComp<MainStationComponent>(station.Value))
                        continue;
                }
            }

            if (job.JobEntity != null)
                continue;

            var isCommand = JobGrantsCommandStaff(job);
            rosterCount++;
            if (isCommand)
                rosterCommand++;
            else
                rosterCrew++;

            if (mind.OwnedEntity is not { } body || !Exists(body))
                continue;

            if (!TryComp<MobStateComponent>(body, out var mobState) || mobState.CurrentState != MobState.Dead)
                continue;

            if (isCommand)
                deadCommand++;
            else
                deadCrew++;
        }
    }

    private bool JobGrantsCommandStaff(JobPrototype job)
    {
        foreach (var special in job.Special)
        {
            if (special is not AddComponentSpecial componentSpecial)
                continue;

            foreach (var component in componentSpecial.Components)
            {
                if (_componentFactory.GetComponent(component.Value) is CommandStaffComponent)
                    return true;
            }
        }

        return false;
    }

    private bool IsSecurityJob(JobPrototype job)
    {
        if (!_jobSystem.TryGetAllDepartments(job.ID, out var departments))
            return false;

        foreach (var dept in departments)
        {
            if (dept.ID == SecurityDepartmentId)
                return true;
        }

        return false;
    }

    private Dictionary<EntityPrototype, StorytellerMetadataPrototype> GetEligibleHeuristicEvents(StorytellerRuleComponent comp, StationMetrics metrics, bool isMajor)
    {
        var result = new Dictionary<EntityPrototype, StorytellerMetadataPrototype>();
        var currentDuration = GameTicker.RoundDuration();

        // Query all rule prototypes with storyteller metadata
        foreach (var proto in _protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!_protoManager.TryIndex<StorytellerMetadataPrototype>(proto.ID, out var metadata))
                continue;

            var isEventMajorAntag = metadata.ThreatType == StorytellerThreatType.MajorAntag;
            if (isMajor != isEventMajorAntag)
                continue;

            if (metadata.ThreatType == StorytellerThreatType.Helpful)
            {
                if (Timing.CurTime - comp.LastHelpfulEventTime < TimeSpan.FromMinutes(comp.HelpfulEventCooldownMinutes))
                    continue;
            }
            else if (metadata.ThreatType == StorytellerThreatType.Neutral)
            {
                if (Timing.CurTime - comp.LastNeutralEventTime < TimeSpan.FromMinutes(comp.NeutralEventCooldownMinutes))
                    continue;
            }

            if (metadata.ThreatType is StorytellerThreatType.MajorAntag or StorytellerThreatType.MajorCalm)
            {
                if (metrics.StationStrength < metadata.MinStationStrength)
                    continue;
            }
            else if (comp.CrewStress > metadata.MaxStress)
            {
                continue;
            }

            // Evac/roundend checks
            if (proto.TryGetComponent<StationEventComponent>(out var stationEvent, EntityManager.ComponentFactory))
            {
                if (metrics.TotalPlayers < stationEvent.MinimumPlayers)
                    continue;

                if (currentDuration.TotalMinutes < stationEvent.EarliestStart)
                    continue;

                // Reoccurrence check
                var lastTime = _eventManager.TimeSinceLastEvent(proto);
                if (lastTime != TimeSpan.Zero && currentDuration.TotalMinutes < stationEvent.ReoccurrenceDelay + lastTime.TotalMinutes)
                    continue;

                if (_roundEnd.IsRoundEndRequested() && !stationEvent.OccursDuringRoundEnd)
                    continue;
            }

            // Gamemode/rule-specific filters based on pacing state
            if (comp.PacingState == StorytellerPacingState.Recovery)
            {
                // In Recovery, we ONLY spawn helpful or neutral events
                if (metadata.ThreatType != StorytellerThreatType.Helpful && metadata.ThreatType != StorytellerThreatType.Neutral)
                    continue;
            }
            else if (comp.PacingState == StorytellerPacingState.Relaxation)
            {
                // In Relaxation, we allow helpful, neutral, and minor calm/antag threats (block MajorCalm and MajorAntag)
                if (metadata.ThreatType == StorytellerThreatType.MajorCalm || metadata.ThreatType == StorytellerThreatType.MajorAntag)
                    continue;
            }
            else if (comp.PacingState == StorytellerPacingState.BuildUp)
            {
                // In BuildUp, we allow helpful, neutral, minor threats, and MajorCalm (block MajorAntag to save budget for Peak)
                if (metadata.ThreatType == StorytellerThreatType.MajorAntag)
                    continue;
            }
            // In Peak, all threats (including MajorAntag) are allowed and will spawn smoothly over time

            // Budget check
            if (metadata.ThreatType != StorytellerThreatType.Helpful && metadata.ThreatType != StorytellerThreatType.Neutral)
            {
                if (metadata.ThreatType == StorytellerThreatType.MajorAntag)
                {
                    if (metadata.ThreatCost > comp.MajorThreatBudget)
                        continue;
                }
                else
                {
                    if (metadata.ThreatCost > comp.ThreatBudget)
                        continue;
                }
            }

            // Prevent already running rules
            if (GameTicker.IsGameRuleActive(proto.ID))
                continue;

            result.Add(proto, metadata);
        }

        return result;
    }

    private (EntityPrototype, StorytellerMetadataPrototype)? PickEventFromEligible(
        Dictionary<EntityPrototype, StorytellerMetadataPrototype> available)
    {
        var totalWeight = 0f;
        foreach (var (proto, metadata) in available)
        {
            var baseWeight = 10f;
            if (proto.TryGetComponent<StationEventComponent>(out var stationEvent, EntityManager.ComponentFactory))
            {
                baseWeight = stationEvent.Weight;
            }

            var weight = baseWeight * metadata.WeightModifier;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return null;

        var roll = _random.NextFloat(totalWeight);
        foreach (var (proto, metadata) in available)
        {
            var baseWeight = 10f;
            if (proto.TryGetComponent<StationEventComponent>(out var stationEvent, EntityManager.ComponentFactory))
            {
                baseWeight = stationEvent.Weight;
            }

            var weight = baseWeight * metadata.WeightModifier;
            roll -= weight;
            if (roll <= 0f)
            {
                return (proto, metadata);
            }
        }

        var first = available.First();
        return (first.Key, first.Value);
    }

    private void TriggerEvent(Entity<StorytellerRuleComponent> entity, EntityPrototype proto, StorytellerMetadataPrototype metadata)
    {
        // Deduct cost
        if (metadata.ThreatType != StorytellerThreatType.Helpful && metadata.ThreatType != StorytellerThreatType.Neutral)
        {
            if (metadata.ThreatType == StorytellerThreatType.MajorAntag)
            {
                entity.Comp.MajorThreatBudget = MathF.Max(0f, entity.Comp.MajorThreatBudget - metadata.ThreatCost);
            }
            else
            {
                entity.Comp.ThreatBudget = MathF.Max(0f, entity.Comp.ThreatBudget - metadata.ThreatCost);
            }
        }

        // Spawn and start rule
        var ruleUid = GameTicker.AddGameRule(proto.ID);
        GameTicker.StartGameRule(ruleUid);

        // Record history
        entity.Comp.ActiveStorytellerRules.Add(ruleUid);
        entity.Comp.EventHistory.Add(proto.ID);

        // Update cooldown tracking
        var isEventMajorAntag = metadata.ThreatType == StorytellerThreatType.MajorAntag;
        if (isEventMajorAntag)
        {
            entity.Comp.LastMajorEventTime = Timing.CurTime;
        }
        else
        {
            entity.Comp.LastAnyEventTime = Timing.CurTime;
        }

        if (metadata.ThreatType == StorytellerThreatType.Helpful)
        {
            entity.Comp.LastHelpfulEventTime = Timing.CurTime;
        }
        else if (metadata.ThreatType == StorytellerThreatType.Neutral)
        {
            entity.Comp.LastNeutralEventTime = Timing.CurTime;
        }

        // Metrics & Logging
        RecordEventTriggered(proto.ID, metadata);
        var metrics = CalculateStationMetrics(entity.Comp);
        CalculateCrewStress(ref metrics);
        LogTelemetryTick(entity.Comp, metrics, false);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        _joinTimestamps.Add(Timing.CurTime);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            _leaveTimestamps.Add(Timing.CurTime);
        }
    }

    private void GetPlayerFlowRates(out float joinRate, out float leaveRate)
    {
        var cutoff = Timing.CurTime - TimeSpan.FromMinutes(10);
        _joinTimestamps.RemoveAll(t => t < cutoff);
        _leaveTimestamps.RemoveAll(t => t < cutoff);
        joinRate = _joinTimestamps.Count;
        leaveRate = _leaveTimestamps.Count;
    }

    private void CalculateGridAtmosMetrics(out float unsafeRatio, out int totalTiles)
    {
        unsafeRatio = 0f;
        totalTiles = 0;

        var totalSensors = 0;
        var unsafeSensors = 0;

        // This is highly accurate and ignores tiles under walls or unsimulated areas.
        var monitorQuery = EntityQueryEnumerator<Content.Server.Atmos.Monitor.Components.AtmosMonitorComponent, TransformComponent>();
        var atmosSystem = EntityManager.System<AtmosphereSystem>(); // Fetch atmosphere system to read real tile gas if sensor is unpowered
        while (monitorQuery.MoveNext(out var uid, out var monitor, out var xform))
        {
            if (monitor.MonitorsPipeNet)
                continue;

            if (xform.GridUid == null || _stationSystem.GetOwningStation(xform.GridUid.Value) == null)
                continue;

            if (monitor.IgnoreStress)
                continue;

            totalSensors++;

            // If the sensor is unpowered, monitor.TileGas will be null.
            // We fetch the real physical atmosphere of the tile directly to prevent false breach stress during blackouts.
            var air = monitor.TileGas ?? atmosSystem.GetContainingMixture(uid, true);

            if (air == null)
            {
                unsafeSensors++;
            }
            else
            {
                // Dynamic threshold evaluation based on the sensor's own YAML/prototype configuration.
                // This completely eliminates hardcoded gas, temperature, and pressure limits.
                var state = AtmosAlarmType.Normal;

                if (monitor.PressureThreshold != null && monitor.PressureThreshold.CheckThreshold(air.Pressure, out var pressureState))
                {
                    if (pressureState > state)
                        state = pressureState;
                }

                if (monitor.TemperatureThreshold != null && monitor.TemperatureThreshold.CheckThreshold(air.Temperature, out var tempState))
                {
                    if (tempState > state)
                        state = tempState;
                }

                if (monitor.GasThresholds != null && air.TotalMoles > 1e-8f)
                {
                    foreach (var (gas, threshold) in monitor.GasThresholds)
                    {
                        var gasRatio = air.GetMoles(gas) / air.TotalMoles;
                        if (threshold.CheckThreshold(gasRatio, out var gasState))
                        {
                            if (gasState > state)
                                state = gasState;
                        }
                    }
                }

                // If the sensor is powered, we also take its actual live alarm state into account
                if (monitor.LastAlarmState > state)
                {
                    state = monitor.LastAlarmState;
                }

                if (state != AtmosAlarmType.Normal)
                    unsafeSensors++;
            }
        }

        if (totalSensors > 0)
        {
            unsafeRatio = (float) unsafeSensors / totalSensors;
            totalTiles = totalSensors;
        }
    }

    private void CalculatePowerGridMetrics(out float powerDeficitRatio)
    {
        var apcQuery = EntityQueryEnumerator<ApcComponent, BatteryComponent, TransformComponent>();
        var totalApcs = 0;
        var deadApcs = 0;
        while (apcQuery.MoveNext(out var uid, out _, out var battery, out var xform))
        {

            if (xform.GridUid == null || _stationSystem.GetOwningStation(xform.GridUid.Value) == null)
                continue;

            totalApcs++;

            if (_batterySystem.GetCharge((uid, battery)) <= battery.MaxCharge * 0.1f)
            {
                deadApcs++;
            }
        }
        powerDeficitRatio = totalApcs > 0 ? (float)deadApcs / totalApcs : 0f;
    }

    /// <summary>
    /// Round-start station crew used for armed strength / weapon metrics.
    /// Excludes NPCs, visitors, ghost-role entities, security (tracked separately), and optionally antags.
    /// </summary>
    private bool IsStationCrewMob(EntityUid mob, bool excludeAntags)
    {
        if (!HasComp<HumanoidProfileComponent>(mob) && !HasComp<BorgChassisComponent>(mob))
            return false;

        if (TryComp(mob, out TransformComponent? xform) && xform.GridUid != null)
        {
            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station != null && !HasComp<MainStationComponent>(station.Value))
                return false;
        }

        if (HasComp<HTNComponent>(mob))
            return false;

        if (!_mindSystem.TryGetMind(mob, out var mindId, out _))
            return false;

        if (!_jobSystem.MindTryGetJob(mindId, out var job))
            return false;

        if (job.JobEntity != null)
            return false;

        if (IsSecurityJob(job))
            return false;

        if (excludeAntags && _roleSystem.MindIsAntagonist(mindId))
            return false;

        return true;
    }

    private int CountCrewWeapons()
    {
        var armedMobs = new HashSet<EntityUid>();
        var gunQuery = EntityQueryEnumerator<GunComponent>();
        while (gunQuery.MoveNext(out var uid, out _))
        {
            if (IsToyWeapon(uid))
                continue;

            var mob = FindCarryingMob(uid);
            if (mob != null && _mobStateQuery.TryGetComponent(mob.Value, out var mobState))
            {
                if (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical)
                {
                    if (IsStationCrewMob(mob.Value, excludeAntags: true))
                    {
                        armedMobs.Add(mob.Value);
                    }
                }
            }
        }

        var meleeQuery = EntityQueryEnumerator<MeleeWeaponComponent>();
        while (meleeQuery.MoveNext(out var uid, out var melee))
        {
            if (IsFirearm(uid))
            {
                var mob = FindCarryingMob(uid);
                if (mob != null && _mobStateQuery.TryGetComponent(mob.Value, out var mobState))
                {
                    if (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical)
                    {
                        if (IsStationCrewMob(mob.Value, excludeAntags: true))
                        {
                            armedMobs.Add(mob.Value);
                        }
                    }
                }
                continue;
            }

            if (melee.Damage.GetTotal().Float() <= 15) // TODO: Магическое число должно быть обьявлено переменной
                continue;

            var mob2 = FindCarryingMob(uid);
            if (mob2 != null && _mobStateQuery.TryGetComponent(mob2.Value, out var mobState2))
            {
                if (mobState2.CurrentState == MobState.Alive || mobState2.CurrentState == MobState.Critical)
                {
                    if (IsStationCrewMob(mob2.Value, excludeAntags: true))
                    {
                        armedMobs.Add(mob2.Value);
                    }
                }
            }
        }
        return armedMobs.Count;
    }

    private float CountArmedCrewNotAntags()
    {
        var mobWeights = new Dictionary<EntityUid, float>();
        var gunQuery = EntityQueryEnumerator<GunComponent>();
        while (gunQuery.MoveNext(out var uid, out _))
        {
            if (IsToyWeapon(uid))
                continue;

            var mob = FindCarryingMob(uid);
            if (mob != null && _mobStateQuery.TryGetComponent(mob.Value, out var mobState))
            {
                if (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical)
                {
                    if (IsStationCrewMob(mob.Value, excludeAntags: true))
                    {
                        var currentMax = mobWeights.GetValueOrDefault(mob.Value, 0f);
                        mobWeights[mob.Value] = MathF.Max(currentMax, 1.0f);
                    }
                }
            }
        }

        var meleeQuery = EntityQueryEnumerator<MeleeWeaponComponent>();
        while (meleeQuery.MoveNext(out var uid, out var melee))
        {
            if (IsFirearm(uid))
                continue;

            var mob = FindCarryingMob(uid);
            if (mob != null && _mobStateQuery.TryGetComponent(mob.Value, out var mobState))
            {
                if (mobState.CurrentState == MobState.Alive || mobState.CurrentState == MobState.Critical)
                {
                    if (IsStationCrewMob(mob.Value, excludeAntags: true))
                    {
                        var weight = 0f;
                        var damage = melee.Damage.GetTotal().Float();

                        if (HasComp<Content.Shared.Weapons.Melee.EnergySword.EnergySwordComponent>(uid) ||
                            HasComp<Content.Shared.Stunnable.StunbatonComponent>(uid) ||
                            (damage >= 20f && !HasComp<Content.Shared.Tools.Components.ToolComponent>(uid)))
                        {
                            weight = 0.5f;
                        }
                        else if (damage >= 12f && !HasComp<Content.Shared.Tools.Components.ToolComponent>(uid))
                        {
                            weight = 0.2f;
                        }

                        if (weight > 0f)
                        {
                            var currentMax = mobWeights.GetValueOrDefault(mob.Value, 0f);
                            mobWeights[mob.Value] = MathF.Max(currentMax, weight);
                        }
                    }
                }
            }
        }

        var totalScore = 0f;
        foreach (var weight in mobWeights.Values)
        {
            totalScore += weight;
        }

        return totalScore;
    }

    private bool IsToyWeapon(EntityUid uid)
    {
        return HasComp<PacifismAllowedGunComponent>(uid);
    }

    private bool IsFirearm(EntityUid uid)
    {
        if (IsToyWeapon(uid))
            return false;

        if (HasComp<GunComponent>(uid))
            return true;

        return false;
    }

    private EntityUid? FindCarryingMob(EntityUid uid)
    {
        var current = uid;
        while (true)
        {
            if (!_xformQuery.TryGetComponent(current, out var xform))
                break;

            if (!xform.ParentUid.Valid)
                break;

            if (_mobStateQuery.HasComponent(xform.ParentUid))
                return xform.ParentUid;

            current = xform.ParentUid;
        }
        return null;
    }

    private void CountAntagAndErt(out int antagCount, out float antagStress, out int ertCount, StorytellerRuleComponent? comp = null)
    {
        antagCount = 0;
        antagStress = 0f;
        ertCount = 0;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (session.AttachedEntity is not { Valid: true } entity || Deleted(entity))
                continue;

            if (TryComp<MobStateComponent>(entity, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            if (_mindSystem.TryGetMind(entity, out var mindId, out var mindComp))
            {
                var isAntag = false;
                var maxAntagStress = 0f;

                foreach (var role in mindComp.MindRoleContainer.ContainedEntities)
                {
                    if (TryComp<MindRoleComponent>(role, out var roleComp) && roleComp.Antag)
                    {
                        isAntag = true;
                        var stressVal = 4f; // Default fallback
                        if (roleComp.AntagPrototype != null && _protoManager.TryIndex(roleComp.AntagPrototype, out var antagProto))
                        {
                            stressVal = antagProto.StorytellerStress;
                        }
                        maxAntagStress = Math.Max(maxAntagStress, stressVal);
                    }
                }

                if (isAntag)
                {
                    antagCount++;
                    antagStress += maxAntagStress;
                }

                if (_jobSystem.MindTryGetJob(mindId, out var job))
                {
                    if (_jobSystem.TryGetAllDepartments(job.ID, out var departments))
                    {
                        foreach (var dept in departments)
                        {
                            var targetDept = comp?.ErtDepartment.Id ?? "SpecialOperations";
                            if (dept.ID == targetDept)
                            {
                                ertCount++;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private void GetSingularityTeslaStatus(out bool singuloActive, out bool singuloContained, out bool teslaActive, out bool teslaContained)
    {
        singuloActive = false;
        singuloContained = true;
        teslaActive = false;
        teslaContained = true;

        var fieldsExist = false;
        var fieldQuery = EntityQueryEnumerator<ContainmentFieldComponent>();
        if (fieldQuery.MoveNext(out _, out _))
        {
            fieldsExist = true;
        }

        var singuloQuery = EntityQueryEnumerator<SingularityComponent, TransformComponent>();
        while (singuloQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            singuloActive = true;
            if (!fieldsExist || !IsNearActiveContainmentField(xform))
                singuloContained = false;
        }

        var teslaQuery = EntityQueryEnumerator<TeslaEnergyBallComponent, TransformComponent>();
        while (teslaQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            teslaActive = true;
            if (!fieldsExist || !IsNearActiveContainmentField(xform))
                teslaContained = false;
        }
    }

    private bool IsNearActiveContainmentField(TransformComponent entityXform)
    {
        var fieldQuery = EntityQueryEnumerator<ContainmentFieldComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out _, out _, out var fieldXform))
        {
            if (entityXform.MapID == fieldXform.MapID)
            {
                var distance = (_transformSystem.GetWorldPosition(entityXform) - _transformSystem.GetWorldPosition(fieldXform)).Length();
                if (distance <= 10.0f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void EnsureResearchStorytellerBoundsCache()
    {
        if (_maxResearchStorytellerScore > 0f)
            return;

        _maxResearchStorytellerScore = 1f;
        _totalTechnologyCount = 0;

        foreach (var tech in _protoManager.EnumeratePrototypes<Content.Shared.Research.Prototypes.TechnologyPrototype>())
        {
            if (tech.Hidden)
                continue;

            _totalTechnologyCount++;
            _maxResearchStorytellerScore += GetTechnologyStorytellerWeight(tech);
        }
    }

    private float GetTechnologyStorytellerWeight(Content.Shared.Research.Prototypes.TechnologyPrototype tech)
    {
        var tierWeight = tech.Tier switch
        {
            1 => 1f,
            2 => 3f,
            3 => 8f,
            _ => 1f,
        };

        var disciplineMult = 1f;
        if (_protoManager.TryIndex(tech.Discipline, out Content.Shared.Research.Prototypes.TechDisciplinePrototype? discipline))
            disciplineMult = discipline.StorytellerUsefulness;

        return tierWeight * disciplineMult;
    }

    private float CalculateResearchStorytellerScore(out int unlockedTechnologyCount)
    {
        var uniqueTechs = new HashSet<string>();
        var techQuery = EntityQueryEnumerator<TechnologyDatabaseComponent, TransformComponent>();
        while (techQuery.MoveNext(out _, out var techDb, out var techXform))
        {
            if (techXform.GridUid == null)
                continue;

            var station = _stationSystem.GetOwningStation(techXform.GridUid.Value);
            if (station == null || !HasComp<MainStationComponent>(station.Value))
                continue;

            foreach (var techId in techDb.UnlockedTechnologies)
            {
                uniqueTechs.Add(techId.Id);
            }
        }

        unlockedTechnologyCount = uniqueTechs.Count;

        var totalScore = 0f;
        foreach (var techId in uniqueTechs)
        {
            if (!_protoManager.TryIndex<Content.Shared.Research.Prototypes.TechnologyPrototype>(techId, out var techProto))
                continue;

            totalScore += GetTechnologyStorytellerWeight(techProto);
        }

        return totalScore;
    }

    private Dictionary<string, int> GetCrewDistribution()
    {
        var dist = new Dictionary<string, int>();
        foreach (var dept in _protoManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            dist[dept.ID] = 0;
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (session.AttachedEntity is not { Valid: true } entity || Deleted(entity))
                continue;

            if (TryComp<MobStateComponent>(entity, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            if (_mindSystem.TryGetMind(entity, out var mindId, out _) && _jobSystem.MindTryGetJob(mindId, out var job))
            {
                if (_jobSystem.TryGetAllDepartments(job.ID, out var departments))
                {
                    foreach (var dept in departments)
                    {
                        if (dist.ContainsKey(dept.ID))
                        {
                            dist[dept.ID]++;
                        }
                    }
                }
            }
        }
        return dist;
    }

    private void LogStarvationDiagnostics(StorytellerRuleComponent comp, StationMetrics metrics, Dictionary<EntityPrototype, StorytellerMetadataPrototype>? eligibleEvents = null)
    {
        var currentDuration = GameTicker.RoundDuration();
        _sawmill.Warning($"Storyteller Starvation Diagnostics: Budget={comp.ThreatBudget}, MajorBudget={comp.MajorThreatBudget}, Pacing={comp.PacingState}, Players={metrics.TotalPlayers}, RoundDuration={currentDuration.TotalMinutes:F1}m");

        if (eligibleEvents != null && eligibleEvents.Count > 0)
        {
            _sawmill.Warning($"Found {eligibleEvents.Count} eligible events, calculating weights:");

            foreach (var (proto, metadata) in eligibleEvents)
            {
                var baseWeight = 10f;
                if (proto.TryGetComponent<StationEventComponent>(out var stationEvent, EntityManager.ComponentFactory))
                {
                    baseWeight = stationEvent.Weight;
                }

                var weight = baseWeight * metadata.WeightModifier;

                _sawmill.Warning($"  Event: {proto.ID}, Type: {metadata.ThreatType}, BaseWeight: {baseWeight}, FinalWeight: {weight}, Cost: {metadata.ThreatCost}");
            }
        }

        var reasons = new Dictionary<string, int>();

        foreach (var proto in _protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!_protoManager.TryIndex<StorytellerMetadataPrototype>(proto.ID, out var metadata))
                continue;

            if (metadata.ThreatType == StorytellerThreatType.Helpful)
            {
                if (Timing.CurTime - comp.LastHelpfulEventTime < TimeSpan.FromMinutes(comp.HelpfulEventCooldownMinutes))
                {
                    IncrementReason($"Helpful event cooldown ({comp.HelpfulEventCooldownMinutes:F1}m)");
                    continue;
                }
            }
            else if (metadata.ThreatType == StorytellerThreatType.Neutral)
            {
                if (Timing.CurTime - comp.LastNeutralEventTime < TimeSpan.FromMinutes(comp.NeutralEventCooldownMinutes))
                {
                    IncrementReason($"Neutral event cooldown ({comp.NeutralEventCooldownMinutes:F1}m)");
                    continue;
                }
            }

            if (metadata.ThreatType is StorytellerThreatType.MajorAntag or StorytellerThreatType.MajorCalm)
            {
                if (metrics.StationStrength < metadata.MinStationStrength)
                {
                    IncrementReason("StationStrength < MinStationStrength");
                    continue;
                }
            }
            else if (comp.CrewStress > metadata.MaxStress)
            {
                IncrementReason("CrewStress > MaxStress");
                continue;
            }

            if (proto.TryGetComponent<StationEventComponent>(out var stationEvent, EntityManager.ComponentFactory))
            {
                if (metrics.TotalPlayers < stationEvent.MinimumPlayers)
                {
                    IncrementReason($"TotalPlayers < MinimumPlayers ({stationEvent.MinimumPlayers})");
                    continue;
                }

                if (currentDuration.TotalMinutes < stationEvent.EarliestStart)
                {
                    IncrementReason($"RoundDuration < EarliestStart ({stationEvent.EarliestStart}m)");
                    continue;
                }

                var lastTime = _eventManager.TimeSinceLastEvent(proto);
                if (lastTime != TimeSpan.Zero && currentDuration.TotalMinutes < stationEvent.ReoccurrenceDelay + lastTime.TotalMinutes)
                {
                    IncrementReason($"Within ReoccurrenceDelay ({stationEvent.ReoccurrenceDelay}m)");
                    continue;
                }

                if (_roundEnd.IsRoundEndRequested() && !stationEvent.OccursDuringRoundEnd)
                {
                    IncrementReason("RoundEndRequested && !OccursDuringRoundEnd");
                    continue;
                }
            }

            // Gamemode/rule-specific filters based on pacing state
            if (comp.PacingState == StorytellerPacingState.Recovery)
            {
                if (metadata.ThreatType != StorytellerThreatType.Helpful && metadata.ThreatType != StorytellerThreatType.Neutral)
                {
                    IncrementReason("RecoveryPacing: Only Helpful/Neutral allowed");
                    continue;
                }
            }
            else if (comp.PacingState == StorytellerPacingState.Relaxation)
            {
                if (metadata.ThreatType == StorytellerThreatType.MajorCalm || metadata.ThreatType == StorytellerThreatType.MajorAntag)
                {
                    IncrementReason("RelaxationPacing: Major threats forbidden");
                    continue;
                }
            }
            else if (comp.PacingState == StorytellerPacingState.Peak)
            {
                if (metadata.ThreatType == StorytellerThreatType.MajorAntag)
                {
                    IncrementReason("PeakPacing: Major Antags forbidden");
                    continue;
                }
            }

            if (metadata.ThreatType != StorytellerThreatType.Helpful && metadata.ThreatType != StorytellerThreatType.Neutral)
            {
                if (metadata.ThreatType == StorytellerThreatType.MajorAntag)
                {
                    if (metadata.ThreatCost > comp.MajorThreatBudget)
                    {
                        IncrementReason($"ThreatCost ({metadata.ThreatCost}) > MajorThreatBudget ({comp.MajorThreatBudget})");
                        continue;
                    }
                }
                else
                {
                    if (metadata.ThreatCost > comp.ThreatBudget)
                    {
                        IncrementReason($"ThreatCost ({metadata.ThreatCost}) > ThreatBudget ({comp.ThreatBudget})");
                        continue;
                    }
                }
            }

            if (GameTicker.IsGameRuleActive(proto.ID))
            {
                IncrementReason("GameRuleActive");
            }
        }

        foreach (var (reason, count) in reasons)
        {
            _sawmill.Warning($"  Skipped {count} events due to: {reason}");
        }

        void IncrementReason(string reason)
        {
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        }
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        if (!HasComp<MainStationComponent>(ev.Station))
            return;

        var query = EntityQueryEnumerator<StorytellerRuleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            RecordAlertLevelChange(comp, ev.Station, ev.AlertLevel);
        }
    }

    private void RecordAlertLevelChange(StorytellerRuleComponent comp, EntityUid station, string level)
    {
        var now = Timing.CurTime;
        if (!comp.AlertLevelHistory.TryGetValue(station, out var history))
        {
            history = new List<AlertLevelHistoryEntry>();
            comp.AlertLevelHistory[station] = history;
        }

        if (history.Count > 0 && history[^1].Level.Equals(level, StringComparison.OrdinalIgnoreCase))
            return;

        history.Add(new AlertLevelHistoryEntry { Time = now, Level = level });
        PruneAlertLevelHistory(comp);
    }

    private void PruneAlertLevelHistory(StorytellerRuleComponent comp)
    {
        var cutoff = Timing.CurTime - TimeSpan.FromHours(1);
        foreach (var (_, history) in comp.AlertLevelHistory)
        {
            if (history.Count <= 1)
                continue;

            int keepIndex = -1;
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].Time <= cutoff)
                {
                    keepIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (keepIndex > 0)
            {
                history.RemoveRange(0, keepIndex);
            }
        }
    }

    public float CalculateAlertLevelStress(StorytellerRuleComponent comp)
    {
        var t = Timing.CurTime;
        var windowStart = t - TimeSpan.FromHours(1);
        if (windowStart < comp.RuleStartTime)
            windowStart = comp.RuleStartTime;

        var totalWindowSeconds = (t - windowStart).TotalSeconds;
        if (totalWindowSeconds <= 0)
            return 0f;

        var stationStresses = new List<float>();

        foreach (var (station, history) in comp.AlertLevelHistory)
        {
            if (!Exists(station) || !HasComp<MainStationComponent>(station))
                continue;

            double greenDuration = 0;
            double totalDuration = 0;

            var defaultLevel = "green";
            if (TryComp<AlertLevelComponent>(station, out var alertComp) && alertComp.AlertLevels != null && !string.IsNullOrEmpty(alertComp.AlertLevels.DefaultLevel))
            {
                defaultLevel = alertComp.AlertLevels.DefaultLevel;
            }

            if (history.Count == 0)
            {
                var currentLevel = defaultLevel;
                if (alertComp != null)
                    currentLevel = alertComp.CurrentLevel;

                if (currentLevel.Equals(defaultLevel, StringComparison.OrdinalIgnoreCase))
                {
                    greenDuration = totalWindowSeconds;
                }
                totalDuration = totalWindowSeconds;
            }
            else
            {
                var activeLevel = defaultLevel;
                var firstEntry = history[0];
                if (firstEntry.Time > windowStart)
                {
                    activeLevel = firstEntry.Level;
                }
                else
                {
                    foreach (var entry in history)
                    {
                        if (entry.Time <= windowStart)
                            activeLevel = entry.Level;
                        else
                            break;
                    }
                }

                var currentIntervalStart = windowStart;
                foreach (var entry in history)
                {
                    if (entry.Time <= windowStart)
                        continue;

                    var duration = (entry.Time - currentIntervalStart).TotalSeconds;
                    if (activeLevel.Equals(defaultLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        greenDuration += duration;
                    }
                    totalDuration += duration;

                    activeLevel = entry.Level;
                    currentIntervalStart = entry.Time;
                }

                var lastDuration = (t - currentIntervalStart).TotalSeconds;
                if (activeLevel.Equals(defaultLevel, StringComparison.OrdinalIgnoreCase))
                {
                    greenDuration += lastDuration;
                }
                totalDuration += lastDuration;
            }

            if (totalDuration > 0)
            {
                var proportion = greenDuration / totalDuration;
                var stress = 10f * (1f - (float)proportion);
                stationStresses.Add(stress);
            }
        }

        if (stationStresses.Count == 0)
            return 0f;

        return stationStresses.Average();
    }
}

public struct StationMetrics
{
    public int TotalPlayers;
    public int AliveCount;
    public int DeadCount;
    public int GhostCount;
    public int SecurityCount;
    public int CargoBalance;
    public int SciencePoints;
    public float AtmosphereUnsafeRatio;
    public float PowerGridDeficitRatio;
    public int CrewWeaponCount;
    public int ActiveAntagonistCount;
    public int ActiveErtCount;
    public bool SingularityActive;
    public bool SingularityContained;
    public bool TeslaActive;
    public bool TeslaContained;
    public float ResearchStorytellerScore;
    public int UnlockedTechnologyCount;
    public int TotalTechnologyCount;
    public float MaxResearchStorytellerScore;
    public int CrewRosterCount;
    public int RosterCommandCount;
    public int RosterCrewCount;
    public int DeadCommandCount;
    public int DeadCrewCount;
    public float MaterialStrengthScore;
    public Dictionary<string, int> CrewDistribution;
    public float PlayerJoinRate;
    public float PlayerLeaveRate;
    public int AvailableGhostRoles;
    public int AnomaliesCount;
    public int ActiveArtifactsCount;
    public int PuddlesCount;
    public int FootprintsCount;
    public int TrashCount;
    public float AverageCrewDamage;
    public int TotalStationTiles;
    public float StationStrength;
    public float AntagonistStressScore;
    public float StressDead;
    public float StressContainment;
    public float StressEconomy;
    public float StressDamage;
    public float StressAnomaly;
    public float StressMess;
    public float StrengthArmedCrew;
    public float StrengthSecurity;
    public float StrengthCargo;
    public float StrengthTechnology;
    public float StrengthMaterials;
    public float StressPower;
    public float StressAtmosphere;
    public float StressGhost;
    public float StressAntagonist;
    public float StressAlertLevel;
}
