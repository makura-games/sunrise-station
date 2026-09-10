using Content.Shared._Sunrise.Objectives;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

public sealed partial class ItemHandledObjectiveConditionSystem
    : ObjectiveConditionSystem<HandsComponent, ItemHandledObjectiveCondition>
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    protected override void Condition(Entity<HandsComponent> entity, ref ObjectiveConditionEvaluateEvent<ItemHandledObjectiveCondition> args)
    {
        foreach (var held in _hands.EnumerateHeld(entity.Owner))
        {
            if (!HasPrototype(held, args.Condition.Item))
                continue;

            args.Satisfied = true;
            return;
        }

        if (!TryComp<InventoryComponent>(entity, out var inventory))
            return;

        if (args.Condition.Slot != null && ContainsSlotItem(entity, inventory, args.Condition.Slot.Value, args.Condition.Item))
        {
            args.Satisfied = true;
            return;
        }

        if (args.Condition.Storage != null && ContainsStorageItem(entity, inventory, args.Condition.Storage.Value, args.Condition.Item))
            args.Satisfied = true;
    }

    private bool ContainsSlotItem(
        EntityUid entity,
        InventoryComponent inventory,
        SlotFlags slotFlags,
        EntProtoId item)
    {
        foreach (var slot in inventory.Slots)
        {
            if ((slot.SlotFlags & slotFlags) == 0)
                continue;

            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var slotItem, inventory))
                continue;

            if (HasPrototype(slotItem.Value, item))
                return true;
        }

        return false;
    }

    private bool ContainsStorageItem(
        EntityUid entity,
        InventoryComponent inventory,
        EntProtoId storagePrototype,
        EntProtoId item)
    {
        foreach (var slot in inventory.Slots)
        {
            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var storageEntity, inventory))
                continue;

            if (!HasPrototype(storageEntity.Value, storagePrototype))
                continue;

            if (!TryComp<StorageComponent>(storageEntity.Value, out var storage))
                continue;

            foreach (var contained in storage.Container.ContainedEntities)
            {
                if (HasPrototype(contained, item))
                    return true;
            }
        }

        return false;
    }

    private bool HasPrototype(EntityUid uid, EntProtoId prototype)
    {
        var entityPrototype = Prototype(uid);
        return entityPrototype != null && entityPrototype.ID == prototype;
    }
}

public sealed partial class ItemHandledObjectiveCondition : ObjectiveConditionBase<ItemHandledObjectiveCondition>
{
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public SlotFlags? Slot;

    [DataField]
    public EntProtoId? Storage;
}
