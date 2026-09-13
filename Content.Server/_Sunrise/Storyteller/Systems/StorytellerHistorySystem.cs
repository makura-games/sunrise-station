using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Nuke;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Server.Tesla.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Singularity.Components;
using Content.Shared.Starlight.Energy.Supermatter;
using Content.Shared._Sunrise.Storyteller;
using Content.Shared._Sunrise.Storyteller.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Server.Bed.Cryostorage;
using Content.Shared.Bed.Cryostorage;
using Content.Server.AlertLevel;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Storyteller.Systems;

/// <summary>
/// Dedicated system that records significant timeline events during the round
/// and formats them into a narrative history of the active storyteller.
/// </summary>
public sealed partial class StorytellerHistorySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private SharedJobSystem _jobSystem = default!;
    [Dependency] private SharedResearchSystem _researchSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    private readonly List<StorytellerHistoryEntry> _history = new();
    private readonly HashSet<string> _researchedDisciplines = new();

    private float _breachCheckTimer = 0f;
    private bool _singularityEscaped = false;
    private bool _teslaEscaped = false;

    // Sunrise-Edit - Track active storyteller rules start times and alert level start times
    private readonly Dictionary<string, TimeSpan> _activeRules = new();
    private readonly Dictionary<string, TimeSpan> _alertLevelStartTimes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRuleStartedEvent>(OnGameRuleStarted);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SunriseSupermatterDelaminatedEvent>(OnSupermatterDelaminated);
        SubscribeLocalEvent<SunriseExplosionEvent>(OnSunriseExplosion);
        SubscribeLocalEvent<TechnologyDatabaseComponent, TechnologyDatabaseModifiedEvent>(OnTechnologyDatabaseModified);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<CryostorageContainedComponent, CryostorageEnteredEvent>(OnCryostorageEntered);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnReset);

        // Sunrise-Edit - Custom story tracking events
        SubscribeLocalEvent<GameRuleEndedEvent>(OnGameRuleEnded);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<SingularityComponent, ComponentInit>(OnSingularityInit);
        SubscribeLocalEvent<TeslaEnergyBallComponent, ComponentInit>(OnTeslaInit);
        SubscribeLocalEvent<SupermatterComponent, ComponentStartup>(OnSupermatterStartup);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
        SubscribeLocalEvent<NukeDisarmSuccessEvent>(OnNukeDisarmSuccess);
        SubscribeLocalEvent<SunriseNukeArmedEvent>(OnNukeArmed);
        SubscribeLocalEvent<AntagSelectionComponent, AntagSelectionCompleteEvent>(OnAntagSelectionComplete);

        SetDefaultAlertLevelTime();
    }

    private void SetDefaultAlertLevelTime()
    {
        _alertLevelStartTimes.Clear();
    }

    private void OnReset(RoundRestartCleanupEvent ev)
    {
        _history.Clear();
        _researchedDisciplines.Clear();
        _singularityEscaped = false;
        _teslaEscaped = false;
        _breachCheckTimer = 0f;
        _activeRules.Clear();
        SetDefaultAlertLevelTime();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check for containment breaches every 5 seconds
        _breachCheckTimer += frameTime;
        if (_breachCheckTimer < 5f)
            return;
        _breachCheckTimer = 0f;

        CheckContainmentBreaches();
    }

    /// <summary>
    /// Gets a copy of the recorded history.
    /// </summary>
    public StorytellerHistoryEntry[] GetHistory()
    {
        return _history.ToArray();
    }

    /// <summary>
    /// Gets the name of the active storyteller.
    /// </summary>
    public string? GetActiveStorytellerName()
    {
        var query = EntityQueryEnumerator<StorytellerRuleComponent>();
        if (query.MoveNext(out _, out var comp))
        {
            var typeStr = comp.StorytellerType.ToString().ToLower();
            return Loc.TryGetString($"ui-vote-storyteller-type-{typeStr}-name", out var localized) ? localized : comp.StorytellerType.ToString();
        }
        return null;
    }

    /// <summary>
    /// Logs a narrative history entry.
    /// </summary>
    public void LogHistoryEntry(StorytellerHistoryType type, string locKey, params (string key, object val)[] args)
    {
        var description = Loc.GetString(locKey, args);
        var entry = new StorytellerHistoryEntry(_gameTicker.RoundDuration(), type, description);
        _history.Add(entry);
    }

    private void OnGameRuleStarted(ref GameRuleStartedEvent args)
    {
        // Skip technical, meta, or post-round rules
        if (args.RuleId.Contains("StationVariation") || args.RuleId.Contains("LobbyBackground") || args.RuleId.Contains("PostRound"))
            return;

        // Sunrise-Edit - Track rule start time
        _activeRules[args.RuleId] = _gameTicker.RoundDuration();

        var autoStartKey = $"storyteller-metadata-{args.RuleId.ToLower()}-start";
        if (!Loc.TryGetString(autoStartKey, out _))
        {
            // If there's no custom start string, we do NOT log the event's start at all to avoid boring report-like lines.
            return;
        }

        var ruleName = args.RuleId;
        if (_protoManager.TryIndex<EntityPrototype>(args.RuleId, out var entityProto))
        {
            ruleName = Loc.TryGetString(entityProto.Name, out var locName) ? locName : entityProto.Name;
        }

        if (_protoManager.TryIndex<StorytellerMetadataPrototype>(args.RuleId, out var metadata))
        {
            var targetKey = !string.IsNullOrEmpty(metadata.DescriptionLocKey) && Loc.TryGetString(metadata.DescriptionLocKey, out _)
                ? metadata.DescriptionLocKey
                : autoStartKey;

            var histType = metadata.ThreatType switch
            {
                StorytellerThreatType.Helpful => StorytellerHistoryType.HelpfulEvent,
                StorytellerThreatType.Neutral => StorytellerHistoryType.NeutralEvent,
                StorytellerThreatType.MinorCalm => StorytellerHistoryType.MinorCalmEvent,
                StorytellerThreatType.MajorCalm => StorytellerHistoryType.MajorCalmEvent,
                StorytellerThreatType.MinorAntag => StorytellerHistoryType.MinorAntagEvent,
                StorytellerThreatType.MajorAntag => StorytellerHistoryType.MajorAntagEvent,
                _ => StorytellerHistoryType.NeutralEvent
            };
            LogHistoryEntry(histType, targetKey, ("name", (object) ruleName));
        }
        else
        {
            LogHistoryEntry(StorytellerHistoryType.NeutralEvent, autoStartKey, ("name", (object) ruleName));
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var target = args.Target;

        var station = _stationSystem.GetOwningStation(target);
        if (station == null || !HasComp<MainStationComponent>(station.Value))
            return;

        if (!TryComp<MindContainerComponent>(target, out var mindContainer) || !_mindSystem.TryGetMind(target, out var mindId, out _))
            return;

        // Ensure it's a crew member (humanoid or has a job)
        if (!_jobSystem.MindTryGetJob(mindId, out var jobProto))
            return;

        var characterName = Name(target);
        var jobName = Loc.TryGetString(jobProto.Name, out var locName) ? locName : jobProto.ID;
        var cause = GetCauseOfDeath(target);
        var location = GetLocationName(target);

        // Sunrise-Edit - Immersive, grammatically correct Russian location formatting
        string locationFormatted;
        if (location == Loc.GetString("storyteller-history-location-space"))
        {
            locationFormatted = Loc.GetString("storyteller-history-location-space-genitive");
        }
        else if (location == Loc.GetString("storyteller-history-location-unknown"))
        {
            locationFormatted = Loc.GetString("storyteller-history-location-unknown-genitive");
        }
        else
        {
            locationFormatted = Loc.GetString("storyteller-history-location-room-genitive", ("room", location));
        }

        // Sunrise-Edit - Randomized crew death template variations (1 to 4)
        var templateNum = _random.Next(1, 5);
        var templateKey = $"storyteller-history-crew-death-{templateNum}";

        LogHistoryEntry(StorytellerHistoryType.Death, templateKey,
            ("name", (object) characterName),
            ("job", (object) jobName),
            ("cause", (object) cause),
            ("location", (object) locationFormatted));
    }

    private void OnSupermatterDelaminated(SunriseSupermatterDelaminatedEvent ev)
    {
        var locationName = GetLocationName(ev.Supermatter);
        LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-supermatter-collapse", ("location", (object) locationName));
    }

    private void OnSunriseExplosion(SunriseExplosionEvent ev)
    {
        // Only log explosions that are large enough to be notable (magnitude / affected tiles > 150)
        if (ev.AffectedTiles < 150 && ev.Intensity < 150)
            return;

        var coords = ev.Epicenter;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        EntityUid? closestBeacon = null;
        var minDistance = float.MaxValue;

        while (query.MoveNext(out var beaconUid, out var beacon, out var beaconXform))
        {
            if (!beacon.Enabled || string.IsNullOrEmpty(beacon.Text))
                continue;

            if (beaconXform.MapID != coords.MapId)
                continue;

            var distance = (beaconXform.WorldPosition - coords.Position).LengthSquared();
            if (distance < minDistance)
            {
                minDistance = distance;
                closestBeacon = beaconUid;
            }
        }

        var location = Loc.GetString("storyteller-history-location-unknown");
        if (closestBeacon != null && TryComp<NavMapBeaconComponent>(closestBeacon.Value, out var closestBeaconComp))
        {
            var text = closestBeaconComp.Text ?? string.Empty;
            location = Loc.TryGetString(text, out var locStr) ? locStr : text;
        }

        // Sunrise-Edit - Literary explosion severity classification instead of raw stats log
        string severityKey;
        if (ev.Intensity >= 750 || ev.AffectedTiles >= 750)
            severityKey = "storyteller-history-explosion-destructive";
        else if (ev.Intensity >= 250 || ev.AffectedTiles >= 250)
            severityKey = "storyteller-history-explosion-strong";
        else
            severityKey = "storyteller-history-explosion-weak";

        var severity = Loc.GetString(severityKey);

        LogHistoryEntry(StorytellerHistoryType.Explosion, "storyteller-history-large-explosion",
            ("location", (object) location), ("severity", (object) severity));
    }

    private void OnTechnologyDatabaseModified(EntityUid uid, TechnologyDatabaseComponent component, ref TechnologyDatabaseModifiedEvent args)
    {
        foreach (var discipline in _protoManager.EnumeratePrototypes<TechDisciplinePrototype>())
        {
            if (_researchedDisciplines.Contains(discipline.ID))
                continue;

            var allTechs = _protoManager.EnumeratePrototypes<TechnologyPrototype>()
                .Where(t => t.Discipline == discipline.ID)
                .ToList();

            if (allTechs.Count == 0)
                continue;

            if (allTechs.All(tech => _researchSystem.IsTechnologyUnlocked(uid, tech.ID, component)))
            {
                _researchedDisciplines.Add(discipline.ID);
                var disciplineName = Loc.TryGetString(discipline.Name, out var name) ? name : discipline.ID;
                LogHistoryEntry(StorytellerHistoryType.Research, "storyteller-history-research-complete", ("discipline", (object) disciplineName));
            }
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!args.LateJoin)
            return;

        var station = _stationSystem.GetOwningStation(args.Mob);
        if (station == null || !HasComp<MainStationComponent>(station.Value))
            return;

        var jobName = Loc.GetString("storyteller-history-arrival-no-job");
        if (!string.IsNullOrEmpty(args.JobId) && _protoManager.TryIndex<JobPrototype>(args.JobId, out var jobProto))
        {
            jobName = Loc.TryGetString(jobProto.Name, out var locName) ? locName : args.JobId;
        }

        var icName = Name(args.Mob);
        LogHistoryEntry(StorytellerHistoryType.Arrival, "storyteller-history-arrival", ("name", (object) icName), ("job", (object) jobName));
    }

    private void OnCryostorageEntered(EntityUid uid, CryostorageContainedComponent component, CryostorageEnteredEvent args)
    {
        var station = _stationSystem.GetOwningStation(uid);
        if (station == null || !HasComp<MainStationComponent>(station.Value))
            return;

        var jobName = Loc.GetString("earlyleave-cryo-job-unknown");
        var characterName = Name(uid);

        if (_mindSystem.TryGetMind(uid, out var mindId, out _) && _jobSystem.MindTryGetJob(mindId, out var jobProto))
        {
            jobName = Loc.TryGetString(jobProto.Name, out var locName) ? locName : jobProto.ID;
        }

        LogHistoryEntry(StorytellerHistoryType.Departure, "storyteller-history-cryo-departure", ("name", (object) characterName), ("job", (object) jobName));
    }

    private void CheckContainmentBreaches()
    {
        var fieldsExist = false;
        var fieldQuery = EntityQueryEnumerator<ContainmentFieldComponent>();
        if (fieldQuery.MoveNext(out _, out _))
        {
            fieldsExist = true;
        }

        // Singularity
        var singuloQuery = EntityQueryEnumerator<SingularityComponent, TransformComponent>();
        var singuloActive = false;
        var singuloContained = true;
        EntityUid? singuloUid = null;
        while (singuloQuery.MoveNext(out var uid, out _, out var xform))
        {
            singuloActive = true;
            singuloUid = uid;
            if (!fieldsExist || !IsNearActiveContainmentField(xform))
            {
                singuloContained = false;
            }
        }

        if (singuloActive)
        {
            if (!singuloContained && !_singularityEscaped)
            {
                _singularityEscaped = true;
                var location = singuloUid.HasValue ? GetLocationName(singuloUid.Value) : Loc.GetString("storyteller-history-location-unknown");
                LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-singularity-escaped", ("location", (object) location));
            }
            else if (singuloContained)
            {
                _singularityEscaped = false;
            }
        }
        else
        {
            _singularityEscaped = false;
        }

        // Tesla
        var teslaQuery = EntityQueryEnumerator<TeslaEnergyBallComponent, TransformComponent>();
        var teslaActive = false;
        var teslaContained = true;
        EntityUid? teslaUid = null;
        while (teslaQuery.MoveNext(out var uid, out _, out var xform))
        {
            teslaActive = true;
            teslaUid = uid;
            if (!fieldsExist || !IsNearActiveContainmentField(xform))
            {
                teslaContained = false;
            }
        }

        if (teslaActive)
        {
            if (!teslaContained && !_teslaEscaped)
            {
                _teslaEscaped = true;
                var location = teslaUid.HasValue ? GetLocationName(teslaUid.Value) : Loc.GetString("storyteller-history-location-unknown");
                LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-tesla-escaped", ("location", (object) location));
            }
            else if (teslaContained)
            {
                _teslaEscaped = false;
            }
        }
        else
        {
            _teslaEscaped = false;
        }
    }

    private bool IsNearActiveContainmentField(TransformComponent entityXform)
    {
        var fieldQuery = EntityQueryEnumerator<ContainmentFieldComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out _, out _, out var fieldXform))
        {
            if (entityXform.MapID == fieldXform.MapID)
            {
                var distance = (entityXform.WorldPosition - fieldXform.WorldPosition).Length();
                if (distance <= 10.0f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private string GetLocationName(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return Loc.GetString("storyteller-history-location-unknown");

        if (xform.GridUid is not { } gridUid)
            return Loc.GetString("storyteller-history-location-space");

        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        EntityUid? closestBeacon = null;
        var minDistance = float.MaxValue;

        while (query.MoveNext(out var beaconUid, out var beacon, out var beaconXform))
        {
            if (!beacon.Enabled || string.IsNullOrEmpty(beacon.Text))
                continue;

            if (beaconXform.GridUid != gridUid)
                continue;

            var distance = (beaconXform.LocalPosition - xform.LocalPosition).LengthParagraphSafe();
            if (distance < minDistance)
            {
                minDistance = distance;
                closestBeacon = beaconUid;
            }
        }

        if (closestBeacon != null && TryComp<NavMapBeaconComponent>(closestBeacon.Value, out var closestBeaconComp))
        {
            var text = closestBeaconComp.Text ?? string.Empty;
            return Loc.TryGetString(text, out var locStr) ? locStr : text;
        }

        return Loc.GetString("storyteller-history-location-unknown");
    }

    private string GetCauseOfDeath(EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return Loc.GetString("storyteller-cause-death-unknown");

        var damage = _damageableSystem.GetAllDamage((target, damageable));
        if (damage.Empty)
            return Loc.GetString("storyteller-cause-death-unknown");

        var highestDamageType = "";
        var maxDamage = FixedPoint2.Zero;

        foreach (var (type, amount) in damage.DamageDict)
        {
            if (amount > maxDamage)
            {
                maxDamage = amount;
                highestDamageType = type;
            }
        }

        if (string.IsNullOrEmpty(highestDamageType) || maxDamage <= 0)
            return Loc.GetString("storyteller-cause-death-unknown");

        var locKey = $"storyteller-cause-death-{highestDamageType.ToLower()}";
        return Loc.TryGetString(locKey, out var result) ? result : highestDamageType;
    }

    // Sunrise-Edit - Event ending, alert levels, and entity spawns
    private void OnGameRuleEnded(ref GameRuleEndedEvent args)
    {
        if (!_activeRules.Remove(args.RuleId, out var startTime))
            return;

        var duration = _gameTicker.RoundDuration() - startTime;
        var minutes = (int) Math.Max(1, Math.Round(duration.TotalMinutes));

        var autoEndKey = $"storyteller-metadata-{args.RuleId.ToLower()}-end";
        var hasAutoEnd = Loc.TryGetString(autoEndKey, out _);

        var ruleName = args.RuleId;
        if (_protoManager.TryIndex<EntityPrototype>(args.RuleId, out var entityProto))
        {
            ruleName = Loc.TryGetString(entityProto.Name, out var locName) ? locName : entityProto.Name;
        }

        if (_protoManager.TryIndex<StorytellerMetadataPrototype>(args.RuleId, out var metadata) &&
            !string.IsNullOrEmpty(metadata.EndedLocKey) &&
            Loc.TryGetString(metadata.EndedLocKey, out _))
        {
            LogHistoryEntry(StorytellerHistoryType.NeutralEvent, metadata.EndedLocKey, ("name", (object) ruleName), ("duration", (object) minutes));
        }
        else if (hasAutoEnd)
        {
            LogHistoryEntry(StorytellerHistoryType.NeutralEvent, autoEndKey, ("name", (object) ruleName), ("duration", (object) minutes));
        }
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        var now = _gameTicker.RoundDuration();

        if (args.PreviousLevel == args.AlertLevel)
            return;

        if (!HasComp<MainStationComponent>(args.Station))
            return;

        // Sunrise-Edit - Ignore any initial/starting alert level changes (first 5 seconds) to ignore initial station code
        if (now.TotalSeconds < 5)
        {
            _alertLevelStartTimes[args.AlertLevel] = now;
            return;
        }

        var localizedNew = Loc.TryGetString($"alert-level-{args.AlertLevel.ToLower()}", out var newName) ? newName : args.AlertLevel;
        var colorNew = GetAlertLevelColor(args.AlertLevel);

        if (!string.IsNullOrEmpty(args.PreviousLevel) && args.PreviousLevel != args.AlertLevel && _alertLevelStartTimes.TryGetValue(args.PreviousLevel, out var prevStart))
        {
            var duration = now - prevStart;
            var minutes = (int) Math.Max(1, Math.Round(duration.TotalMinutes));
            var localizedPrev = Loc.TryGetString($"alert-level-{args.PreviousLevel.ToLower()}", out var prevName) ? prevName : args.PreviousLevel;
            var colorPrev = GetAlertLevelColor(args.PreviousLevel);

            LogHistoryEntry(StorytellerHistoryType.StationEvent, "storyteller-history-alert-level-changed-with-prev",
                ("level", (object) localizedNew),
                ("color", (object) colorNew),
                ("prev", (object) localizedPrev),
                ("prevColor", (object) colorPrev),
                ("duration", (object) minutes));

            _alertLevelStartTimes.Remove(args.PreviousLevel);
        }
        else
        {
            LogHistoryEntry(StorytellerHistoryType.StationEvent, "storyteller-history-alert-level-changed",
                ("level", (object) localizedNew),
                ("color", (object) colorNew));
        }

        _alertLevelStartTimes[args.AlertLevel] = now;
    }

    private string GetAlertLevelColor(string level)
    {
        switch (level.ToLower())
        {
            case "green":
                return "#36FF36"; // Green
            case "blue":
                return "#3498db"; // Blue
            case "violet":
            case "indigo":
                return "#9b59b6"; // Violet/Indigo
            case "yellow":
                return "#f1c40f"; // Yellow
            case "red":
                return "#e74c3c"; // Red
            case "gamma":
                return "#e67e22"; // Orange/Gamma
            case "delta":
                return "#8e44ad"; // Dark violet/Delta
            default:
                return "#7DF9FF"; // Default light blue
        }
    }

    private void OnSingularityInit(EntityUid uid, SingularityComponent component, ComponentInit args)
    {
        var location = GetLocationName(uid);
        LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-singularity-spawned", ("location", (object) location));
    }

    private void OnTeslaInit(EntityUid uid, TeslaEnergyBallComponent component, ComponentInit args)
    {
        var location = GetLocationName(uid);
        LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-tesla-spawned", ("location", (object) location));
    }

    private void OnSupermatterStartup(EntityUid uid, SupermatterComponent component, ComponentStartup args)
    {
        var location = GetLocationName(uid);
        LogHistoryEntry(StorytellerHistoryType.AnomalyEngine, "storyteller-history-supermatter-spawned", ("location", (object) location));
    }

    private void OnNukeArmed(SunriseNukeArmedEvent ev)
    {
        LogHistoryEntry(StorytellerHistoryType.StationEvent, "storyteller-history-nuke-armed", ("location", (object) ev.Location));
    }

    private void OnNukeDisarmSuccess(NukeDisarmSuccessEvent ev)
    {
        LogHistoryEntry(StorytellerHistoryType.StationEvent, "storyteller-history-nuke-disarmed");
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        LogHistoryEntry(StorytellerHistoryType.Explosion, "storyteller-history-nuke-exploded");
    }

    private void OnAntagSelectionComplete(EntityUid uid, AntagSelectionComponent component, ref AntagSelectionCompleteEvent args)
    {
        // Sunrise-Edit - Retrieve actual prototype ID of the gamerule entity rather than EntityName
        var ruleId = MetaData(uid).EntityPrototype?.ID ?? Name(uid);
        var lowercaseId = ruleId.ToLower();

        // Check if there are assigned minds
        if (component.AssignedMinds.Count == 0)
            return;

        var names = component.AssignedMinds
            .SelectMany(entry => entry.Value)
            .Select(assigned => assigned.name)
            .ToList();
        var playersList = string.Join(", ", names);

        var key = $"storyteller-metadata-{lowercaseId}-assigned";
        if (Loc.TryGetString(key, out _))
        {
            LogHistoryEntry(StorytellerHistoryType.MajorAntagEvent, key, ("players", (object) playersList));
        }
    }
}

public static class Vector2Extensions
{
    public static float LengthParagraphSafe(this System.Numerics.Vector2 vector)
    {
        return vector.LengthSquared();
    }
}
