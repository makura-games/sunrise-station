using Content.Shared._Sunrise.Weapons.Ranged.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Sunrise.Weapons.Ranged.Systems;

public sealed partial class WeaponStorageColorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeaponStorageColorComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<WeaponStorageColorComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<WeaponStorageColorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == (ent.Comp.UseStorage ? StorageComponent.ContainerId : SharedGunSystem.MagazineSlot))
            UpdateAppearance(ent, args.Container);
    }

    private void OnRemoved(Entity<WeaponStorageColorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == (ent.Comp.UseStorage ? StorageComponent.ContainerId : SharedGunSystem.MagazineSlot))
            UpdateAppearance(ent, args.Container);
    }

    private void UpdateAppearance(Entity<WeaponStorageColorComponent> ent, BaseContainer container)
    {
        if (!ent.Comp.UseStorage)
        {
            string? magazine = null;
            foreach (var item in container.ContainedEntities)
            {
                magazine = MetaData(item).EntityPrototype?.ID;
                break;
            }

            _appearance.SetData(ent.Owner, WeaponStorageColorVisuals.ItemPrototype, magazine ?? string.Empty);
            _appearance.SetData(ent.Owner, WeaponStorageColorVisuals.Fill, magazine == null ? 0 : 1);
            return;
        }

        string? prototype = null;
        var count = 0;
        var fullItem = false;
        foreach (var item in container.ContainedEntities)
        {
            count++;
            var id = MetaData(item).EntityPrototype?.ID;
            if (id == null)
                continue;

            prototype ??= id;
            if (ent.Comp.FullItemTag == null || !_tag.HasTag(item, ent.Comp.FullItemTag.Value))
                continue;

            prototype = id;
            fullItem = true;
            break;
        }

        _appearance.SetData(ent.Owner, WeaponStorageColorVisuals.ItemPrototype, prototype ?? string.Empty);
        _appearance.SetData(ent.Owner, WeaponStorageColorVisuals.Fill, fullItem ? -1 : Math.Min(count, ent.Comp.MaxFill));
    }
}
