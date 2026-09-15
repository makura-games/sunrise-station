using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared.Trigger.Systems;

public sealed partial class GibOnTriggerSystem : XOnTriggerSystem<GibOnTriggerComponent>
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private InventorySystem _inventory = default!;

    protected override void OnTrigger(Entity<GibOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (ent.Comp.DeleteItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(target);
            foreach (var item in items)
            {
                PredictedQueueDel(item);
            }
        }

        // Sunrise edit start - support gear acidifier without deleting the body
        if (ent.Comp.GibBody)
            _gibbing.Gib(target, dropGiblets: ent.Comp.GibOrgans, user: args.User);
        // Sunrise edit end

        args.Handled = true;
    }
}
