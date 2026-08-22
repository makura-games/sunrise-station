using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;

/// <summary>
///     Blocks equip and unequip actions.
/// </summary>
public sealed partial class TutorialSoftLockSystem
{
    public void InitializeEquip()
    {
        SubscribeLocalEvent<TutorialEquipSoftLockComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<TutorialEquipSoftLockComponent, IsEquippingTargetAttemptEvent>(OnEquipTargetAttempt);

        SubscribeLocalEvent<TutorialEquipBlockedSoftLockComponent, IsEquippingAttemptEvent>(OnBlockedEquipAttempt);
        SubscribeLocalEvent<TutorialEquipBlockedSoftLockComponent, IsEquippingTargetAttemptEvent>(OnBlockedEquipTargetAttempt);

        SubscribeLocalEvent<TutorialUnequipSoftLockComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<TutorialUnequipSoftLockComponent, IsUnequippingTargetAttemptEvent>(OnUnequipTargetAttempt);
    }

    private void OnEquipAttempt(Entity<TutorialEquipSoftLockComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelEquip(ent, args);
    }

    private void OnEquipTargetAttempt(Entity<TutorialEquipSoftLockComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelEquip(ent, args);
    }

    private void OnBlockedEquipAttempt(Entity<TutorialEquipBlockedSoftLockComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelBlockedEquip(ent, args);
    }

    private void OnBlockedEquipTargetAttempt(Entity<TutorialEquipBlockedSoftLockComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelBlockedEquip(ent, args);
    }

    private void TryCancelEquip(Entity<TutorialEquipSoftLockComponent> ent, EquipAttemptBase args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.Cancelled)
            return;

        if ((args.SlotFlags & ent.Comp.Slot) != 0 && IsAllowedPrototype(args.Equipment, ent.Comp.Item))
            return;

        args.Reason = ent.Comp.Popup;
        args.Cancel();
        ShowPopup(args.EquipTarget, ent.Comp.Popup);
    }

    private void TryCancelBlockedEquip(Entity<TutorialEquipBlockedSoftLockComponent> ent, EquipAttemptBase args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.BlockAllSlots && (args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (!HasBlockedPrototype(args.Equipment, ent.Comp.Items))
            return;

        args.Reason = ent.Comp.Popup;
        args.Cancel();
        ShowPopup(args.EquipTarget, ent.Comp.Popup);
    }

    private void OnUnequipAttempt(Entity<TutorialUnequipSoftLockComponent> ent, ref IsUnequippingAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelUnequip(ent, args);
    }

    private void OnUnequipTargetAttempt(Entity<TutorialUnequipSoftLockComponent> ent, ref IsUnequippingTargetAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        TryCancelUnequip(ent, args);
    }

    private void OnHandEquipped(Entity<TutorialSoftLockTrackerComponent> ent, ref DidEquipHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (ShouldMarkEntity(ent.Owner, args.Equipped))
            TryMarkEntity(ent.Owner, args.Equipped, ent.Comp);
    }

    private bool TryCancelUnequip(Entity<TutorialUnequipSoftLockComponent> ent, UnequipAttemptEventBase args)
    {
        if (args.Cancelled || (args.SlotFlags & ent.Comp.Slots) == 0)
            return false;

        if (ent.Comp.Items.Count > 0 && !HasBlockedPrototype(args.Equipment, ent.Comp.Items))
            return false;

        args.Reason = ent.Comp.Popup;
        args.Cancel();
        return true;
    }
}
