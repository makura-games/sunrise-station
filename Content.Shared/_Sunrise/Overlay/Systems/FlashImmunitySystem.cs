using Content.Shared._Sunrise.Overlay.Components;
using Content.Shared._Sunrise.Overlay.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Player;

namespace Content.Shared._Sunrise.Overlay.Systems;

public sealed partial class FlashImmunitySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlashImmunityComponent, GotEquippedEvent>(OnFlashImmunityEquipped);
        SubscribeLocalEvent<FlashImmunityComponent, GotUnequippedEvent>(OnFlashImmunityUnEquipped);

        SubscribeLocalEvent<FlashImmunityComponent, ComponentStartup>(OnFlashImmunityChanged);
        SubscribeLocalEvent<FlashImmunityComponent, ComponentRemove>(OnFlashImmunityChanged);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);

        SubscribeLocalEvent<StarlightNightVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<StarlightNightVisionComponent, ComponentRemove>(OnVisionChanged); //это должно использовать shutdown, нut something else is already using it.....

        SubscribeLocalEvent<StarlightThermalVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<StarlightThermalVisionComponent, ComponentRemove>(OnVisionChanged); //это должно использовать shutdown, нut something else is already using it.....

        SubscribeLocalEvent<CycloriteVisionComponent, ComponentStartup>(OnVisionChanged);
        SubscribeLocalEvent<CycloriteVisionComponent, ComponentRemove>(OnVisionChanged); //это должно использовать shutdown, нut something else is already using it.....
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(args.Entity, HasFlashImmunityVisionBlockers(args.Entity));
        RaiseLocalEvent(args.Entity, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityChanged(EntityUid uid, FlashImmunityComponent component, EntityEventArgs args)
    {
        uid = GetPossibleWearer(uid);
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(uid));
        RaiseLocalEvent(uid, flashImmunityChangedEvent);
    }

    private void OnVisionChanged(EntityUid uid, Component component, EntityEventArgs args)
    {
        uid = GetPossibleWearer(uid);
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(uid));
        RaiseLocalEvent(uid, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityEquipped(EntityUid uid, FlashImmunityComponent component, GotEquippedEvent args)
    {
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(args.Equipee));
        RaiseLocalEvent(args.Equipee, flashImmunityChangedEvent);
    }

    private void OnFlashImmunityUnEquipped(EntityUid uid, FlashImmunityComponent component, GotUnequippedEvent args)
    {
        FlashImmunityCheckEvent flashImmunityChangedEvent = new(uid, HasFlashImmunityVisionBlockers(args.Equipee));
        RaiseLocalEvent(args.Equipee, flashImmunityChangedEvent);
    }

    private EntityUid GetPossibleWearer(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothingComponent))
        {
            //мы хотим получить владельца одежды, а не саму одежду
            return Transform(uid).ParentUid;
        }

        return uid;
    }

    public bool HasFlashImmunityVisionBlockers(EntityUid uid)
    {
        if (TryComp(uid, out FlashImmunityComponent? flashImmunityComponent))
        {
            if (flashImmunityComponent.Enabled)
                return true;
        }

        if (TryComp<InventoryComponent>(uid, out var inventoryComp))
        {
            //получаем все надетые предметы
            var slots = _inventory.GetSlotEnumerator((uid, inventoryComp), SlotFlags.WITHOUT_POCKET);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity != null && TryComp(slot.ContainedEntity, out FlashImmunityComponent? wornFlashImmunityComponent))
                {
                    if (wornFlashImmunityComponent.Enabled)
                        return true;
                }
            }
        }

        return false;
    }
}
