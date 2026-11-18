using System.Collections.Generic;
using System.Linq;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Clothing;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.SponsorInventory;

public sealed partial class SunriseInventorySystem
{
    /*
     * Validates profiles against a concrete character through a temporary dummy mob.
     */
    private SunriseInventoryProfile EnsureValidForCharacter(
        ICommonSession session,
        int slot,
        SunriseInventoryProfile profile)
    {
        if (!_preferences.TryGetCachedPreferences(session.UserId, out var preferences) ||
            !preferences.Characters.TryGetValue(slot, out var character) ||
            character is not HumanoidCharacterProfile humanoid)
        {
            return new SunriseInventoryProfile();
        }

        var dummy = SpawnValidationDummy(humanoid);
        if (dummy == null)
            return new SunriseInventoryProfile();

        try
        {
            var items = BuildSponsorItemLookup(_sponsors?.GetSponsorInventoryConfig());
            var valid = new SunriseInventoryProfile
            {
                Global = EnsureValidSelectionForCharacter(
                    dummy.Value,
                    profile.Global,
                    validateBag: false,
                    items: items),
            };

            foreach (var (jobId, selection) in profile.Jobs ?? new Dictionary<string, SunriseInventorySelection>())
            {
                if (selection == null || !_prototype.TryIndex<JobPrototype>(jobId, out var job))
                    continue;

                ClearValidationDummyEquipment(dummy.Value);
                EquipValidationDummyForJob(session, humanoid, dummy.Value, job);

                var effectiveProfile = new SunriseInventoryProfile
                {
                    Global = valid.Global.Clone(),
                    Jobs = { [jobId] = selection.Clone() },
                };
                var effectiveSelection = SunriseInventoryValidation.GetEffectiveSelection(effectiveProfile, jobId);
                var validEffectiveSelection = EnsureValidSelectionForCharacter(
                    dummy.Value,
                    effectiveSelection,
                    validateBag: true,
                    items: items);
                var validSelection = GetJobSpecificSelection(selection, validEffectiveSelection);
                if (!validSelection.IsEmpty())
                    valid.Jobs[jobId] = validSelection;
            }

            return valid;
        }
        finally
        {
            QueueDel(dummy.Value);
        }
    }

    private SunriseInventorySelection EnsureValidSelectionForCharacter(
        EntityUid dummy,
        SunriseInventorySelection selection,
        bool validateBag,
        IReadOnlyDictionary<string, SponsorInventoryItemInfo> items)
    {
        var valid = new SunriseInventorySelection();
        selection ??= new SunriseInventorySelection();
        var usedItems = new HashSet<string>();

        foreach (var (slot, itemId) in selection.SlotItems)
        {
            if (!usedItems.Add(itemId) ||
                !items.TryGetValue(itemId, out var item))
                continue;

            if (!TryEquipSponsorItem(dummy, slot, item))
            {
                usedItems.Remove(itemId);
                continue;
            }

            valid.SlotItems[slot] = itemId;
        }

        if (!validateBag ||
            !_inventory.TryGetSlotEntity(dummy, "back", out var backEntity) ||
            !TryComp<StorageComponent>(backEntity, out var storage))
        {
            return valid;
        }

        foreach (var itemId in selection.BagItems)
        {
            if (!usedItems.Add(itemId) ||
                !items.TryGetValue(itemId, out var item))
                continue;

            if (!TryStoreSponsorItem(backEntity.Value, storage, item))
            {
                usedItems.Remove(itemId);
                continue;
            }

            valid.BagItems.Add(itemId);
        }

        return valid;
    }

    private EntityUid? SpawnValidationDummy(HumanoidCharacterProfile profile)
    {
        if (!_prototype.TryIndex(profile.Species, out var species))
            return null;

        var dummy = Spawn(species.DollPrototype, MapCoordinates.Nullspace);
        _visualBody.ApplyProfileTo(dummy, profile);
        _humanoidProfile.ApplyProfileTo(dummy, profile);
        _sunriseProfile.ApplyProfileTo(dummy, profile);
        return dummy;
    }

    private void EquipValidationDummyForJob(
        ICommonSession session,
        HumanoidCharacterProfile profile,
        EntityUid dummy,
        JobPrototype job)
    {
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(job.ID);
        var effectiveJobLoadoutId = LoadoutSystem.GetEffectiveRolePrototype(jobLoadoutId, _prototype);
        if (_prototype.TryIndex(effectiveJobLoadoutId, out var roleProto))
        {
            var loadout = GetRoleLoadoutForJob(session, profile, job.ID);
            _spawn.EquipRoleLoadout(dummy, loadout, roleProto);
        }

        if (job.StartingGear != null)
            _spawn.EquipStartingGear(dummy, job.StartingGear, raiseEvent: false);
    }

    private RoleLoadout GetRoleLoadoutForJob(
        ICommonSession session,
        HumanoidCharacterProfile profile,
        string jobId)
    {
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(jobId);
        var loadout = profile.Loadouts.TryGetValue(jobLoadoutId, out var existing)
            ? existing.Clone()
            : new RoleLoadout(jobLoadoutId);
        loadout.SetDefault(profile, session, _prototype);
        return loadout;
    }

    private void ClearValidationDummyEquipment(EntityUid dummy)
    {
        if (TryComp<InventoryComponent>(dummy, out var inventory))
        {
            var slots = _inventory.GetSlotEnumerator((dummy, inventory));
            while (slots.NextItem(out _, out var slot))
            {
                if (_inventory.TryUnequip(
                        dummy,
                        slot.Name,
                        out var removed,
                        silent: true,
                        force: true,
                        inventory: inventory,
                        reparent: false))
                {
                    QueueDel(removed.Value);
                }
            }
        }

        if (!TryComp<HandsComponent>(dummy, out var hands))
            return;

        foreach (var handId in hands.Hands.Keys)
        {
            if (!_container.TryGetContainer(dummy, handId, out var container))
                continue;

            foreach (var held in container.ContainedEntities.ToArray())
            {
                if (!_container.Remove(held, container, reparent: false, force: true))
                    continue;

                QueueDel(held);
            }
        }
    }

    private static SunriseInventorySelection GetJobSpecificSelection(
        SunriseInventorySelection requestedSelection,
        SunriseInventorySelection validEffectiveSelection)
    {
        var selection = new SunriseInventorySelection();

        foreach (var (slot, itemId) in requestedSelection.SlotItems)
        {
            if (validEffectiveSelection.SlotItems.TryGetValue(slot, out var validItemId) &&
                validItemId == itemId)
            {
                selection.SlotItems[slot] = itemId;
            }
        }

        var validBagItems = validEffectiveSelection.BagItems.ToHashSet();
        foreach (var itemId in requestedSelection.BagItems)
        {
            if (!validBagItems.Remove(itemId))
                continue;

            selection.BagItems.Add(itemId);
        }

        return selection;
    }
}
