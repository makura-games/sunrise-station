using Content.Server._Sunrise.Disease.Systems;
using Content.Server._Sunrise.StationEvents.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.GameTicking.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.StationEvents.Events;

/// <summary>
/// Портит партию еды на станции: несколько порций становятся источником заражения.
/// </summary>
/// <remarks>
/// Заражается уже существующая еда, а не подброшенная: вспышка выглядит как брак на кухне,
/// и отвечать за неё приходится повару.
/// </remarks>
public sealed partial class DiseaseFoodBatchRule : StationEventSystem<DiseaseFoodBatchRuleComponent>
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    protected override void Started(
        EntityUid uid,
        DiseaseFoodBatchRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Diseases.Count == 0 || !TryGetRandomStation(out var station))
            return;

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<EdibleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out _, out var xform))
        {
            // Только то, что лежит на самой станции: еда на шаттлах и в руинах нас не касается.
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            // Уже заражённое второй раз не портим.
            if (HasComp<DiseaseSourceComponent>(ent))
                continue;

            candidates.Add(ent);
        }

        if (candidates.Count == 0)
            return;

        // Штамм заводится один на всю партию и раздаётся порциям готовой сущностью:
        // по шаблону каждая порция вытянула бы болезнь раунда, общую со всеми прочими вспышками.
        var template = RobustRandom.Pick(component.Diseases);
        if (_registry.CreateStrain(template) is not { } disease)
            return;

        var count = Math.Min(RobustRandom.Next(component.MinItems, component.MaxItems + 1), candidates.Count);

        RobustRandom.Shuffle(candidates);
        for (var i = 0; i < count; i++)
        {
            var source = EnsureComp<DiseaseSourceComponent>(candidates[i]);
            source.Templates = new List<ProtoId<DiseasePrototype>>();
            source.Diseases = new List<EntityUid> { disease };
            source.Routes = DiseaseTransmissionRoute.Ingestion;
            source.IngestionChance = component.IngestionChance;
            source.ContactChance = 0f;
        }

        Sawmill.Info($"Contaminated {count} food items with {ToPrettyString(disease)}");
    }
}
