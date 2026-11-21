using Content.Server._Sunrise.Disease.Systems;
using Content.Server._Sunrise.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.StationEvents.Events;

/// <summary>
/// Заводит вспышку: выбирает болезнь и заражает нескольких живых членов экипажа на станции.
/// </summary>
public sealed partial class DiseaseOutbreakRule : StationEventSystem<DiseaseOutbreakRuleComponent>
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    protected override void Started(
        EntityUid uid,
        DiseaseOutbreakRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Diseases.Count == 0 || !TryGetRandomStation(out var station))
            return;

        // Болезнь одна на всю вспышку: иначе медотдел получит несколько разных штаммов разом.
        // Но у каждой вспышки она своя, поэтому вторая вспышка того же прототипа
        // не закрывается вакциной от первой.
        var template = RobustRandom.Pick(component.Diseases);
        if (_registry.CreateStrain(template) is not { } disease)
            return;

        var count = InfectCrew(station.Value, disease, component.MinInfected, component.MaxInfected);
        Sawmill.Info($"Disease outbreak started: {ToPrettyString(disease)} infected {count} crew members");
    }

    /// <summary>
    /// Заражает случайных живых членов экипажа станции готовым штаммом.
    /// </summary>
    /// <returns>Сколько человек заразилось.</returns>
    public int InfectCrew(EntityUid station, EntityUid disease, int minInfected, int maxInfected)
    {
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<MindContainerComponent, HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out _, out _, out var mobState, out var xform))
        {
            // Только живые и только на этой станции: мертвеца и улетевшего заражать бессмысленно.
            if (!_mobState.IsAlive(ent, mobState))
                continue;

            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            candidates.Add(ent);
        }

        if (candidates.Count == 0)
            return 0;

        var count = Math.Min(RobustRandom.Next(minInfected, maxInfected + 1), candidates.Count);

        RobustRandom.Shuffle(candidates);
        var infected = 0;
        for (var i = 0; i < count; i++)
        {
            if (_disease.Infect(candidates[i], disease))
                infected++;
        }

        return infected;
    }
}
