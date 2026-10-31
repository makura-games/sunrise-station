using Content.Shared._Sunrise.Bed;
using Content.Shared.Inventory;

namespace Content.Shared.Bed;

public sealed partial class BedSystem
{
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private EntityQuery<BedHealModifierClothingComponent> _bedHealModifierClothingQuery = default!;

    private float GetSunriseHealingMultiplier(EntityUid healedEntity)
    {
        if (!TryComp<InventoryComponent>(healedEntity, out var inventory))
            return 1f;

        var multiplier = 1f;
        var enumerator = _inventorySystem.GetSlotEnumerator((healedEntity, inventory), SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            if (!_bedHealModifierClothingQuery.TryComp(item, out var modifier))
                continue;

            multiplier = Math.Min(multiplier, modifier.Multiplier);
        }

        return multiplier;
    }
}
