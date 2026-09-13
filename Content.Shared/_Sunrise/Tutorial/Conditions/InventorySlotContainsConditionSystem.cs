using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

public sealed partial class InventorySlotContainsConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, InventorySlotContainsCondition>
{
    [Dependency] private InventorySystem _inventory = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<InventorySlotContainsCondition> args)
    {
        if (!TryComp<InventoryComponent>(entity, out var inventory))
            return;

        foreach (var slot in inventory.Slots)
        {
            if ((slot.SlotFlags & args.Condition.Slot) == 0)
                continue;

            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var item, inventory))
                continue;

            if (args.Condition.Item == null || HasPrototype(item.Value, args.Condition.Item.Value))
            {
                args.Result = true;
                return;
            }
        }
    }

    private bool HasPrototype(EntityUid uid, EntProtoId prototype)
    {
        var entityPrototype = Prototype(uid);
        return entityPrototype != null && entityPrototype.ID == prototype;
    }
}

public sealed partial class InventorySlotContainsCondition : TutorialConditionBase<InventorySlotContainsCondition>
{
    [DataField(required: true)]
    public SlotFlags Slot;

    [DataField]
    public EntProtoId? Item;
}
