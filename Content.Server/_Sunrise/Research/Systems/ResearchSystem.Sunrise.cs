using Content.Server._Sunrise.Research.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared._Sunrise.Research.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Research.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

/// <summary>
/// Модификатор получения РНД очков исходя от количества игроков.
/// </summary>
public sealed partial class ResearchSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private const int PopulationDeadzone = 4;
    private static readonly ProtoId<ResearchPopulationWeightsPrototype> PopulationWeightsPrototypeId = "SunriseResearchPopulationWeights";

    private int _populationScalingDelayMinutes = SunriseCCVars.ResearchPointScalingDelayMinutes.DefaultValue;
    private int _targetPopulation = SunriseCCVars.ResearchPointScalingTargetPopulation.DefaultValue;
    private float _minimumPopulationModifier = SunriseCCVars.ResearchPointScalingMinModifier.DefaultValue;
    private float _maximumPopulationModifier = SunriseCCVars.ResearchPointScalingMaxModifier.DefaultValue;
    private float _populationScalingMultiplier = SunriseCCVars.ResearchPointScalingMultiplier.DefaultValue;
    private float? _roundPopulationModifier;
    private TimeSpan? _populationScalingRoundStartedAt;
    private TimeSpan? _populationModifierCalculateAt;
    [Dependency] private EntityQuery<GhostComponent> _ghostQuery = default!;

    private void InitializePopulationScaling()
    {
        _cfg.OnValueChanged(SunriseCCVars.ResearchPointScalingDelayMinutes, OnPopulationScalingDelayChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ResearchPointScalingTargetPopulation, value => _targetPopulation = value, true);
        _cfg.OnValueChanged(SunriseCCVars.ResearchPointScalingMinModifier, value => _minimumPopulationModifier = value, true);
        _cfg.OnValueChanged(SunriseCCVars.ResearchPointScalingMaxModifier, value => _maximumPopulationModifier = value, true);
        _cfg.OnValueChanged(SunriseCCVars.ResearchPointScalingMultiplier, value => _populationScalingMultiplier = value, true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        ResetPopulationModifierState();
    }

    private void UpdatePopulationScaling()
    {
        if (_roundPopulationModifier != null)
            return;

        if (_populationModifierCalculateAt is not { } calculateAt || _timing.CurTime < calculateAt)
            return;

        _roundPopulationModifier = CalculatePopulationModifier(CountResearchPopulation());
        _populationModifierCalculateAt = null;
    }

    private void OnPopulationScalingDelayChanged(int value)
    {
        _populationScalingDelayMinutes = value;
        UpdatePopulationModifierCalculateAt();
    }

    public void ModifyServerResearchPoints(EntityUid uid, int points, ResearchServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (points > 0)
        {
            var modifier = _roundPopulationModifier ?? 1f;

            if (modifier != 1f)
                points = (int) MathF.Round(points * modifier, MidpointRounding.AwayFromZero);
        }

        ModifyServerPoints(uid, points, component);
    }

    private void OnRoundStarted(RoundStartedEvent _)
    {
        ResetPopulationModifierState();
        _populationScalingRoundStartedAt = _timing.CurTime;
        UpdatePopulationModifierCalculateAt();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        ResetPopulationModifierState();
    }

    private void ResetPopulationModifierState()
    {
        _roundPopulationModifier = null;
        _populationScalingRoundStartedAt = null;
        _populationModifierCalculateAt = null;
    }

    private float CalculatePopulationModifier(float population)
    {
        if (_populationScalingMultiplier <= 0f || _targetPopulation <= 0)
            return 1f;

        if (MathF.Abs(population - _targetPopulation) <= PopulationDeadzone)
            return 1f;

        var ratio = _targetPopulation / MathF.Max(population, 1f);
        var baseModifier = MathF.Sqrt(ratio);
        var modifier = 1f + (baseModifier - 1f) * _populationScalingMultiplier;
        var minimumModifier = MathF.Min(_minimumPopulationModifier, _maximumPopulationModifier);
        var maximumModifier = MathF.Max(_minimumPopulationModifier, _maximumPopulationModifier);

        return Math.Clamp(modifier, minimumModifier, maximumModifier);
    }

    private float CountResearchPopulation()
    {
        var population = 0f;

        foreach (var session in _player.Sessions)
        {
            population += GetResearchPopulationContribution(session);
        }

        return population;
    }


    private void UpdatePopulationModifierCalculateAt()
    {
        if (_roundPopulationModifier != null || _populationScalingRoundStartedAt is not { } roundStartedAt)
            return;

        _populationModifierCalculateAt = roundStartedAt + TimeSpan.FromMinutes(Math.Max(_populationScalingDelayMinutes, 0));


    }

    // Общая проверка в игре ли персонаж
    private float GetResearchPopulationContribution(ICommonSession session)
    {
        // Считаем даже тех кто в лобби
        if (session.Status == SessionStatus.Connected)
            return 0.4f;

        // Чтобы не считать игроков которые "подключаются"
        if (session.Status != SessionStatus.InGame)
            return 0f;

        if (session.AttachedEntity is not { Valid: true } uid || Deleted(uid))
            return 0f;

        if (_ghostQuery.HasComp(uid))
            return 0.4f;

        return GetResearchPopulationContribution(uid);
    }

    // Проверка на влияние
    private float GetResearchPopulationContribution(EntityUid uid)
    {
        if (TryComp<ResearchPopulationComponent>(uid, out var researchPopulation))
            return researchPopulation.Weight;

        if (!_mind.TryGetMind(uid, out var mindId, out _) || !_jobs.MindTryGetJobId(mindId, out var jobId))
            return 0.4f;

        return GetResearchPopulationWeight(jobId);
    }


    private float GetResearchPopulationWeight(ProtoId<JobPrototype>? jobId)
    {
        if (jobId == null)
            return 0.4f;

        if (!_prototype.TryIndex(PopulationWeightsPrototypeId, out var prototype))
            return 0.4f;

        return prototype.Weights.GetValueOrDefault(jobId.Value, 0.4f);
    }
}
