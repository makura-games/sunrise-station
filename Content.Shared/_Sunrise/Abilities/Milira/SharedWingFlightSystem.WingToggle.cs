using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Abilities.Milira;

/// <summary>
/// Shared система WingFlight с блокировкой одевания брони при раскрытых крыльях
/// </summary>
public sealed partial class WingToggleSharedSystem : SharedWingFlightSystem
{
    [Dependency] private TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WingToggleComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<WingToggleComponent, IsEquippingTargetAttemptEvent>(OnEquipTargetAttempt);

        SubscribeLocalEvent<WingToggleComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private bool ShouldCancelEquip(Entity<WingToggleComponent> ent, string slot, EntityUid equipment)
    {
        if (!ent.Comp.WingsOpened)
            return false;

        if (ent.Comp.BlockedSlots != null && ent.Comp.BlockedSlots.Contains(slot))
        {
            if (ent.Comp.AllowedTag != null && _tagSystem.HasTag(equipment, ent.Comp.AllowedTag.Value))
                return false;

            return true;
        }

        return false;
    }

    private void OnEquipAttempt(Entity<WingToggleComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (ShouldCancelEquip(ent, args.Slot, args.Equipment))
            args.Cancel();
    }

    private void OnEquipTargetAttempt(Entity<WingToggleComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (ShouldCancelEquip(ent, args.Slot, args.Equipment))
            args.Cancel();
    }

    public void CloseWing(Entity<WingToggleComponent> ent)
    {
        if (!ent.Comp.WingsOpened)
            return;

        var ev = new WingForceClose();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnMobStateChanged(Entity<WingToggleComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            CloseWing(ent);
    }
}

[ByRefEvent]
public readonly record struct WingForceClose;
