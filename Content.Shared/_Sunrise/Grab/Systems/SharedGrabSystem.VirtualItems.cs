using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Grab.Components;

namespace Content.Shared._Sunrise.Grab.Systems;

public sealed partial class SharedGrabSystem
{
    private void OnVirtualItemDeleted(Entity<GrabberComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (args.Handled || ent.Comp.Grabbed != args.BlockingEntity)
            return;

        if (ent.Comp.DeletingVirtualItems.Contains(args.VirtualItem))
        {
            args.Handled = true;
            return;
        }

        if (!ent.Comp.VirtualItems.Remove(args.VirtualItem))
            return;

        args.Handled = true;
        Dirty(ent);

        if (ent.Comp.Grabbed is { } grabbed)
            TryLowerGrabStage(ent.Owner, grabbed, GrabStageChangeCause.Relax);
    }

    private bool TryUpdateGrabVirtualItems(Entity<GrabberComponent> grabber, EntityUid grabbed, GrabStage stage)
    {
        PruneDeletingVirtualItems(grabber);

        var required = 0;
        if (grabber.Comp.VirtualItemStageCount.TryGetValue(stage, out var count))
            required += count;

        while (grabber.Comp.VirtualItems.Count < required)
        {
            if (!TrySpawnGrabVirtualItemInHand(grabbed, grabber.Owner, out var item))
            {
                PopupGrabActor(Loc.GetString("popup-grab-need-hand"), grabber.Owner, grabber.Owner, PopupType.MediumCaution);
                return false;
            }

            grabber.Comp.VirtualItems.Add(item.Value);
        }

        TrimGrabVirtualItems(grabber, required);
        Dirty(grabber);
        return true;
    }

    private void CleanupGrabVirtualItems(Entity<GrabberComponent> grabber)
    {
        PruneDeletingVirtualItems(grabber);
        TrimGrabVirtualItems(grabber, 0);
        grabber.Comp.VirtualItems.Clear();
        grabber.Comp.DeletingVirtualItems.Clear();
        Dirty(grabber);
    }

    private void TrimGrabVirtualItems(Entity<GrabberComponent> grabber, int required)
    {
        for (var i = grabber.Comp.VirtualItems.Count - 1; i >= required; i--)
        {
            var item = grabber.Comp.VirtualItems[i];
            grabber.Comp.VirtualItems.RemoveAt(i);

            if (!_virtualQuery.TryComp(item, out var virtualItem))
                continue;

            grabber.Comp.DeletingVirtualItems.Add(item);
            _virtual.DeleteVirtualItem((item, virtualItem), grabber.Owner);
        }
    }

    private void PruneDeletingVirtualItems(Entity<GrabberComponent> grabber)
    {
        grabber.Comp.DeletingVirtualItems.RemoveWhere(uid => TerminatingOrDeleted(uid) || !_virtualQuery.HasComp(uid));
    }

    private bool TrySpawnGrabVirtualItemInHand(
        EntityUid blockingEnt,
        EntityUid user,
        [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;

        if (_hands.TryGetEmptyHand(user, out var emptyHand))
            return _virtual.TrySpawnVirtualItemInHand(blockingEnt, user, out item, empty: emptyHand, silent: true);

        foreach (var hand in _hands.EnumerateHands((user, null)))
        {
            if (!_hands.TryGetHeldItem((user, null), hand, out var held))
                continue;

            if (_virtualQuery.HasComp(held.Value))
                continue;

            if (!_hands.TryDrop((user, null), hand))
                continue;

            return _virtual.TrySpawnVirtualItemInHand(blockingEnt, user, out item, empty: hand, silent: true);
        }

        return false;
    }
}
