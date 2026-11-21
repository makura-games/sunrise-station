using Content.Shared._Sunrise.Biocode.Components;
using Content.Shared.Containers;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Storage;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Shared._Sunrise.Biocode.Systems;

/// <summary>
/// System that handles automatic deactivation of biocoded items when they're not in authorized user's possession.
/// </summary>
public abstract partial class BiocodeDeactivationSystem : EntitySystem
{
    [Dependency] private BiocodeSystem _biocodeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BiocodeDeactivationComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BiocodeDeactivationComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BiocodeDeactivationComponent, DroppedEvent>(OnItemDropped);
        SubscribeLocalEvent<BiocodeDeactivationComponent, GotEquippedEvent>(OnItemPickedUp);
    }

    private void OnItemDropped(EntityUid uid, BiocodeDeactivationComponent component, DroppedEvent args)
    {
        if (!component.DeactivateOnRemoval)
            return;

        // Check if this item has biocode
        if (!TryComp<BiocodeComponent>(uid, out var biocodeComponent))
            return;

        // Item was dropped, deactivate it
        DeactivateItem(uid);
    }

    private void OnItemPickedUp(EntityUid uid, BiocodeDeactivationComponent component, GotEquippedEvent args)
    {
        if (!component.DeactivateOnUnauthorized)
            return;

        // Check if this item has biocode
        if (!TryComp<BiocodeComponent>(uid, out var biocodeComponent))
            return;

        // Check if the picker is authorized
        if (_biocodeSystem.CanUse(args.EquipTarget, biocodeComponent.Factions))
            return;

        // Picker is not authorized, deactivate the item
        DeactivateItem(uid);
    }

    private void OnActivate(EntityUid uid, BiocodeDeactivationComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        // Check if this item has biocode
        if (!TryComp<BiocodeComponent>(uid, out var biocodeComponent))
            return;

        // Check if user is authorized
        if (_biocodeSystem.CanUse(args.User, biocodeComponent.Factions))
            return;

        // User is not authorized, show alert and prevent activation
        var alertText = component.AlertText ?? biocodeComponent.AlertText;
        ShowAlert(args.User, alertText);
        args.Handled = true;
    }

    private void OnUseInHand(EntityUid uid, BiocodeDeactivationComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Check if this item has biocode
        if (!TryComp<BiocodeComponent>(uid, out var biocodeComponent))
            return;

        // Check if user is authorized
        if (_biocodeSystem.CanUse(args.User, biocodeComponent.Factions))
            return;

        // User is not authorized, show alert and prevent use
        var alertText = component.AlertText ?? biocodeComponent.AlertText;
        ShowAlert(args.User, alertText);
        args.Handled = true;
    }

    /// <summary>
    /// Shows an alert to the user. Override this method to implement specific alert display logic.
    /// </summary>
    protected abstract void ShowAlert(EntityUid user, string alertText);

    /// <summary>
    /// Deactivates the item. Override this method in the shared system to implement specific deactivation logic.
    /// </summary>
    protected abstract void DeactivateItem(EntityUid uid);

    private EntityUid? GetContainerOwner(EntityUid container)
    {
        // Try to find the owner through various container types
        if (TryComp<HandsComponent>(container, out _))
        {
            return container;
        }

        if (TryComp<InventoryComponent>(container, out _))
        {
            return container;
        }

        if (TryComp<StorageComponent>(container, out _))
        {
            return container;
        }

        // Check if this container is inside another entity
        var parent = Transform(container).ParentUid;
        if (parent != EntityUid.Invalid)
        {
            return GetContainerOwner(parent);
        }

        return null;
    }
}
