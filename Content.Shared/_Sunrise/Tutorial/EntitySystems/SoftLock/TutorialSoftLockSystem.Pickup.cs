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
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Item;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;

/// <summary>
///     Blocks pickup and drop actions.
/// </summary>
public sealed partial class TutorialSoftLockSystem
{
    public void InitializePickup()
    {
        SubscribeLocalEvent<TutorialDropSoftLockComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<TutorialDropSoftLockComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<TutorialPickupSoftLockComponent, PickupAttemptEvent>(OnPickupAttempt);

    }

    private void OnDropAttempt(Entity<TutorialDropSoftLockComponent> ent, ref DropAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
        ShowPopup(ent, ent.Comp.Popup);
    }

    private void OnBeforeThrow(Entity<TutorialDropSoftLockComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        ShowPopup(ent, ent.Comp.Popup);
    }

    private void OnPickupAttempt(Entity<TutorialPickupSoftLockComponent> ent, ref PickupAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.BlockAll && IsAllowedPrototype(args.Item, ent.Comp.AllowedItems))
            return;

        args.Cancel();
        if (args.ShowPopup)
            ShowPopup(ent, ent.Comp.Popup);
    }
}
