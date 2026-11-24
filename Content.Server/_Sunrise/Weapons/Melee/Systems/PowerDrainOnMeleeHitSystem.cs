using Content.Server.Power.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._Sunrise.Weapons.Melee.Components;
using Content.Shared.PowerCell;

namespace Content.Server._Sunrise.Weapons.Melee.Systems;

public sealed partial class PowerDrainOnMeleeHitSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private BatterySystem _battery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PowerDrainOnMeleeHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, PowerDrainOnMeleeHitComponent comp, ref MeleeHitEvent args)
    {
        if (comp.ChargePerHit <= 0)
            return;

        if (!args.IsHit)
            return;

        if (comp.RequireActualHit && (args.HitEntities == null || args.HitEntities.Count == 0))
            return;

        // Check if item is toggled on (if it has ItemToggleComponent)
        if (TryComp<ItemToggleComponent>(uid, out var toggle) && !_itemToggle.IsActivated((uid, toggle)))
            return;

        // Prefer slotted power cell if present
        if (HasComp<PowerCellSlotComponent>(uid))
        {
            if (!_powerCell.HasCharge(uid, comp.ChargePerHit, args.User))
            {
                args.Handled = true;
                return;
            }

            if (!_powerCell.TryUseCharge(uid, comp.ChargePerHit, args.User))
            {
                args.Handled = true;
                return;
            }
            return;
        }

        // Fall back to direct BatteryComponent on the same entity
        if (TryComp<BatteryComponent>(uid, out var directBattery))
        {
            if (_battery.GetCharge((uid, directBattery)) < comp.ChargePerHit)
            {
                args.Handled = true;
                return;
            }

            if (!_battery.TryUseCharge(uid, comp.ChargePerHit))
            {
                args.Handled = true;
                return;
            }
        }
    }
}
