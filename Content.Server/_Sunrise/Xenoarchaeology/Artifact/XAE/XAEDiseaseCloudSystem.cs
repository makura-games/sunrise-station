using Content.Server._Sunrise.Xenoarchaeology.Artifact.XAE.Components;
using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// Эффект артефакта: выброс облаков заразного аэрозоля вокруг узла.
/// </summary>
public sealed partial class XAEDiseaseCloudSystem : BaseXAESystem<XAEDiseaseCloudComponent>
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transform = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<XAEDiseaseCloudComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SelectedDisease != null || ent.Comp.Diseases.Count == 0)
            return;

        // Штамм закрепляется за узлом на весь раунд: один артефакт — одна вспышка.
        ent.Comp.SelectedDisease = _random.Pick(ent.Comp.Diseases);
    }

    /// <inheritdoc />
    protected override void OnActivated(Entity<XAEDiseaseCloudComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        // Штамм заводится при первом срабатывании и дальше живёт с узлом: все облака одного
        // артефакта несут одну болезнь, а соседний артефакт даёт уже другую.
        if (ent.Comp.SelectedDisease is not { } template)
            return;

        if (ent.Comp.Strain is not { } disease || !_registry.StrainExists(disease))
        {
            if (_registry.CreateStrain(template) is not { } created)
                return;

            disease = created;
            ent.Comp.Strain = created;
        }

        for (var i = 0; i < ent.Comp.Clouds; i++)
        {
            // Облака расходятся от артефакта, а не висят одной точкой: иначе достаточно
            // отойти на шаг, и выброс уже никого не задевает.
            var offset = _random.NextVector2(ent.Comp.SpreadRange);
            var coords = args.Coordinates.Offset(offset);

            var cloud = Spawn(ent.Comp.CloudPrototype, coords);
            var cloudComp = EnsureComp<DiseaseCloudComponent>(cloud);
            cloudComp.Disease = disease;
        }
    }
}
