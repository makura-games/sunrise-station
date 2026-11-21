using Content.Server._Sunrise.Disease.Systems;
using Content.Server._Sunrise.StationEvents.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.StationEvents.Events;

/// <summary>
/// Подбрасывает на станцию заражённый предмет и заряжает его случайной болезнью.
/// </summary>
public sealed partial class DiseaseSourceSpawnRule : StationEventSystem<DiseaseSourceSpawnRuleComponent>
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    protected override void Started(
        EntityUid uid,
        DiseaseSourceSpawnRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Diseases.Count == 0 || !TryFindRandomTile(out _, out _, out _, out var coords))
            return;

        // У каждого подброшенного очага свой штамм, иначе вторая такая же вспышка
        // закрывалась бы вакциной от первой.
        var template = RobustRandom.Pick(component.Diseases);
        if (_registry.CreateStrain(template) is not { } disease)
            return;

        var source = Spawn(component.Prototype, coords);

        // Болезнь ставится на уже созданный предмет: так один прототип ящика годится
        // для любого штамма, и подкидывать отдельный ящик под каждую болезнь не нужно.
        var sourceComp = EnsureComp<DiseaseSourceComponent>(source);
        sourceComp.Templates = new List<ProtoId<DiseasePrototype>>();
        sourceComp.Diseases = new List<EntityUid> { disease };
        sourceComp.Routes = component.Routes;
        sourceComp.ContactChance = component.ContactChance;
        sourceComp.IngestionChance = component.IngestionChance;

        Sawmill.Info($"Spawned disease source {component.Prototype} with {disease} at {coords}");
    }
}
