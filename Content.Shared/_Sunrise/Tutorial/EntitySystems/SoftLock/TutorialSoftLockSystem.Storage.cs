using System;
using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Item;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;

/// <summary>
///     Blocks storage actions.
/// </summary>
public sealed partial class TutorialSoftLockSystem
{
    public void InitializeStorage()
    {
        SubscribeLocalEvent<TutorialSoftLockEntityComponent, StorageCloseAttemptEvent>(OnStorageCloseAttempt);
        SubscribeLocalEvent<TutorialSoftLockEntityComponent, StorageInsertFailedEvent>(OnStorageInsertFailed);
        SubscribeLocalEvent<TutorialSoftLockEntityComponent, InteractionAttemptEvent>(OnHeldItemStorageInteractionAttempt);
        SubscribeLocalEvent<TutorialSoftLockEntityComponent, ContainerGettingInsertedAttemptEvent>(OnContainerGettingInsertedAttempt);

        SubscribeLocalEvent<TutorialStorageCloseSoftLockComponent, TutorialShouldMarkEntityEvent>(OnStorageCloseShouldMarkEntity);
        SubscribeLocalEvent<TutorialStorageInsertSoftLockComponent, TutorialShouldMarkEntityEvent>(OnStorageInsertShouldMarkEntity);
        SubscribeLocalEvent<TutorialStorageInteractSoftLockComponent, TutorialShouldMarkEntityEvent>(OnStorageInteractShouldMarkEntity);
    }

    private void OnStorageCloseAttempt(Entity<TutorialSoftLockEntityComponent> ent, ref StorageCloseAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        if (!ent.Comp.Players.Contains(user))
            return;

        if (!TryComp<TutorialStorageCloseSoftLockComponent>(user, out var softLock))
            return;

        if (!IsAllowedPrototype(ent, softLock.Targets))
            return;

        args.Cancelled = true;
        ShowPopup(user, softLock.Popup);
    }

    private void OnStorageInsertFailed(Entity<TutorialSoftLockEntityComponent> ent, ref StorageInsertFailedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!ent.Comp.Players.Contains(args.Player) ||
            !TryComp<TutorialStorageInsertSoftLockComponent>(args.Player, out var softLock))
            return;

        if (!_hands.TryGetActiveItem(args.Player, out var activeItem))
            return;

        if (!ShouldBlockStorageInsert(softLock, ent, activeItem.Value))
            return;

        ShowPopup(args.Player, softLock.Popup);
    }

    private void OnHeldItemStorageInteractionAttempt(Entity<TutorialSoftLockEntityComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!ent.Comp.Players.Contains(args.Uid))
            return;

        if (!TryComp<TutorialStorageInteractSoftLockComponent>(args.Uid, out var softLock))
            return;

        if (!ShouldBlockStorageInteract(softLock, target, ent.Owner))
            return;

        args.Cancelled = true;
        ShowPopup(args.Uid, softLock.Popup);
    }

    private void OnContainerGettingInsertedAttempt(
        Entity<TutorialSoftLockEntityComponent> ent, ref
        ContainerGettingInsertedAttemptEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.Cancelled)
            return;

        foreach (var player in ent.Comp.Players)
        {
            if (!TryComp<TutorialStorageInsertSoftLockComponent>(player, out var softLock))
                continue;

            if (!TryComp<HandsComponent>(player, out var hands))
                continue;

            if (!_hands.IsHolding((player, hands), args.EntityUid))
                continue;

            if (!ShouldBlockStorageInsert(softLock, args.Container.Owner, args.EntityUid))
                continue;

            args.Cancel();
            ShowPopup(player, softLock.Popup);
            return;
        }
    }

    private void OnStorageCloseShouldMarkEntity(
        Entity<TutorialStorageCloseSoftLockComponent> ent,
        ref TutorialShouldMarkEntityEvent args)
    {
        if (args.ShouldMark)
            return;

        if (!HasComp<EntityStorageComponent>(args.Target))
            return;

        args.ShouldMark = IsAllowedPrototype(args.Target, ent.Comp.Targets);
    }

    private void OnStorageInsertShouldMarkEntity(
        Entity<TutorialStorageInsertSoftLockComponent> ent,
        ref TutorialShouldMarkEntityEvent args)
    {
        if (args.ShouldMark)
            return;

        args.ShouldMark =
            (HasComp<StorageComponent>(args.Target) && IsStorageTargetAllowed(args.Target, ent.Comp)) ||
            IsStorageSourceAllowed(args.Target, ent.Comp.Items, ent.Comp.Targets);
    }

    private void OnStorageInteractShouldMarkEntity(
        Entity<TutorialStorageInteractSoftLockComponent> ent,
        ref TutorialShouldMarkEntityEvent args)
    {
        if (args.ShouldMark)
            return;

        args.ShouldMark = IsStorageSourceAllowed(args.Target, ent.Comp.Items, ent.Comp.Targets);
    }

    private bool IsStorageTargetAllowed(EntityUid target, TutorialStorageInsertSoftLockComponent softLock)
    {
        if (softLock.Items.Count == 0 && softLock.Targets.Count == 0)
            return false;

        return softLock.Targets.Count == 0 || HasBlockedPrototype(target, softLock.Targets);
    }

    private bool IsStorageSourceAllowed(EntityUid target, List<EntProtoId> items, List<EntProtoId> targets)
    {
        if (items.Count == 0)
            return targets.Count > 0;

        return HasBlockedPrototype(target, items);
    }

    private bool ShouldBlockStorageInsert(TutorialStorageInsertSoftLockComponent component, EntityUid storage, EntityUid item)
    {
        return ShouldBlockStorage(component.Items, component.Targets, storage, item);
    }

    private bool ShouldBlockStorageInteract(
        TutorialStorageInteractSoftLockComponent component,
        EntityUid storage,
        EntityUid item)
    {
        return ShouldBlockStorage(component.Items, component.Targets, storage, item);
    }

    private bool ShouldBlockStorage(
        List<EntProtoId> items,
        List<EntProtoId> targets,
        EntityUid storage,
        EntityUid item)
    {
        if (items.Count == 0 && targets.Count == 0)
            return false;

        if (items.Count > 0 && !HasBlockedPrototype(item, items))
            return false;

        if (targets.Count > 0 && !HasBlockedPrototype(storage, targets))
            return false;

        return true;
    }
}
