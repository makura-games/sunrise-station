using Content.Shared.Electrocution;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Sunrise.Electrocution;

public sealed partial class InsulatedGunSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InsulatedComponent, InventoryRelayedEvent<ShotAttemptedEvent>>(OnShootAttempted);
    }

    private void OnShootAttempted(
        Entity<InsulatedComponent> ent,
        ref InventoryRelayedEvent<ShotAttemptedEvent> args)
    {
        if (!ent.Comp.PreventOperatingGuns || args.Args.Used.Comp.BigTrigger)
            return;

        _popup.PopupPredicted(Loc.GetString("gun-Insulated-gloves"), args.Args.User, args.Args.User);
        args.Args.Cancel();
    }
}
