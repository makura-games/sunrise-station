using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Налёт заразы на вещах: что трогало очаг, то переносит заразу дальше.
/// </summary>
/// <remarks>
/// Работает не только с перчатками: заразу собирает любая вещь с <see cref="DiseaseResidueComponent"/>,
/// надетая или взятая в руки, — фартук, халат, тряпка, инструмент. Загрязнённая вещь становится
/// обычным очагом <see cref="DiseaseSourceComponent"/>: её видно в визоре вирусолога, она заражает
/// того, кто берёт её голыми руками, и очищается дезинфекцией, как любой предмет.
/// </remarks>
public sealed partial class DiseaseResidueSystem : EntitySystem
{
    [Dependency] private DiseaseSourceSystem _source = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityQuery<DiseaseResidueComponent> _residueQuery = default!;
    [Dependency] private EntityQuery<DiseaseSourceComponent> _sourceQuery = default!;

    /// <summary>
    /// Владелец коснулся кого-то: зараза с его вещей переходит на того, кого тронули.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnWearerContact(Entity<InventoryComponent> ent, ref ContactInteractionEvent args)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(ent.Owner))
        {
            if (item == args.Other || !_residueQuery.TryComp(item, out var residue) ||
                !_sourceQuery.TryComp(item, out var source))
            {
                continue;
            }

            _source.TryExpose((item, source), args.Other, DiseaseTransmissionRoute.Contact, residue.ContactChance);
        }
    }

    /// <summary>
    /// Оставляет болезнь на вещах того, кто коснулся очага или носителя.
    /// </summary>
    /// <param name="wearer">Тот, кто коснулся.</param>
    /// <param name="disease">Болезнь очага.</param>
    /// <param name="origin">Чего коснулись: сама себя вещь не загрязняет.</param>
    /// <returns>True, если зараза осела хотя бы на одной вещи.</returns>
    public bool TryContaminate(EntityUid wearer, EntityUid disease, EntityUid? origin = null)
    {
        var contaminated = false;
        foreach (var item in _inventory.GetHandOrInventoryEntities(wearer))
        {
            if (item == origin || !_residueQuery.TryComp(item, out var residue))
                continue;

            contaminated |= TryContaminateItem((item, residue), disease);
        }

        return contaminated;
    }

    /// <summary>
    /// Оставляет болезнь на одной вещи.
    /// </summary>
    /// <remarks>
    /// Налёт заражает только касанием: грязную вещь не едят, поэтому путь по умолчанию
    /// у очага здесь переписывается. Прежняя зараза на вещи сохраняется, новая добавляется к ней.
    /// </remarks>
    /// <returns>True, если вещь теперь несёт эту болезнь.</returns>
    public bool TryContaminateItem(Entity<DiseaseResidueComponent> ent, EntityUid disease)
    {
        if (ent.Comp.PickUpChance <= 0f || !_random.Prob(Math.Clamp(ent.Comp.PickUpChance, 0f, 1f)))
            return false;

        var source = EnsureComp<DiseaseSourceComponent>(ent);
        source.Templates.Clear();
        source.Routes = DiseaseTransmissionRoute.Contact;
        source.ContactChance = ent.Comp.ContactChance;
        source.IngestionChance = 0f;

        if (!source.Diseases.Contains(disease))
            source.Diseases.Add(disease);

        return true;
    }
}
