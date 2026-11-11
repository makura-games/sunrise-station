using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Sunrise.Helpers;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Storage;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.SponsorInventory;

public sealed partial class InventoryWindow
{
    /*
     * Preview entity equipment and bag storage rendering for selected loadout and sponsor items.
     */
    private const int BagTitleMaxLength = 36;
    private static readonly Color BagGridTileModulate = Color.FromHex("#C8C8D0");

    private void ApplySponsorSlotsToPreview(EntityUid dummy, SunriseInventorySelection selection)
    {
        foreach (var (slot, itemId) in selection.SlotItems)
        {
            if (!TryGetSponsorItem(itemId, out var item))
                continue;

            // Replacing a storage item must preserve its contents or restore the previous item if the sponsor item cannot fit.
            var spawned = _entManager.SpawnEntity(item.EntityPrototype, MapCoordinates.Nullspace);
            List<EntityUid>? previousStorageItems = null;
            EntityUid? previousItem = null;

            if (_inventory.TryGetSlotEntity(dummy, slot, out var existing))
            {
                previousItem = existing.Value;
                previousStorageItems = TakeStorageItems(previousItem.Value);

                if (!_inventory.TryUnequip(dummy, slot, out _, silent: true, force: true, reparent: false))
                {
                    MoveStorageItemsOrDelete(previousItem.Value, previousStorageItems);
                    _entManager.DeleteEntity(spawned);
                    continue;
                }
            }

            if (_inventory.TryEquip(dummy, spawned, slot, silent: true, force: true) &&
                (previousStorageItems == null || TryMoveStorageItems(spawned, previousStorageItems)))
            {
                if (previousItem != null)
                    _entManager.DeleteEntity(previousItem.Value);

                continue;
            }

            _inventory.TryUnequip(dummy, slot, out _, silent: true, force: true, reparent: false);

            if (previousItem != null)
            {
                _inventory.TryEquip(dummy, previousItem.Value, slot, silent: true, force: true);

                if (previousStorageItems != null)
                    MoveStorageItemsOrDelete(previousItem.Value, previousStorageItems);
            }

            if (_entManager.EntityExists(spawned))
                _entManager.DeleteEntity(spawned);
        }
    }

    private void EnsureSelectedLoadoutStorage(EntityUid dummy, RoleLoadout roleLoadout)
    {
        var expectedStorage = GetSelectedLoadoutStorage(roleLoadout);
        foreach (var (slotName, prototypes) in expectedStorage)
        {
            if (!_inventory.TryGetSlotEntity(dummy, slotName, out var storageEntity) ||
                !_entManager.TryGetComponent<StorageComponent>(storageEntity.Value, out var storage))
            {
                continue;
            }

            var existingCounts = CountStoragePrototypes(storage);
            foreach (var prototype in prototypes)
            {
                // Only add missing instances; LoadProfileEntity may already have placed the starting gear storage contents.
                var id = prototype.Id;
                if (existingCounts.TryGetValue(id, out var existingCount) && existingCount > 0)
                {
                    existingCounts[id] = existingCount - 1;
                    continue;
                }

                var spawned = _entManager.SpawnEntity(prototype, MapCoordinates.Nullspace);
                if (!_entManager.TryGetComponent<ItemComponent>(spawned, out var itemComp) ||
                    !_storage.TryGetAvailableGridSpace((storageEntity.Value, storage), (spawned, itemComp), out var location) ||
                    !_storage.InsertAt((storageEntity.Value, storage), (spawned, itemComp), location.Value, out _, playSound: false, stackAutomatically: false))
                {
                    _entManager.DeleteEntity(spawned);
                }
            }
        }
    }

    private Dictionary<string, List<EntProtoId>> GetSelectedLoadoutStorage(RoleLoadout roleLoadout)
    {
        var storage = new Dictionary<string, List<EntProtoId>>();
        foreach (var group in roleLoadout.SelectedLoadouts.Values)
        {
            foreach (var selectedLoadout in group)
            {
                if (!_prototype.TryIndex(selectedLoadout.Prototype, out LoadoutPrototype? loadout))
                    continue;

                foreach (var (slot, prototypes) in GetLoadoutStorage(loadout))
                {
                    if (!storage.TryGetValue(slot, out var list))
                        storage[slot] = list = new List<EntProtoId>();

                    foreach (var prototype in prototypes)
                        list.Add(prototype);
                }
            }
        }

        return storage;
    }

    private Dictionary<string, int> CountStoragePrototypes(StorageComponent storage)
    {
        var counts = new Dictionary<string, int>();
        foreach (var entity in storage.Container.ContainedEntities)
        {
            var prototype = _entManager.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID;
            if (prototype == null)
                continue;

            counts[prototype] = counts.GetValueOrDefault(prototype) + 1;
        }

        return counts;
    }

    private void PackSponsorBagItems()
    {
        if (_previewDummy == null || !TryGetPreviewBackStorage(out var back, out var storage))
        {
            BagHint.Text = Loc.GetString("sunrise-inventory-bag-missing");
            BagCapacityLabel.Text = string.Empty;
            return;
        }

        var selection = GetSponsorSelectionSnapshot();

        foreach (var itemId in selection.BagItems)
        {
            if (!TryGetSponsorItem(itemId, out var item))
                continue;

            // The spawned entity id is kept only for this preview rebuild so bag remove buttons can map back to sponsor item ids.
            var spawned = _entManager.SpawnEntity(item.EntityPrototype, MapCoordinates.Nullspace);
            if (!_entManager.TryGetComponent<ItemComponent>(spawned, out var itemComp) ||
                !_storage.CanInsert(back, spawned, out _, storageComp: storage, item: itemComp) ||
                !_storage.TryGetAvailableGridSpace((back, storage), (spawned, itemComp), out var location) ||
                !_storage.InsertAt((back, storage), (spawned, itemComp), location.Value, out _, playSound: false, stackAutomatically: false))
            {
                _entManager.DeleteEntity(spawned);
                continue;
            }

            _sponsorBagPreviewItems[spawned] = itemId;
        }
    }

    private void RenderBagGrid()
    {
        BagGridHost.RemoveAllChildren();

        if (!TryGetPreviewBackStorage(out var back, out var storage) || storage.Grid.Count == 0)
            return;

        var bounds = storage.Grid.GetBoundingBox();
        var width = (bounds.Width + 1) * BagTileSize;
        var height = (bounds.Height + 1) * BagTileSize;
        BagGridHost.SetWidth = width;
        BagGridHost.SetHeight = height;

        // The bag grid is rendered from the preview storage component, so loadout and sponsor items share one view.
        var bagTitle = Loc.GetString("sunrise-inventory-bag-title-named", ("bag", GetEntityName(back)));
        BagTitle.Text = bagTitle.TruncateWithEllipsis(BagTitleMaxLength);
        BagTitle.ToolTip = bagTitle;
        BagCapacityLabel.Text = Loc.GetString(
            "sunrise-inventory-bag-capacity",
            ("used", CountStorageUsedCells(storage)),
            ("total", storage.Grid.GetArea()),
            ("items", storage.StoredItems.Count));
        BagHint.Text = Loc.GetString("sunrise-inventory-bag-hint");

        for (var y = bounds.Bottom; y <= bounds.Top; y++)
        {
            for (var x = bounds.Left; x <= bounds.Right; x++)
            {
                var location = new Vector2i(x, y);
                var valid = storage.Grid.Any(box => box.Contains(location));
                var tile = CreateStorageTile(valid);

                BagGridHost.AddChild(tile);
                LayoutContainer.SetPosition(tile, ToBagPosition(location, bounds));
            }
        }

        foreach (var (entity, location) in storage.StoredItems.ToArray())
        {
            if (!_entManager.TryGetComponent<ItemComponent>(entity, out var itemComp))
                continue;

            var itemBounds = _item.GetAdjustedItemShape((entity, itemComp), location).GetBoundingBox();
            var sponsorItemId = _sponsorBagPreviewItems.TryGetValue(entity, out var itemId)
                ? itemId
                : null;
            var itemPrototype = _entManager.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID;
            var removeAction = GetBagItemRemoveAction(itemPrototype, sponsorItemId);
            var itemSize = new Vector2(
                (itemBounds.Width + 1) * BagTileSize,
                (itemBounds.Height + 1) * BagTileSize);
            var itemControl = CreateBagItemControl(
                entity,
                location,
                sponsorItemId,
                removeAction,
                itemSize);

            BagGridHost.AddChild(itemControl);
            LayoutContainer.SetPosition(itemControl, ToBagPosition(itemBounds.BottomLeft, bounds));
        }
    }

    private Control CreateBagItemControl(
        EntityUid entity,
        ItemStorageLocation location,
        string? sponsorItemId,
        Action? removeAction,
        Vector2 size)
    {
        var control = new Control
        {
            MinSize = size,
            SetWidth = size.X,
            SetHeight = size.Y,
            MouseFilter = MouseFilterMode.Stop,
        };

        var source = sponsorItemId == null
            ? Loc.GetString("sunrise-inventory-source-loadout-storage")
            : Loc.GetString("sunrise-inventory-source-sponsor");
        control.TooltipSupplier = _ => new Tooltip
        {
            Text = Loc.GetString(
                "sunrise-inventory-bag-item-tooltip",
                ("item", _entManager.GetComponent<MetaDataComponent>(entity).EntityName),
                ("source", source)),
        };

        if (_entManager.TryGetComponent<ItemComponent>(entity, out var item))
        {
            control.AddChild(new ItemGridPiece((entity, item), new ItemStorageLocation(location.Rotation, Vector2i.Zero), _entManager)
            {
                MinSize = size,
                SetWidth = size.X,
                SetHeight = size.Y,
            });
        }
        else
        {
            var sprite = new SpriteView
            {
                SetSize = size,
                Scale = new Vector2(2, 2),
                OverrideDirection = Direction.South,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };
            sprite.SetEntity(entity);
            control.AddChild(sprite);
        }

        if (removeAction != null)
            control.AddChild(CreateRemoveButton(removeAction, "sunrise-inventory-bag-remove-tooltip"));

        return control;
    }

    private TextureRect CreateStorageTile(bool valid)
    {
        return new TextureRect
        {
            SetWidth = BagTileSize,
            SetHeight = BagTileSize,
            Texture = Theme.ResolveTextureOrNull(valid
                ? StorageTileEmptyTexturePath
                : StorageTileBlockedTexturePath)?.Texture,
            TextureScale = new Vector2(2, 2),
            MouseFilter = MouseFilterMode.Ignore,
            Modulate = BagGridTileModulate
        };
    }

    private int CountStorageUsedCells(StorageComponent storage)
    {
        var used = 0;
        foreach (var (entity, location) in storage.StoredItems)
        {
            if (!_entManager.TryGetComponent<ItemComponent>(entity, out var itemComp))
                continue;

            used += _item.GetAdjustedItemShape((entity, itemComp), location).GetArea();
        }

        return Math.Min(used, storage.Grid.GetArea());
    }

    private static Vector2 ToBagPosition(Vector2i gridPosition, Box2i bounds)
    {
        return new Vector2(
            (gridPosition.X - bounds.Left) * BagTileSize,
            (gridPosition.Y - bounds.Bottom) * BagTileSize);
    }

    private bool TryGetPreviewBackStorage(out EntityUid back, out StorageComponent storage)
    {
        back = default;
        storage = default!;

        if (_previewDummy == null)
            return false;

        if (!_inventory.TryGetSlotEntity(_previewDummy.Value, "back", out var backEntity))
            return false;

        if (!_entManager.TryGetComponent<StorageComponent>(backEntity.Value, out var storageComp))
            return false;

        if (!backEntity.Value.IsValid())
            return false;

        back = backEntity.Value;
        storage = storageComp;
        return true;
    }

    private static HashSet<string> GetEquipmentTargetSlots(Dictionary<string, EntProtoId> equipment)
    {
        return equipment.Keys.ToHashSet();
    }

    private HashSet<string> GetValidSlotsForEntityPrototype(string entityPrototype)
    {
        var slots = new HashSet<string>();
        if (_previewDummy == null ||
            !_inventory.TryGetSlots(_previewDummy.Value, out var slotDefinitions))
        {
            return slots;
        }

        // Пробная сущность позволяет проверить реальные whitelist и зависимости слотов.
        // Спонсорский слой заменяет уже выданный loadout, поэтому его обязательные предметы здесь не ограничивают выбор.
        var item = _entManager.SpawnEntity(entityPrototype, MapCoordinates.Nullspace);
        foreach (var slot in slotDefinitions)
        {
            if (!slot.ShowInWindow)
                continue;

            if (CanFitSlot(_previewDummy.Value, item, slot))
                slots.Add(slot.Name);
        }

        _entManager.DeleteEntity(item);
        return slots;
    }

    private bool CanFitSlot(EntityUid target, EntityUid itemUid, SlotDefinition slot)
    {
        EntityUid? dependency = null;
        if (slot.DependsOn != null && !_inventory.TryGetSlotEntity(target, slot.DependsOn, out dependency))
            return false;

        // Some slots are only valid when another equipped item exposes the matching dependency component.
        if (slot.DependsOnComponents is { } componentRegistry && dependency is { } dependencyUid)
        {
            foreach (var (_, entry) in componentRegistry)
            {
                if (!_entManager.HasComponent(dependencyUid, entry.Component.GetType()))
                    return false;

                if (_entManager.TryGetComponent<AllowSuitStorageComponent>(dependencyUid, out var allowSuitStorage) &&
                    _whitelist.IsWhitelistFailOrNull(allowSuitStorage.Whitelist, itemUid))
                {
                    return false;
                }
            }
        }

        _entManager.TryGetComponent<ClothingComponent>(itemUid, out var clothing);
        _entManager.TryGetComponent<ItemComponent>(itemUid, out var item);

        // Pocket slots accept small non-clothing items, while normal equipment slots require matching ClothingComponent flags.
        var fitsPocket = slot.SlotFlags.HasFlag(SlotFlags.POCKET) &&
                         item != null &&
                         _item.GetSizePrototype(item.Size) <= _item.GetSizePrototype(PocketableItemSize);

        if (clothing == null && !fitsPocket)
            return false;

        if (clothing != null && !clothing.Slots.HasFlag(slot.SlotFlags) && !fitsPocket)
            return false;

        return !_whitelist.IsWhitelistFail(slot.Whitelist, itemUid) &&
               !_whitelist.IsWhitelistPass(slot.Blacklist, itemUid);
    }

    private bool CanAddSponsorItemToBag(string itemId)
    {
        if (!TryGetSponsorItem(itemId, out var item))
            return false;

        if (!TryGetPreviewBackStorage(out var back, out var storage))
            return false;

        var spawned = _entManager.SpawnEntity(item.EntityPrototype, MapCoordinates.Nullspace);
        var canInsert = _entManager.TryGetComponent<ItemComponent>(spawned, out var itemComp) &&
                        _storage.CanInsert(back, spawned, out _, storageComp: storage, item: itemComp);
        _entManager.DeleteEntity(spawned);
        return canInsert;
    }

    private void AddSponsorItemToSlot(string itemId, string slot)
    {
        if (_characterProfile == null || CurrentJobId == null)
            return;

        // Обычный loadout остаётся неизменным, а спонсорский выбор применяется поверх него при предпросмотре и спавне.
        UpdateSponsorSelection(selection =>
        {
            RemoveSponsorItem(selection, itemId);
            selection.SlotItems[slot] = itemId;
        });
    }

    private void AddSponsorItemToBag(string itemId)
    {
        if (!CanAddSponsorItemToBag(itemId))
        {
            StatusLabel.Text = Loc.GetString("sunrise-inventory-bag-full");
            return;
        }

        UpdateSponsorSelection(selection =>
        {
            RemoveSponsorItem(selection, itemId);
            selection.BagItems.Add(itemId);
        });
    }

    private void RemoveSponsorSlot(string slot)
    {
        UpdateSponsorSelection(selection => selection.SlotItems.Remove(slot), removeGlobalSlot: slot);
    }

    private void RemoveSponsorItem(string itemId)
    {
        UpdateSponsorSelection(selection => RemoveSponsorItem(selection, itemId), removeGlobalItem: itemId);
    }

    private bool CanRemoveSlotItem(string slot)
    {
        if (GetSponsorSelectionSnapshot().SlotItems.ContainsKey(slot))
            return true;

        return TryFindSelectedLoadoutSlot(slot, out var groupId, out var loadoutId) &&
               CanRemoveSelectedLoadout(groupId, loadoutId);
    }

    private void RemoveSlotItem(string slot)
    {
        if (GetSponsorSelectionSnapshot().SlotItems.ContainsKey(slot))
        {
            RemoveSponsorSlot(slot);
            return;
        }

        if (TryFindSelectedLoadoutSlot(slot, out var groupId, out var loadoutId))
            RemoveSelectedLoadout(groupId, loadoutId);
    }

    private Action? GetBagItemRemoveAction(string? itemPrototype, string? sponsorItemId)
    {
        if (sponsorItemId != null)
            return () => RemoveSponsorItem(sponsorItemId);

        if (itemPrototype != null &&
            TryFindSelectedLoadoutStorage(itemPrototype, out var groupId, out var loadoutId) &&
            CanRemoveSelectedLoadout(groupId, loadoutId))
        {
            return () => RemoveSelectedLoadout(groupId, loadoutId);
        }

        return null;
    }

}
