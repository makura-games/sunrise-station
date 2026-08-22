using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared.Sunrise.Eye;

public sealed partial class VisionDarkenerSystem : EntitySystem
{
    [Dependency] private SharedDarkenedVisionSystem _darkenedVision = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisionDarkenerComponent, GetVisionDarkeningEvent>(OnGetVisionDarkening);
        SubscribeLocalEvent<VisionDarkenerComponent, InventoryRelayedEvent<GetVisionDarkeningEvent>>(OnGetVisionDarkening);

        SubscribeLocalEvent<VisionDarkenerComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<VisionDarkenerComponent, ClothingGotUnequippedEvent>(OnGotUnquipped);

        SubscribeLocalEvent<VisionDarkenerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VisionDarkenerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VisionDarkenerComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnStartup(Entity<VisionDarkenerComponent> ent, ref ComponentStartup args)
    {
        if (_inventory.TryGetContainingSlot(ent.Owner, out var slot) && Transform(ent).ParentUid is { Valid: true } wearer)
        {
            _darkenedVision.UpdateVisionDarkening(wearer);
        }
    }

    private void OnShutdown(Entity<VisionDarkenerComponent> ent, ref ComponentShutdown args)
    {
        if (_inventory.TryGetContainingSlot(ent.Owner, out var slot) && Transform(ent).ParentUid is { Valid: true } wearer)
        {
            _darkenedVision.UpdateVisionDarkening(wearer);
        }
    }

    private void OnGetVisionDarkening(Entity<VisionDarkenerComponent> ent, ref GetVisionDarkeningEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;
        if (TryComp<ItemToggleComponent>(ent, out var toggle) && !_itemToggle.IsActivated((ent.Owner, toggle)))
            return;
        args.Strength += ent.Comp.Strength;
    }

    private void OnGetVisionDarkening(Entity<VisionDarkenerComponent> ent, ref InventoryRelayedEvent<GetVisionDarkeningEvent> args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;
        if (TryComp<ItemToggleComponent>(ent, out var toggle) && !_itemToggle.IsActivated((ent.Owner, toggle)))
            return;
        args.Args.Strength += ent.Comp.Strength;
    }

    private void OnGotEquipped(Entity<VisionDarkenerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        _darkenedVision.UpdateVisionDarkening(args.Wearer);
    }

    private void OnGotUnquipped(Entity<VisionDarkenerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _darkenedVision.UpdateVisionDarkening(args.Wearer);
    }

    private void OnToggled(Entity<VisionDarkenerComponent> ent, ref ItemToggledEvent args)
    {
        if (_inventory.TryGetContainingSlot(ent.Owner, out var slot) && Transform(ent).ParentUid is { Valid: true } wearer)
        {
            _darkenedVision.UpdateVisionDarkening(wearer);
        }
    }
}
