using Content.Shared._Sunrise.Objectives;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

public sealed partial class InventorySlotContainsObjectiveConditionSystem
    : ObjectiveConditionSystem<InventoryComponent, InventorySlotContainsObjectiveCondition>
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    protected override void Condition(Entity<InventoryComponent> entity, ref ObjectiveConditionEvaluateEvent<InventorySlotContainsObjectiveCondition> args)
    {
        foreach (var slot in entity.Comp.Slots)
        {
            if ((slot.SlotFlags & args.Condition.Slot) == 0)
                continue;

            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var item, entity.Comp))
                continue;

            if (args.Condition.Item == null || HasPrototype(item.Value, args.Condition.Item.Value))
            {
                args.Satisfied = true;
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

public sealed partial class InventorySlotContainsObjectiveCondition : ObjectiveConditionBase<InventorySlotContainsObjectiveCondition>
{
    [DataField(required: true)]
    public SlotFlags Slot;

    [DataField]
    public EntProtoId? Item;
}
