using System.Collections.Generic;
using System.Linq;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.SponsorInventory;

public sealed partial class SunriseInventorySystem
{
    /*
     * Applies a valid profile to a mob and safely replaces equipped items.
     */

    /// <summary>
    /// Applies a validated sponsor inventory profile to the spawned mob for the selected job.
    /// </summary>
    public bool TryApplyInventory(
        EntityUid mob,
        SunriseInventoryProfile profile,
        string? jobId,
        ICommonSession session)
    {
        if (_sponsors == null)
            return false;

        profile ??= new SunriseInventoryProfile();

        var validInventory = SunriseInventoryValidation.EnsureValid(profile, session, _prototype, _sponsors);
        var selection = SunriseInventoryValidation.GetEffectiveSelection(validInventory, jobId);
        if (selection.IsEmpty())
            return true;

        var itemLookup = BuildSponsorItemLookup(_sponsors.GetSponsorInventoryConfig());
        var usedItems = new HashSet<string>();
        // Обычный loadout уже выдан к этому моменту; спонсорские предметы являются отдельным слоем замены экипировки.
        foreach (var (slot, itemId) in selection.SlotItems)
        {
            if (usedItems.Contains(itemId) || !itemLookup.TryGetValue(itemId, out var item))
                continue;

            if (!TryEquipSponsorItem(mob, slot, item))
                continue;

            usedItems.Add(itemId);
        }

        if (!_inventory.TryGetSlotEntity(mob, "back", out var backEntity) ||
            !TryComp<StorageComponent>(backEntity, out var storage))
        {
            return true;
        }

        foreach (var itemId in selection.BagItems)
        {
            if (!usedItems.Add(itemId) || !itemLookup.TryGetValue(itemId, out var item))
                continue;

            TryStoreSponsorItem(backEntity.Value, storage, item);
        }

        return true;
    }

    private bool TryEquipSponsorItem(EntityUid mob, string slot, SponsorInventoryItemInfo item)
    {
        var spawned = Spawn(item.EntityPrototype, Transform(mob).Coordinates);
        if (!_inventory.CanEquip(mob, spawned, slot, out _))
        {
            QueueDel(spawned);
            return false;
        }

        if (!TryPrepareSponsorSlotReplacement(mob, slot, out var replacement))
        {
            QueueDel(spawned);
            return false;
        }

        if (!_inventory.TryEquip(mob, spawned, slot, silent: true))
        {
            RestoreSponsorSlotReplacement(mob, slot, replacement);
            QueueDel(spawned);
            return false;
        }

        if (!TryFinishSponsorSlotReplacement(mob, slot, spawned, replacement))
        {
            QueueDel(spawned);
            return false;
        }

        DeletePreviousSponsorItem(replacement);
        return true;
    }

    private bool TryPrepareSponsorSlotReplacement(
        EntityUid mob,
        string slot,
        out SponsorSlotReplacement replacement)
    {
        replacement = SponsorSlotReplacement.Empty;
        if (!_inventory.TryGetSlotEntity(mob, slot, out var existing))
            return true;

        var previousItem = existing.Value;

        // Storage contents are detached before unequip so rollback can restore the old container.
        var previousStorageItems = TakeStorageItems(previousItem);
        if (_inventory.TryUnequip(mob, slot, out _, silent: true, force: true, reparent: false))
        {
            replacement = new SponsorSlotReplacement(previousItem, previousStorageItems);
            return true;
        }

        MoveStorageItemsOrDelete(previousItem, previousStorageItems);
        return false;
    }

    private bool TryFinishSponsorSlotReplacement(
        EntityUid mob,
        string slot,
        EntityUid equippedItem,
        SponsorSlotReplacement replacement)
    {
        if (replacement.PreviousStorageItems == null)
            return true;

        if (TryMoveStorageItems(equippedItem, replacement.PreviousStorageItems))
            return true;

        _inventory.TryUnequip(mob, slot, out _, silent: true, force: true, reparent: false);
        RestoreSponsorSlotReplacement(mob, slot, replacement);
        return false;
    }

    private void RestoreSponsorSlotReplacement(
        EntityUid mob,
        string slot,
        SponsorSlotReplacement replacement)
    {
        if (replacement.PreviousItem == null)
            return;

        _inventory.TryEquip(mob, replacement.PreviousItem.Value, slot, silent: true, force: true);

        if (replacement.PreviousStorageItems != null)
            MoveStorageItemsOrDelete(replacement.PreviousItem.Value, replacement.PreviousStorageItems);
    }

    private void DeletePreviousSponsorItem(SponsorSlotReplacement replacement)
    {
        if (replacement.PreviousItem != null)
            QueueDel(replacement.PreviousItem.Value);
    }

    private bool TryStoreSponsorItem(EntityUid storageEntity, StorageComponent storage, SponsorInventoryItemInfo item)
    {
        var spawned = Spawn(item.EntityPrototype, Transform(storageEntity).Coordinates);
        if (!TryComp<ItemComponent>(spawned, out var itemComp))
        {
            QueueDel(spawned);
            return false;
        }

        if (_storage.CanInsert(storageEntity, spawned, out _, storageComp: storage, item: itemComp) &&
            _storage.TryGetAvailableGridSpace((storageEntity, storage), (spawned, itemComp), out var location) &&
            _storage.InsertAt((storageEntity, storage), (spawned, itemComp), location.Value, out _, playSound: false, stackAutomatically: false))
        {
            return true;
        }

        QueueDel(spawned);
        return false;
    }

    private List<EntityUid> TakeStorageItems(EntityUid storageEntity)
    {
        var items = new List<EntityUid>();
        if (!TryComp<StorageComponent>(storageEntity, out var storage))
            return items;

        foreach (var contained in storage.Container.ContainedEntities.ToArray())
        {
            if (!_container.Remove(contained, storage.Container, reparent: false, force: true))
                continue;

            items.Add(contained);
        }

        return items;
    }

    private bool TryMoveStorageItems(EntityUid storageEntity, List<EntityUid> items)
    {
        if (!TryComp<StorageComponent>(storageEntity, out var storage))
            return items.Count == 0;

        var inserted = new List<EntityUid>();
        foreach (var item in items)
        {
            if (!TryComp<ItemComponent>(item, out var itemComp) ||
                !_storage.TryGetAvailableGridSpace((storageEntity, storage), (item, itemComp), out var location) ||
                !_storage.InsertAt((storageEntity, storage), (item, itemComp), location.Value, out _, playSound: false, stackAutomatically: false))
            {
                foreach (var insertedItem in inserted)
                {
                    _container.Remove(insertedItem, storage.Container, reparent: false, force: true);
                }

                return false;
            }

            inserted.Add(item);
        }

        return true;
    }

    private void MoveStorageItemsOrDelete(EntityUid storageEntity, List<EntityUid> items)
    {
        if (TryMoveStorageItems(storageEntity, items))
            return;

        foreach (var item in items)
        {
            QueueDel(item);
        }
    }

    private static Dictionary<string, SponsorInventoryItemInfo> BuildSponsorItemLookup(SponsorInventoryConfig? config)
    {
        var itemLookup = new Dictionary<string, SponsorInventoryItemInfo>();
        foreach (var item in config?.Items ?? [])
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;

            itemLookup[item.Id] = item;
        }

        return itemLookup;
    }

    private readonly record struct SponsorSlotReplacement(
        EntityUid? PreviousItem,
        List<EntityUid>? PreviousStorageItems)
    {
        public static readonly SponsorSlotReplacement Empty = new(null, null);
    }
}
