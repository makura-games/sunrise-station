using System.Globalization;
using System.Linq;
using Prometheus;
using Content.Shared.AlertLevel;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Shared._Sunrise.Storyteller.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;

namespace Content.Server._Sunrise.Storyteller.Systems;

public sealed partial class StorytellerSystem
{
    private ISawmill _sawmill = default!;

    // Prometheus Metrics
    private static readonly Gauge CrewStressGauge = Metrics.CreateGauge(
        "ss14_storyteller_crew_stress",
        "Current stress rating of the crew (0 to 100).");

    private static readonly Gauge ThreatBudgetGauge = Metrics.CreateGauge(
        "ss14_storyteller_threat_budget",
        "Current threat budget available to spend on challenging events.");
    private static readonly Gauge MajorThreatBudgetGauge = Metrics.CreateGauge(
        "ss14_storyteller_major_threat_budget",
        "Current major threat budget available to spend on major antag/calm events.");
    private static readonly Gauge PacingStateGauge = Metrics.CreateGauge(
        "ss14_storyteller_pacing_state",
        "Current storyteller pacing state (0 = Relaxation, 1 = BuildUp, 2 = Peak, 3 = Recovery).");

    private static readonly Gauge StorytellerTypeGauge = Metrics.CreateGauge(
        "ss14_storyteller_type",
        "Current storyteller type (0 = Calm, 1 = Classic, 2 = Insane).");

    private static readonly Gauge AlivePlayersGauge = Metrics.CreateGauge(
        "ss14_storyteller_alive_players",
        "Number of active alive players currently in-game.");

    private static readonly Gauge DeadPlayersGauge = Metrics.CreateGauge(
        "ss14_storyteller_dead_players",
        "Number of dead players currently in-game.");

    private static readonly Gauge GhostPlayersGauge = Metrics.CreateGauge(
        "ss14_storyteller_ghost_players",
        "Number of ghosts and observers currently in-game.");

    private static readonly Gauge SecurityCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_security_count",
        "Number of active alive security personnel.");

    private static readonly Gauge CargoBalanceGauge = Metrics.CreateGauge(
        "ss14_storyteller_cargo_balance",
        "Sum of cargo accounts or current primary cargo balance.");

    private static readonly Gauge SciencePointsGauge = Metrics.CreateGauge(
        "ss14_storyteller_science_points",
        "Sum of scientific research points currently available.");

    private static readonly Gauge AtmosUnsafeRatioGauge = Metrics.CreateGauge(
        "ss14_storyteller_atmos_unsafe_ratio",
        "Ratio of atmospheric monitors in an unsafe state.");

    private static readonly Gauge PowerDeficitRatioGauge = Metrics.CreateGauge(
        "ss14_storyteller_power_deficit_ratio",
        "Ratio of drained/empty APC batteries to total APCs.");

    private static readonly Gauge CrewWeaponCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_crew_weapon_count",
        "Total number of unique living crew members carrying at least one active weapon.");

    private static readonly Gauge ActiveAntagonistCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_active_antagonist_count",
        "Number of active alive antagonists.");

    private static readonly Gauge ActiveErtCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_active_ert_count",
        "Number of active alive Emergency Response Team (ERT) and NT Command members.");

    private static readonly Gauge SingularityActiveGauge = Metrics.CreateGauge(
        "ss14_storyteller_singularity_active",
        "Whether a singularity is currently active (0 or 1).");

    private static readonly Gauge TeslaActiveGauge = Metrics.CreateGauge(
        "ss14_storyteller_tesla_active",
        "Whether a Tesla energy ball is currently active (0 or 1).");

    private static readonly Gauge ResearchStorytellerScoreGauge = Metrics.CreateGauge(
        "ss14_storyteller_research_storyteller_score",
        "Weighted storyteller score from unlocked technologies.");

    private static readonly Gauge UnlockedTechnologyCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_unlocked_technology_count",
        "Number of unique technologies unlocked on the station.");

    private static readonly Gauge MaxResearchStorytellerScoreGauge = Metrics.CreateGauge(
        "ss14_storyteller_max_research_storyteller_score",
        "Maximum weighted storyteller score for the full technology catalog.");

    private static readonly Gauge AvailableGhostRolesGauge = Metrics.CreateGauge(
        "ss14_storyteller_available_ghost_roles",
        "Total number of available ghost roles.");


    private static readonly Gauge AnomaliesCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_anomalies_count",
        "Total number of active anomalies.");

    private static readonly Gauge ActiveArtifactsCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_active_artifacts_count",
        "Total number of active uncontained artifacts.");

    private static readonly Gauge PuddlesCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_puddles_count",
        "Total number of puddles on the station (excluding footprints).");

    private static readonly Gauge TrashCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_trash_count",
        "Total number of trash items on the station grid.");

    private static readonly Gauge AverageCrewDamageGauge = Metrics.CreateGauge(
        "ss14_storyteller_average_crew_damage",
        "Average damage across the living crew members.");

    private static readonly Gauge StationStrengthGauge = Metrics.CreateGauge(
        "ss14_storyteller_station_strength",
        "Normalized defensive and technological strength score of the station (0 to 100).");

    private static readonly Gauge CrewRosterCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_crew_roster_count",
        "Total crew minds assigned to station jobs this round.");

    private static readonly Gauge MaterialStrengthScoreGauge = Metrics.CreateGauge(
        "ss14_storyteller_material_strength_score",
        "Weighted material volume score (raw) used for station strength normalization.");

    private static readonly Gauge FootprintsCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_footprints_count",
        "Total number of footprint decals on the station.");

    private static readonly Gauge TotalStationTilesGauge = Metrics.CreateGauge(
        "ss14_storyteller_total_station_tiles",
        "Atmos monitor count used as station tile proxy for mess density.");

    private static readonly Gauge RosterCommandCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_roster_command_count",
        "Crew roster minds with command staff jobs.");

    private static readonly Gauge RosterCrewCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_roster_crew_count",
        "Crew roster minds without command staff jobs.");

    private static readonly Gauge DeadCommandCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_dead_command_count",
        "Dead command staff on the crew roster.");

    private static readonly Gauge DeadCrewCountGauge = Metrics.CreateGauge(
        "ss14_storyteller_dead_crew_count",
        "Dead non-command crew on the crew roster.");

    private static readonly Gauge PlayerJoinRateGauge = Metrics.CreateGauge(
        "ss14_storyteller_player_join_rate",
        "Player lobby joins in the last 10 minutes.");

    private static readonly Gauge PlayerLeaveRateGauge = Metrics.CreateGauge(
        "ss14_storyteller_player_leave_rate",
        "Player disconnects in the last 10 minutes.");

    private static readonly Gauge SingularityContainedGauge = Metrics.CreateGauge(
        "ss14_storyteller_singularity_contained",
        "Whether an active singularity is within containment (0 or 1).");

    private static readonly Gauge TeslaContainedGauge = Metrics.CreateGauge(
        "ss14_storyteller_tesla_contained",
        "Whether an active tesla is within containment (0 or 1).");

    private static readonly Gauge MaxThreatBudgetGauge = Metrics.CreateGauge(
        "ss14_storyteller_max_threat_budget",
        "Maximum threat budget cap for this storyteller rule.");

    private static readonly Gauge StressDeadGauge = Metrics.CreateGauge("ss14_storyteller_stress_dead", "Stress from dead crew.");
    private static readonly Gauge StressGhostGauge = Metrics.CreateGauge("ss14_storyteller_stress_ghost", "Stress from ghosts of living crew.");
    private static readonly Gauge StressContainmentGauge = Metrics.CreateGauge("ss14_storyteller_stress_containment", "Stress from singularity or tesla containment breach.");
    private static readonly Gauge StressEconomyGauge = Metrics.CreateGauge("ss14_storyteller_stress_economy", "Stress from cargo budget deficit.");
    private static readonly Gauge StressDamageGauge = Metrics.CreateGauge("ss14_storyteller_stress_damage", "Stress from average crew damage.");
    private static readonly Gauge StressAnomalyGauge = Metrics.CreateGauge("ss14_storyteller_stress_anomaly", "Stress from active anomalies and uncontained artifacts.");
    private static readonly Gauge StressMessGauge = Metrics.CreateGauge("ss14_storyteller_stress_mess", "Stress from puddles and trash on the station.");
    private static readonly Gauge StressAlertLevelGauge = Metrics.CreateGauge("ss14_storyteller_stress_alert_level", "Stress from station alert levels.");

    private static readonly Gauge StrengthArmedCrewGauge = Metrics.CreateGauge("ss14_storyteller_strength_armed_crew", "Station strength from peaceful armed crew members.");
    private static readonly Gauge StrengthSecurityGauge = Metrics.CreateGauge("ss14_storyteller_strength_security", "Station strength from security personnel.");
    private static readonly Gauge StrengthCargoGauge = Metrics.CreateGauge("ss14_storyteller_strength_cargo", "Station strength from cargo budget balance.");
    private static readonly Gauge StrengthTechnologyGauge = Metrics.CreateGauge("ss14_storyteller_strength_technology", "Station strength from researched technologies.");
    private static readonly Gauge StrengthMaterialsGauge = Metrics.CreateGauge("ss14_storyteller_strength_materials", "Station strength from lathe/silo material reserves.");
    private static readonly Gauge StressPowerGauge = Metrics.CreateGauge("ss14_storyteller_stress_power", "Stress from power grid deficit.");
    private static readonly Gauge StressAtmosphereGauge = Metrics.CreateGauge("ss14_storyteller_stress_atmosphere", "Stress from unsafe atmospheric conditions.");
    private static readonly Gauge StressAntagonistGauge = Metrics.CreateGauge("ss14_storyteller_stress_antagonist", "Stress from active antagonists.");
    private static readonly Gauge TotalPlayersGauge = Metrics.CreateGauge("ss14_storyteller_total_players", "Total number of active players currently in-game.");
    private static readonly Gauge AlertLevelGauge = Metrics.CreateGauge("ss14_storyteller_alert_level", "Current station alert level (0 = Green, 1 = Blue, 2 = Violet, 3 = Yellow, 4 = Red, 5 = Gamma, 6 = Delta, 7 = Epsilon).");

    private static readonly Counter EventsTriggeredCounter = Metrics.CreateCounter(
        "ss14_storyteller_events_triggered_total",
        "Total number of events triggered by the storyteller.",
        new CounterConfiguration
        {
            LabelNames = new[] { "event_id", "threat_type" }
        });

    private void InitializeMetrics()
    {
        _sawmill = Logger.GetSawmill("storyteller");
        _sawmill.Level = LogLevel.Info;
    }

    private double GetAlertLevelNumeric(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "green" => 0,
            "blue" => 1,
            "violet" => 2,
            "yellow" => 3,
            "red" => 4,
            "gamma" => 5,
            "delta" or "deltanuke" => 6,
            "epsilon" => 7,
            _ => -1
        };
    }

    private void UpdatePrometheusGauges(StorytellerRuleComponent comp, StationMetrics metrics)
    {
        CrewStressGauge.Set(comp.CrewStress);
        ThreatBudgetGauge.Set(comp.ThreatBudget);
        MajorThreatBudgetGauge.Set(comp.MajorThreatBudget);
        MaxThreatBudgetGauge.Set(comp.MaxThreatBudget);
        PacingStateGauge.Set((double)comp.PacingState);

        StorytellerTypeGauge.Set((double)comp.StorytellerType);
        AlivePlayersGauge.Set(metrics.AliveCount);
        DeadPlayersGauge.Set(metrics.DeadCount);
        GhostPlayersGauge.Set(metrics.GhostCount);
        SecurityCountGauge.Set(metrics.SecurityCount);
        CargoBalanceGauge.Set(metrics.CargoBalance);
        SciencePointsGauge.Set(metrics.SciencePoints);

        AtmosUnsafeRatioGauge.Set(metrics.AtmosphereUnsafeRatio);
        PowerDeficitRatioGauge.Set(metrics.PowerGridDeficitRatio);
        CrewWeaponCountGauge.Set(metrics.CrewWeaponCount);
        ActiveAntagonistCountGauge.Set(metrics.ActiveAntagonistCount);
        ActiveErtCountGauge.Set(metrics.ActiveErtCount);
        SingularityActiveGauge.Set(metrics.SingularityActive ? 1.0 : 0.0);
        SingularityContainedGauge.Set(metrics.SingularityContained ? 1.0 : 0.0);
        TeslaActiveGauge.Set(metrics.TeslaActive ? 1.0 : 0.0);
        TeslaContainedGauge.Set(metrics.TeslaContained ? 1.0 : 0.0);
        ResearchStorytellerScoreGauge.Set(metrics.ResearchStorytellerScore);
        UnlockedTechnologyCountGauge.Set(metrics.UnlockedTechnologyCount);
        MaxResearchStorytellerScoreGauge.Set(metrics.MaxResearchStorytellerScore);
        AvailableGhostRolesGauge.Set(metrics.AvailableGhostRoles);


        AnomaliesCountGauge.Set(metrics.AnomaliesCount);
        ActiveArtifactsCountGauge.Set(metrics.ActiveArtifactsCount);
        PuddlesCountGauge.Set(metrics.PuddlesCount);
        TrashCountGauge.Set(metrics.TrashCount);
        AverageCrewDamageGauge.Set(metrics.AverageCrewDamage);
        StationStrengthGauge.Set(metrics.StationStrength);
        CrewRosterCountGauge.Set(metrics.CrewRosterCount);
        RosterCommandCountGauge.Set(metrics.RosterCommandCount);
        RosterCrewCountGauge.Set(metrics.RosterCrewCount);
        DeadCommandCountGauge.Set(metrics.DeadCommandCount);
        DeadCrewCountGauge.Set(metrics.DeadCrewCount);
        MaterialStrengthScoreGauge.Set(metrics.MaterialStrengthScore);
        FootprintsCountGauge.Set(metrics.FootprintsCount);
        TotalStationTilesGauge.Set(metrics.TotalStationTiles);
        PlayerJoinRateGauge.Set(metrics.PlayerJoinRate);
        PlayerLeaveRateGauge.Set(metrics.PlayerLeaveRate);

        StressDeadGauge.Set(metrics.StressDead);
        StressGhostGauge.Set(metrics.StressGhost);
        StressContainmentGauge.Set(metrics.StressContainment);
        StressEconomyGauge.Set(metrics.StressEconomy);
        StressDamageGauge.Set(metrics.StressDamage);
        StressAnomalyGauge.Set(metrics.StressAnomaly);
        StressMessGauge.Set(metrics.StressMess);
        StressAlertLevelGauge.Set(metrics.StressAlertLevel);

        StrengthArmedCrewGauge.Set(metrics.StrengthArmedCrew);
        StrengthSecurityGauge.Set(metrics.StrengthSecurity);
        StrengthCargoGauge.Set(metrics.StrengthCargo);
        StrengthTechnologyGauge.Set(metrics.StrengthTechnology);
        StrengthMaterialsGauge.Set(metrics.StrengthMaterials);

        StressPowerGauge.Set(metrics.StressPower);
        StressAtmosphereGauge.Set(metrics.StressAtmosphere);
        StressAntagonistGauge.Set(metrics.StressAntagonist);
        TotalPlayersGauge.Set(metrics.TotalPlayers);

        var alertQuery = EntityQueryEnumerator<AlertLevelComponent, MainStationComponent>();
        Entity<AlertLevelComponent>? mainAlert = null;
        while (alertQuery.MoveNext(out var station, out var alertComp, out _))
        {
            mainAlert = (station, alertComp);
            break;
        }

        if (mainAlert is { } alert &&
            _alertLevel.TryGetLevel(alert.AsNullable(), out var currentAlertLevel) &&
            currentAlertLevel is { } level)
        {
            AlertLevelGauge.Set(GetAlertLevelNumeric(level.Id));
        }
        else
        {
            AlertLevelGauge.Set(-1);
        }
    }

    private void LogStorytellerState(StorytellerRuleComponent comp, StorytellerPacingState? oldState)
    {
        if (!_cfg.GetCVar(SunriseCCVars.StorytellerTelemetryEnabled))
            return;

        var message = $"State changed: {oldState} -> {comp.PacingState}. Crew stress: {comp.CrewStress.ToString("F1", CultureInfo.InvariantCulture)}, Threat budget: {comp.ThreatBudget.ToString("F1", CultureInfo.InvariantCulture)}, Major budget: {comp.MajorThreatBudget.ToString("F1", CultureInfo.InvariantCulture)}";
        _sawmill.Debug(message);
    }

    private void LogTelemetryTick(StorytellerRuleComponent comp, StationMetrics metrics, bool debug = true)
    {
        if (!_cfg.GetCVar(SunriseCCVars.StorytellerTelemetryEnabled))
            return;

        var inv = CultureInfo.InvariantCulture;
        static string F1(float v, IFormatProvider p) => v.ToString("F1", p);
        static string F2(float v, IFormatProvider p) => v.ToString("F2", p);
        static string F4(float v, IFormatProvider p) => v.ToString("F4", p);

        var maxBudgetModifier = 1f;
        if (ProtoMan.TryIndex<StorytellerTypePrototype>(comp.StorytellerType.ToString(), out var typeProto))
        {
            maxBudgetModifier = typeProto.MaxBudgetModifier;
        }
        var maxBudget = comp.MaxThreatBudget * maxBudgetModifier;

        var alertLevel = "green";
        var alertQuery = EntityQueryEnumerator<AlertLevelComponent, MainStationComponent>();
        Entity<AlertLevelComponent>? mainAlert = null;
        while (alertQuery.MoveNext(out var station, out var alertComp, out _))
        {
            mainAlert = (station, alertComp);
            break;
        }

        if (mainAlert is { } alert &&
            _alertLevel.TryGetLevel(alert.AsNullable(), out var currentAlertLevel) &&
            currentAlertLevel is { } level)
        {
            alertLevel = level.Id;
        }

        var message =
            $"Tick - State: {comp.PacingState}, Type: {comp.StorytellerType}, Alert: {alertLevel}, " +
            $"Crew Stress: {F2(comp.CrewStress, inv)}, Threat Budget: {F2(comp.ThreatBudget, inv)}/{F1(maxBudget, inv)}, " +
            $"Major Budget: {F2(comp.MajorThreatBudget, inv)}/{F1(maxBudget, inv)}, " +
            $"Players: {metrics.AliveCount}/{metrics.TotalPlayers} (Dead: {metrics.DeadCount}, Ghosts: {metrics.GhostCount}, Sec: {metrics.SecurityCount}), " +
            $"Roster: {metrics.CrewRosterCount} (Command/Crew: {metrics.RosterCommandCount}/{metrics.RosterCrewCount}, Dead Cmd/Crew: {metrics.DeadCommandCount}/{metrics.DeadCrewCount}), " +
            $"Join/Leave Rate: {F1(metrics.PlayerJoinRate, inv)}/{F1(metrics.PlayerLeaveRate, inv)}, " +
            $"Economy: Cargo {metrics.CargoBalance}, Science {metrics.SciencePoints}, " +
            $"Station Strength: {F1(metrics.StationStrength, inv)} " +
            $"(Armed {F1(metrics.StrengthArmedCrew, inv)}, Sec {F1(metrics.StrengthSecurity, inv)}, Cargo {F1(metrics.StrengthCargo, inv)}, Tech {F1(metrics.StrengthTechnology, inv)}, Mats {F1(metrics.StrengthMaterials, inv)}), " +
            $"Material Score: {F1(metrics.MaterialStrengthScore, inv)}, " +
            $"Stress: Dead {F2(metrics.StressDead, inv)}, Ghost {F2(metrics.StressGhost, inv)}, Antag {F2(metrics.StressAntagonist, inv)}, Contain {F2(metrics.StressContainment, inv)}, Econ {F2(metrics.StressEconomy, inv)}, " +
            $"Dmg {F2(metrics.StressDamage, inv)}, Anomaly {F2(metrics.StressAnomaly, inv)}, Mess {F2(metrics.StressMess, inv)}, Power {F2(metrics.StressPower, inv)}, Atmos {F2(metrics.StressAtmosphere, inv)}, " +
            $"StressAlertLevel: {F2(metrics.StressAlertLevel, inv)}, " +
            $"Atmos Unsafe: {F4(metrics.AtmosphereUnsafeRatio, inv)}, Power Deficit: {F4(metrics.PowerGridDeficitRatio, inv)}, Tiles: {metrics.TotalStationTiles}, " +
            $"Weapons: {metrics.CrewWeaponCount}, Antags: {metrics.ActiveAntagonistCount}, ERT: {metrics.ActiveErtCount}, " +
            $"Singularity: {metrics.SingularityActive}/{metrics.SingularityContained}, Tesla: {metrics.TeslaActive}/{metrics.TeslaContained}, " +
            $"Research: {metrics.UnlockedTechnologyCount}/{metrics.TotalTechnologyCount} techs " +
            $"(score {F1(metrics.ResearchStorytellerScore, inv)}/{F1(metrics.MaxResearchStorytellerScore, inv)}), Ghost Roles: {metrics.AvailableGhostRoles}, " +
            $"Anomalies: {metrics.AnomaliesCount}, Artifacts: {metrics.ActiveArtifactsCount}, " +
            $"Mess: Puddles {metrics.PuddlesCount}, Footprints {metrics.FootprintsCount}, Trash {metrics.TrashCount}, Avg Damage {F2(metrics.AverageCrewDamage, inv)}";

        var crewDist = metrics.CrewDistribution
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}={kv.Value}");
        var distText = string.Join(", ", crewDist);
        if (!string.IsNullOrEmpty(distText))
            message += $", CrewDist: {distText}";

        if (debug)
            _sawmill.Debug(message);
        else
            _sawmill.Info(message);
    }

    private void RecordEventTriggered(string eventId, StorytellerMetadataPrototype metadata)
    {
        EventsTriggeredCounter.WithLabels(eventId, metadata.ThreatType.ToString()).Inc();

        if (!_cfg.GetCVar(SunriseCCVars.StorytellerTelemetryEnabled))
            return;

        _sawmill.Info($"Triggered event: {eventId} (Threat Type: {metadata.ThreatType}, Cost: {metadata.ThreatCost.ToString("F1", CultureInfo.InvariantCulture)})");
    }
}
