using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.SponsorInventory;

public sealed partial class InventoryWindow
{
    /*
     * Selection mutation helpers for loadouts, sponsor slots, and job-specific sponsor inventory state.
     */
    private void RemoveSelectedLoadout(string groupId, string loadoutId)
    {
        if (_characterProfile == null || CurrentJobId == null)
            return;

        if (!CanRemoveSelectedLoadout(groupId, loadoutId))
        {
            StatusLabel.Text = Loc.GetString("sunrise-inventory-loadout-required");
            return;
        }

        var roleLoadout = EnsureRoleLoadout(CurrentJobId);
        roleLoadout.RemoveLoadout(groupId, loadoutId, _prototype);
        _characterProfile = _characterProfile.WithLoadout(roleLoadout);
        RefreshEquipment();
    }

    private bool CanRemoveSelectedLoadout(string groupId, string loadoutId)
    {
        if (_characterProfile == null || CurrentJobId == null)
            return false;

        var jobLoadoutId = LoadoutSystem.GetJobPrototype(CurrentJobId);
        if (!_characterProfile.Loadouts.TryGetValue(jobLoadoutId, out var roleLoadout))
            return false;

        return CanRemoveSelectedLoadout(roleLoadout, groupId, loadoutId);
    }

    private bool CanRemoveSelectedLoadout(
        RoleLoadout roleLoadout,
        ProtoId<LoadoutGroupPrototype> groupId,
        ProtoId<LoadoutPrototype> loadoutId)
    {
        if (!roleLoadout.SelectedLoadouts.TryGetValue(groupId, out var selectedLoadouts) ||
            selectedLoadouts.All(l => l.Prototype != loadoutId))
        {
            return false;
        }

        if (!_prototype.TryIndex(groupId, out LoadoutGroupPrototype? group))
            return true;

        return selectedLoadouts.Count > Math.Max(0, group.MinLimit);
    }

    private bool TryFindSelectedLoadoutSlot(string slot, out string groupId, out string loadoutId)
    {
        return TryFindSelectedLoadout(
            loadout => GetLoadoutEquipment(loadout).ContainsKey(slot),
            out groupId,
            out loadoutId);
    }

    private bool TryFindSelectedLoadoutStorage(string itemPrototype, out string groupId, out string loadoutId)
    {
        return TryFindSelectedLoadout(
            loadout => GetLoadoutStorage(loadout).Values.SelectMany(p => p).Any(p => p.Id == itemPrototype),
            out groupId,
            out loadoutId);
    }

    private bool TryFindSelectedLoadout(
        Func<LoadoutPrototype, bool> predicate,
        out string groupId,
        out string loadoutId)
    {
        groupId = string.Empty;
        loadoutId = string.Empty;

        if (_characterProfile == null || CurrentJobId == null)
            return false;

        var jobLoadoutId = LoadoutSystem.GetJobPrototype(CurrentJobId);
        if (!_characterProfile.Loadouts.TryGetValue(jobLoadoutId, out var roleLoadout))
            return false;

        foreach (var (selectedGroup, loadouts) in roleLoadout.SelectedLoadouts)
        {
            foreach (var selectedLoadout in loadouts)
            {
                if (!_prototype.TryIndex(selectedLoadout.Prototype, out LoadoutPrototype? loadout) ||
                    !predicate(loadout))
                {
                    continue;
                }

                groupId = selectedGroup;
                loadoutId = loadout.ID;
                return true;
            }
        }

        return false;
    }

    private void UpdateSponsorSelection(
        Action<SunriseInventorySelection> update,
        string? removeGlobalSlot = null,
        string? removeGlobalItem = null)
    {
        if (_characterProfile == null)
            return;

        // Work on a clone so canceling the lobby window can still discard all local sponsor inventory edits.
        var inventory = _editedInventoryProfile.Clone();

        if (removeGlobalSlot != null)
            inventory.Global.SlotItems.Remove(removeGlobalSlot);

        if (removeGlobalItem != null)
            RemoveSponsorItem(inventory.Global, removeGlobalItem);

        var jobId = CurrentJobId;
        var selection = jobId == null
            ? inventory.Global
            : inventory.Jobs.GetValueOrDefault(jobId)?.Clone() ?? new SunriseInventorySelection();

        update(selection);

        // Empty job selections collapse back to global-only data instead of leaving no-op per-job records.
        if (jobId == null)
        {
            inventory.Global = selection;
        }
        else if (selection.IsEmpty())
        {
            inventory.Jobs.Remove(jobId);
        }
        else
        {
            inventory.Jobs[jobId] = selection;
        }

        _editedInventoryProfile = inventory;
        RefreshEquipment();
    }

    private void RemoveSponsorSlotSelection(string slot)
    {
        if (_characterProfile == null)
            return;

        var inventory = _editedInventoryProfile.Clone();
        inventory.Global.SlotItems.Remove(slot);

        var jobId = CurrentJobId;
        if (jobId != null && inventory.Jobs.TryGetValue(jobId, out var jobSelection))
        {
            var selection = jobSelection.Clone();
            selection.SlotItems.Remove(slot);

            // Removing a sponsor slot because a loadout took it must clear both global and current job overrides.
            if (selection.IsEmpty())
                inventory.Jobs.Remove(jobId);
            else
                inventory.Jobs[jobId] = selection;
        }

        _editedInventoryProfile = inventory;
    }

    private SunriseInventorySelection GetSponsorSelectionSnapshot()
    {
        if (_characterProfile == null)
            return new SunriseInventorySelection();

        // Effective selection overlays current job overrides on top of global sponsor inventory picks.
        return SunriseInventoryValidation.GetEffectiveSelection(_editedInventoryProfile, CurrentJobId);
    }

    private static void RemoveSponsorItem(SunriseInventorySelection selection, string itemId)
    {
        foreach (var slot in selection.SlotItems.Where(p => p.Value == itemId).Select(p => p.Key).ToArray())
        {
            selection.SlotItems.Remove(slot);
        }

        selection.BagItems.RemoveAll(i => i == itemId);
    }

    private static bool IsSponsorItemSelected(SunriseInventorySelection selection, string itemId)
    {
        return selection.BagItems.Contains(itemId) || selection.SlotItems.ContainsValue(itemId);
    }

    private Dictionary<string, EntProtoId> GetLoadoutEquipment(LoadoutPrototype loadout)
    {
        var equipment = new Dictionary<string, EntProtoId>();

        if (_prototype.Resolve(loadout.StartingGear, out var startingGear))
        {
            foreach (var (slot, prototype) in startingGear.Equipment)
                equipment[slot] = prototype;
        }

        // Explicit loadout equipment wins over inherited starting gear for the same slot.
        foreach (var (slot, prototype) in loadout.Equipment)
            equipment[slot] = prototype;

        return equipment;
    }

    private Dictionary<string, List<EntProtoId>> GetLoadoutStorage(LoadoutPrototype loadout)
    {
        var storage = new Dictionary<string, List<EntProtoId>>();

        if (_prototype.Resolve(loadout.StartingGear, out var startingGear))
        {
            foreach (var (slot, prototypes) in startingGear.Storage)
                storage[slot] = prototypes.ToList();
        }

        // Storage entries are additive: loadout storage extends starting gear storage instead of replacing it.
        foreach (var (slot, prototypes) in loadout.Storage)
        {
            if (!storage.TryGetValue(slot, out var list))
                storage[slot] = list = new List<EntProtoId>();

            list.AddRange(prototypes);
        }

        return storage;
    }

    private string? GetLoadoutIcon(
        LoadoutPrototype loadout,
        Dictionary<string, EntProtoId> equipment,
        Dictionary<string, List<EntProtoId>> storage)
    {
        if (loadout.DummyEntity != null)
            return loadout.DummyEntity.Value.Id;

        if (equipment.Count > 0)
            return equipment.Values.First().Id;

        foreach (var prototypes in storage.Values)
        {
            if (prototypes.Count > 0)
                return prototypes[0].Id;
        }

        return _entManager.System<LoadoutSystem>().GetFirstOrNull(loadout)?.Id;
    }
}
