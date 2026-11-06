using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

public sealed partial class StorageContainsConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, StorageContainsCondition>
{
    [Dependency] private InventorySystem _inventory = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<StorageContainsCondition> args)
    {
        if (!TryComp<InventoryComponent>(entity, out var inventory))
            return;

        foreach (var slot in inventory.Slots)
        {
            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var storageEntity, inventory))
                continue;

            if (!HasPrototype(storageEntity.Value, args.Condition.Storage))
                continue;

            if (!TryComp<StorageComponent>(storageEntity.Value, out var storage))
                continue;

            if (args.Condition.Item == null)
            {
                args.Result = storage.Container.ContainedEntities.Count > 0;
                return;
            }

            foreach (var contained in storage.Container.ContainedEntities)
            {
                if (!HasPrototype(contained, args.Condition.Item.Value))
                    continue;

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

public sealed partial class StorageContainsCondition : TutorialConditionBase<StorageContainsCondition>
{
    [DataField(required: true)]
    public EntProtoId Storage;

    [DataField]
    public EntProtoId? Item;
}
