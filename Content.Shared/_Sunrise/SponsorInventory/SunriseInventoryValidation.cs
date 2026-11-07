using System.Collections.Generic;
using System.Linq;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.SponsorInventory;

/// <summary>
/// Shared validation for sponsor inventory profile data received from clients or external sponsor services.
/// </summary>
public static class SunriseInventoryValidation
{
    private const int MaxJobSelectionCount = 128;
    private const int MaxSlotSelectionCount = 64;
    private const int MaxBagSelectionCount = 128;
    private const int MaxPrototypeIdLength = 128;
    private const int MaxSlotIdLength = 64;

    /// <summary>
    /// Validates a sponsor inventory profile against server-side sponsor ownership data for a session.
    /// </summary>
    public static SunriseInventoryProfile EnsureValid(
        SunriseInventoryProfile profile,
        ICommonSession session,
        IPrototypeManager prototype,
        ISharedSponsorsManager? sponsors)
    {
        if (sponsors == null)
            return new SunriseInventoryProfile();

        var config = sponsors.GetSponsorInventoryConfig();
        if (config == null || config.Items is not { Length: > 0 })
            return new SunriseInventoryProfile();

        var purchasedItems = GetPurchasedItems(session, sponsors);
        var sponsorTier = sponsors.GetSponsorTier(session.UserId);
        var entitlements = sponsors.GetSponsorInventoryEntitlements(session.UserId);

        return EnsureValid(profile, prototype, config, purchasedItems, sponsorTier, entitlements);
    }

    /// <summary>
    /// Validates a sponsor inventory profile against an already loaded catalog and ownership snapshot.
    /// </summary>
    public static SunriseInventoryProfile EnsureValid(
        SunriseInventoryProfile profile,
        IPrototypeManager prototype,
        SponsorInventoryConfig config,
        IEnumerable<string> purchasedItems,
        int sponsorTier,
        IEnumerable<string> entitlements)
    {
        profile ??= new SunriseInventoryProfile();

        if (config == null || config.Items is not { Length: > 0 })
            return new SunriseInventoryProfile();

        var items = new Dictionary<string, SponsorInventoryItemInfo>();
        foreach (var item in config.Items)
        {
            if (item == null || !IsReasonableId(item.Id, MaxPrototypeIdLength))
                continue;

            items[item.Id] = item;
        }

        if (items.Count == 0)
            return new SunriseInventoryProfile();

        var purchased = new HashSet<string>();
        foreach (var purchasedItem in purchasedItems ?? [])
        {
            if (IsReasonableId(purchasedItem, MaxPrototypeIdLength))
                purchased.Add(purchasedItem);
        }

        var entitlementSet = GetValidEntitlements(entitlements);

        var valid = new SunriseInventoryProfile
        {
            Global = EnsureValidSelection(
                profile.Global ?? new SunriseInventorySelection(),
                null,
                items,
                purchased,
                sponsorTier,
                entitlementSet,
                prototype),
        };

        var checkedJobs = 0;
        foreach (var (jobId, selection) in profile.Jobs ?? new Dictionary<string, SunriseInventorySelection>())
        {
            if (++checkedJobs > MaxJobSelectionCount)
                break;

            if (!IsReasonableId(jobId, MaxPrototypeIdLength) || !prototype.HasIndex<JobPrototype>(jobId))
                continue;

            var validSelection = EnsureValidSelection(
                selection ?? new SunriseInventorySelection(),
                jobId,
                items,
                purchased,
                sponsorTier,
                entitlementSet,
                prototype);

            if (!validSelection.IsEmpty())
                valid.Jobs[jobId] = validSelection;
        }

        return valid;
    }

    /// <summary>
    /// Returns whether a sponsor inventory item may be used by a session for the selected job.
    /// </summary>
    public static bool CanUseItem(
        string inventoryItemId,
        string? jobId,
        ICommonSession session,
        IPrototypeManager prototype,
        ISharedSponsorsManager? sponsors)
    {
        if (sponsors == null || !IsReasonableId(inventoryItemId, MaxPrototypeIdLength))
            return false;

        var config = sponsors.GetSponsorInventoryConfig();
        SponsorInventoryItemInfo? item = null;

        foreach (var inventoryItem in config.Items ?? [])
        {
            if (inventoryItem == null)
                continue;

            if (inventoryItem.Id != inventoryItemId)
                continue;

            item = inventoryItem;
            break;
        }

        if (item == null)
            return false;

        return CanUseItem(
            item,
            jobId,
            GetPurchasedItems(session, sponsors),
            sponsors.GetSponsorTier(session.UserId),
            GetValidEntitlements(sponsors.GetSponsorInventoryEntitlements(session.UserId)),
            prototype);
    }

    /// <summary>
    /// Returns whether a sponsor inventory item may be used with a preloaded catalog and ownership snapshot.
    /// </summary>
    public static bool CanUseItem(
        string inventoryItemId,
        string? jobId,
        IPrototypeManager prototype,
        SponsorInventoryConfig config,
        IEnumerable<string> purchasedItems,
        int sponsorTier,
        IEnumerable<string> entitlements)
    {
        if (config == null || !IsReasonableId(inventoryItemId, MaxPrototypeIdLength))
            return false;

        SponsorInventoryItemInfo? item = null;

        foreach (var inventoryItem in config.Items ?? [])
        {
            if (inventoryItem == null)
                continue;

            if (inventoryItem.Id != inventoryItemId)
                continue;

            item = inventoryItem;
            break;
        }

        var purchased = new HashSet<string>();
        foreach (var purchasedItem in purchasedItems ?? [])
        {
            if (IsReasonableId(purchasedItem, MaxPrototypeIdLength))
                purchased.Add(purchasedItem);
        }

        return item != null &&
               CanUseItem(item, jobId, purchased, sponsorTier, GetValidEntitlements(entitlements), prototype);
    }

    /// <summary>
    /// Проверяет обычный loadout на наличие предметов каталога, требующих владения или наградного доступа.
    /// </summary>
    public static bool CanUseLoadout(
        LoadoutPrototype loadout,
        ICommonSession session,
        IPrototypeManager prototype,
        ISharedSponsorsManager sponsors)
    {
        var config = sponsors.GetSponsorInventoryConfig();
        return CanUseLoadout(
            loadout,
            prototype,
            config,
            GetPurchasedItems(session, sponsors),
            sponsors.GetSponsorTier(session.UserId),
            GetValidEntitlements(sponsors.GetSponsorInventoryEntitlements(session.UserId)));
    }

    /// <summary>
    /// Проверяет обычный loadout по уже загруженному снимку спонсорского инвентаря.
    /// </summary>
    public static bool CanUseLoadout(
        LoadoutPrototype loadout,
        IPrototypeManager prototype,
        SponsorInventoryConfig config,
        IReadOnlySet<string> purchasedItems,
        int sponsorTier,
        IReadOnlySet<string> entitlements)
    {
        if (config.Items is not { Length: > 0 })
            return true;

        foreach (var entityPrototype in GetLoadoutEntityPrototypes(loadout, prototype))
        {
            var catalogItemFound = false;
            var catalogItemAllowed = false;

            foreach (var item in config.Items)
            {
                if (item == null || item.EntityPrototype != entityPrototype)
                    continue;

                catalogItemFound = true;
                if (!CanUseForOwnershipOrSponsorAccess(item, purchasedItems, sponsorTier, entitlements))
                    continue;

                catalogItemAllowed = true;
                break;
            }

            if (catalogItemFound && !catalogItemAllowed)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Merges global sponsor inventory choices with job-specific overrides.
    /// </summary>
    public static SunriseInventorySelection GetEffectiveSelection(SunriseInventoryProfile profile, string? jobId)
    {
        profile ??= new SunriseInventoryProfile();

        var selection = new SunriseInventorySelection();
        CopySelectionLayer(profile.Global ?? new SunriseInventorySelection(), selection);

        if (jobId == null ||
            profile.Jobs == null ||
            !profile.Jobs.TryGetValue(jobId, out var jobSelection) ||
            jobSelection == null)
        {
            return selection;
        }

        CopySelectionLayer(jobSelection, selection);
        return selection;
    }

    private static SunriseInventorySelection EnsureValidSelection(
        SunriseInventorySelection selection,
        string? jobId,
        Dictionary<string, SponsorInventoryItemInfo> items,
        HashSet<string> purchasedItems,
        int sponsorTier,
        HashSet<string> entitlements,
        IPrototypeManager prototype)
    {
        var valid = new SunriseInventorySelection();
        var usedItems = new HashSet<string>();

        var checkedSlotItems = 0;
        foreach (var (slot, itemId) in selection.SlotItems ?? new Dictionary<string, string>())
        {
            if (++checkedSlotItems > MaxSlotSelectionCount)
                break;

            if (!IsReasonableId(slot, MaxSlotIdLength) ||
                !IsReasonableId(itemId, MaxPrototypeIdLength) ||
                usedItems.Contains(itemId) ||
                !items.TryGetValue(itemId, out var item) ||
                !CanUseItem(item, jobId, purchasedItems, sponsorTier, entitlements, prototype))
            {
                continue;
            }

            valid.SlotItems[slot] = itemId;
            usedItems.Add(itemId);
        }

        var checkedBagItems = 0;
        foreach (var itemId in selection.BagItems ?? [])
        {
            if (++checkedBagItems > MaxBagSelectionCount)
                break;

            if (!IsReasonableId(itemId, MaxPrototypeIdLength) ||
                usedItems.Contains(itemId) ||
                !items.TryGetValue(itemId, out var item) ||
                !CanUseItem(item, jobId, purchasedItems, sponsorTier, entitlements, prototype))
            {
                continue;
            }

            valid.BagItems.Add(itemId);
            usedItems.Add(itemId);
        }

        return valid;
    }

    private static void CopySelectionLayer(SunriseInventorySelection source, SunriseInventorySelection target)
    {
        var checkedSlotItems = 0;
        foreach (var (slot, itemId) in source.SlotItems ?? new Dictionary<string, string>())
        {
            if (++checkedSlotItems > MaxSlotSelectionCount)
                break;

            if (!IsReasonableId(slot, MaxSlotIdLength) ||
                !IsReasonableId(itemId, MaxPrototypeIdLength) ||
                target.SlotItems.Count >= MaxSlotSelectionCount && !target.SlotItems.ContainsKey(slot))
            {
                continue;
            }

            target.SlotItems[slot] = itemId;
        }

        var checkedBagItems = 0;
        foreach (var itemId in source.BagItems ?? [])
        {
            if (++checkedBagItems > MaxBagSelectionCount ||
                target.BagItems.Count >= MaxBagSelectionCount)
            {
                break;
            }

            if (IsReasonableId(itemId, MaxPrototypeIdLength))
                target.BagItems.Add(itemId);
        }
    }

    private static bool IsReasonableId(string? id, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(id) && id.Length <= maxLength;
    }

    private static bool CanUseItem(
        SponsorInventoryItemInfo item,
        string? jobId,
        HashSet<string> purchasedItems,
        int sponsorTier,
        HashSet<string> entitlements,
        IPrototypeManager prototype)
    {
        if (!IsReasonableId(item.Id, MaxPrototypeIdLength) ||
            !IsReasonableId(item.EntityPrototype, MaxPrototypeIdLength) ||
            !prototype.HasIndex<EntityPrototype>(item.EntityPrototype))
        {
            return false;
        }

        if (!IsUsageAllowed(item, jobId, prototype))
            return false;

        return CanUseForOwnershipOrSponsorAccess(item, purchasedItems, sponsorTier, entitlements);
    }

    /// <summary>
    /// Возвращает, выдаёт ли активная подписка или entitlement временный доступ к предмету без покупки.
    /// </summary>
    public static bool HasSponsorAccess(
        SponsorInventoryItemInfo item,
        int sponsorTier,
        IReadOnlySet<string> entitlements)
    {
        if (item.Access.Tier is { } tierAccess)
        {
            if (tierAccess.Inherit && sponsorTier >= tierAccess.Value ||
                !tierAccess.Inherit && sponsorTier == tierAccess.Value)
            {
                return true;
            }
        }

        if (item.Access.Entitlements is { Length: > 0 })
        {
            foreach (var entitlement in item.Access.Entitlements)
            {
                if (!IsReasonableId(entitlement, MaxPrototypeIdLength) || !entitlements.Contains(entitlement))
                    return false;
            }

            return true;
        }

        return false;
    }

    private static bool CanUseForOwnershipOrSponsorAccess(
        SponsorInventoryItemInfo item,
        IReadOnlySet<string> purchasedItems,
        int sponsorTier,
        IReadOnlySet<string> entitlements)
    {
        if (purchasedItems.Contains(item.Id))
            return true;

        return HasSponsorAccess(item, sponsorTier, entitlements);
    }

    private static IEnumerable<string> GetLoadoutEntityPrototypes(
        LoadoutPrototype loadout,
        IPrototypeManager prototype)
    {
        foreach (var entityPrototype in GetEquipmentLoadoutEntityPrototypes(loadout))
            yield return entityPrototype;

        if (!prototype.Resolve(loadout.StartingGear, out var startingGear))
            yield break;

        foreach (var entityPrototype in GetEquipmentLoadoutEntityPrototypes(startingGear))
            yield return entityPrototype;
    }

    private static IEnumerable<string> GetEquipmentLoadoutEntityPrototypes(IEquipmentLoadout loadout)
    {
        foreach (var entityPrototype in loadout.Equipment.Values)
            yield return entityPrototype.Id;

        foreach (var entityPrototype in loadout.Inhand)
            yield return entityPrototype.Id;

        foreach (var storedPrototypes in loadout.Storage.Values)
        {
            foreach (var entityPrototype in storedPrototypes)
                yield return entityPrototype.Id;
        }
    }

    /// <summary>
    /// Возвращает, разрешено ли использовать предмет выбранной профессии согласно каталогу.
    /// Разрешающие списки профессий и отделов объединяются по OR, а исключение профессии имеет приоритет.
    /// </summary>
    public static bool IsUsageAllowed(
        SponsorInventoryItemInfo item,
        string? jobId,
        IPrototypeManager prototype)
    {
        var usage = item.Usage;
        var jobs = usage?.Jobs;
        var departments = usage?.Departments;
        var excludedJobs = usage?.ExcludeJobs;
        var hasAllowedJobs = jobs is { Length: > 0 };
        var hasAllowedDepartments = departments is { Length: > 0 };
        var hasExcludedJobs = excludedJobs is { Length: > 0 };

        if (!hasAllowedJobs && !hasAllowedDepartments && !hasExcludedJobs)
            return true;

        if (jobId == null || !IsReasonableId(jobId, MaxPrototypeIdLength))
            return false;

        if (hasExcludedJobs && excludedJobs!.Contains(jobId))
            return false;

        if (hasAllowedJobs && jobs!.Contains(jobId))
            return true;

        if (hasAllowedDepartments)
        {
            foreach (var departmentId in departments!)
            {
                if (!prototype.TryIndex<DepartmentPrototype>(departmentId, out var department))
                    continue;

                if (department.Roles.Contains(jobId))
                    return true;
            }
        }

        return !hasAllowedJobs && !hasAllowedDepartments;
    }

    private static HashSet<string> GetPurchasedItems(ICommonSession session, ISharedSponsorsManager sponsors)
    {
        if (sponsors.TryGetPurchasedInventoryItems(session.UserId, out var serverItems) && serverItems != null)
            return serverItems.ToHashSet();

        return sponsors.GetClientPurchasedInventoryItems()?.ToHashSet() ?? new HashSet<string>();
    }

    private static HashSet<string> GetValidEntitlements(IEnumerable<string>? entitlements)
    {
        var result = new HashSet<string>();
        foreach (var entitlement in entitlements ?? [])
        {
            if (IsReasonableId(entitlement, MaxPrototypeIdLength))
                result.Add(entitlement);
        }

        return result;
    }
}
