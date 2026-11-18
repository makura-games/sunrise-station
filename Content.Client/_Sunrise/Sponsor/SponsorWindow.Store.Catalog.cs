using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Sunrise.Sheetlets;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Shared._Sunrise.Helpers;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Roles;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void PopulateStoreCategories()
    {
        StoreCategoryList.RemoveAllChildren();
        _storeCategoryButtons.Clear();

        AddStoreCategoryButton(StoreCatalogCategory.Items, "donation-terminal-shop-category-items");
        AddStoreCategoryButton(StoreCatalogCategory.Packs, "donation-terminal-shop-category-packs");
        UpdateStoreCategoryButtons();
    }

    private void AddStoreCategoryButton(StoreCatalogCategory category, string locKey)
    {
        var button = new Button
        {
            Text = Loc.GetString(locKey),
            HorizontalExpand = true,
            StyleClasses =
            {
                SunriseStyleClass.StyleClassSciFiPanelButton,
            },
        };

        button.OnPressed += _ =>
        {
            _storeCategory = category;
            _selectedStoreEntry = null;
            UpdateStoreCategoryButtons();
            UpdateStoreFilterButtons();
            RefreshStoreCatalog();
        };

        _storeCategoryButtons[category] = button;
        StoreCategoryList.AddChild(button);
    }

    private void UpdateStoreCategoryButtons()
    {
        foreach (var (category, button) in _storeCategoryButtons)
        {
            if (category == _storeCategory)
                button.AddStyleClass(SunriseStyleClass.StyleClassSciFiPanelButtonActive);
            else
                button.RemoveStyleClass(SunriseStyleClass.StyleClassSciFiPanelButtonActive);
        }
    }

    private void PopulateStoreFilters()
    {
        StoreFilterList.RemoveAllChildren();
        _storeFilterButtons.Clear();

        AddStoreFilterButton(StoreCatalogFilter.All, "donation-terminal-shop-filter-all");
        AddStoreFilterButton(StoreCatalogFilter.Purchasable, "donation-terminal-shop-filter-purchasable");
        AddStoreFilterButton(StoreCatalogFilter.Owned, "donation-terminal-shop-filter-owned");
        UpdateStoreFilterButtons();
    }

    private void AddStoreFilterButton(StoreCatalogFilter filter, string locKey)
    {
        var button = new Button
        {
            Text = Loc.GetString(locKey),
            HorizontalExpand = true,
            StyleClasses =
            {
                SunriseStyleClass.StyleClassSciFiPanelButton,
            },
        };

        button.OnPressed += _ =>
        {
            _storeFilter = filter;
            UpdateStoreFilterButtons();
            RefreshStoreCatalog();
        };

        _storeFilterButtons[filter] = button;
        StoreFilterList.AddChild(button);
    }

    private void UpdateStoreFilterButtons()
    {
        if (_storeFilterButtons.TryGetValue(StoreCatalogFilter.All, out var allButton))
        {
            allButton.Text = Loc.GetString(_storeCategory == StoreCatalogCategory.Packs
                ? "donation-terminal-shop-filter-all-packs"
                : "donation-terminal-shop-filter-all");
        }

        foreach (var (filter, button) in _storeFilterButtons)
        {
            if (filter == _storeFilter)
                button.AddStyleClass(SunriseStyleClass.StyleClassSciFiPanelButtonActive);
            else
                button.RemoveStyleClass(SunriseStyleClass.StyleClassSciFiPanelButtonActive);
        }
    }

    private void RefreshStoreCatalog()
    {
        _storeEntries.Clear();
        StoreCatalogGrid.RemoveAllChildren();
        var previousSelectedKey = _selectedStoreEntry?.Key;

        var config = _sponsorInventory.GetSponsorInventoryConfig();
        var purchasedItems = new HashSet<string>(_sponsorInventory.GetPurchasedInventoryItems());
        var sponsorTier = _sponsorInventory.GetSponsorTier();
        var entitlements = new HashSet<string>(_sponsorInventory.GetSponsorInventoryEntitlements());
        var itemsById = BuildStoreItemsById(config);

        if (_storeCategory == StoreCatalogCategory.Items)
        {
            foreach (var item in config.Items ?? [])
            {
                var entry = BuildStoreEntry(item, purchasedItems, sponsorTier, entitlements);
                AddVisibleStoreEntry(entry);
            }
        }
        else
        {
            foreach (var pack in config.Packs ?? [])
            {
                var entry = BuildStorePackEntry(pack, itemsById, purchasedItems, sponsorTier, entitlements);
                AddVisibleStoreEntry(entry);
            }
        }

        _storeEntries.Sort(CompareStoreEntries);
        _selectedStoreEntry = GetVisibleStoreEntry(previousSelectedKey);

        for (var i = 0; i < _storeEntries.Count; i++)
        {
            var card = CreateStoreCard(_storeEntries[i]);
            StoreCatalogGrid.AddChild(card);
        }

        StoreEmptyLabel.Visible = _storeEntries.Count == 0;
        StoreEmptyLabel.Text = Loc.GetString(_storeCategory == StoreCatalogCategory.Packs
            ? "donation-terminal-shop-empty-packs"
            : "donation-terminal-shop-empty");
        StoreCatalogTitleLabel.Text = Loc.GetString(_storeCategory == StoreCatalogCategory.Packs
            ? "donation-terminal-shop-catalog-packs"
            : "donation-terminal-shop-catalog");
        StoreCatalogCountLabel.Text = Loc.GetString("donation-terminal-inventory-count", ("count", _storeEntries.Count));
        RefreshStoreDetails();
    }

    private Dictionary<string, SponsorInventoryItemInfo> BuildStoreItemsById(SponsorInventoryConfig config)
    {
        var itemsById = new Dictionary<string, SponsorInventoryItemInfo>();
        foreach (var item in config.Items ?? [])
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;

            itemsById[item.Id] = item;
        }

        return itemsById;
    }

    private void AddVisibleStoreEntry(SponsorStoreEntry? entry)
    {
        if (entry == null)
            return;

        if (!EntryMatchesSearch(entry, StoreSearch.Text) ||
            !EntryMatchesFilter(entry))
        {
            return;
        }

        _storeEntries.Add(entry);
    }

    private SponsorStoreEntry? BuildStoreEntry(
        SponsorInventoryItemInfo? item,
        HashSet<string> purchasedItems,
        int sponsorTier,
        HashSet<string> entitlements)
    {
        if (item == null)
            return null;

        if (!item.Purchasable)
            return null;

        if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.EntityPrototype))
            return null;

        if (!_prototype.TryIndex<EntityPrototype>(item.EntityPrototype, out var entityPrototype))
            return null;

        var owned = purchasedItems.Contains(item.Id);
        var temporaryAccessGranted = SunriseInventoryValidation.HasSponsorAccess(
            item,
            sponsorTier,
            entitlements);
        var requiresSpecialAccess = item.Access.Entitlements is { Length: > 0 };
        var details = BuildStoreDetails(item, entityPrototype, owned, temporaryAccessGranted);

        return new SponsorStoreEntry(
            SponsorStoreEntryKind.Item,
            item.Id,
            null,
            entityPrototype.Name,
            entityPrototype.Description,
            item.EntityPrototype,
            [item.EntityPrototype],
            item.Access.Tier?.Value,
            item.Access.Tier != null,
            item.Access.Tier?.Inherit ?? true,
            item.Price,
            owned,
            requiresSpecialAccess,
            details,
            owned ? 1 : 0,
            1);
    }

    private SponsorStoreEntry? BuildStorePackEntry(
        SponsorInventoryPackInfo? pack,
        Dictionary<string, SponsorInventoryItemInfo> itemsById,
        HashSet<string> purchasedItems,
        int sponsorTier,
        HashSet<string> entitlements)
    {
        var packItemIds = pack?.InventoryItemIds;
        if (pack == null)
            return null;

        if (string.IsNullOrWhiteSpace(pack.Id))
            return null;

        if (packItemIds is not { Length: > 0 })
            return null;

        var itemNames = new List<string>();
        var previewPrototypes = new List<string>();
        var uniqueItemIds = new HashSet<string>();
        int? minimumTier = null;
        int? exactTier = null;
        var exactTierConflict = false;
        var ownedCount = 0;
        var temporaryAccessGranted = true;
        var requiresSpecialAccess = false;

        foreach (var itemId in packItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !uniqueItemIds.Add(itemId))
                continue;

            if (!itemsById.TryGetValue(itemId, out var item))
                return null;

            if (!item.Purchasable)
                return null;

            if (item.Access.Tier is { } tierAccess)
            {
                if (tierAccess.Inherit)
                {
                    minimumTier = Math.Max(minimumTier ?? 0, tierAccess.Value);
                }
                else if (exactTier == null)
                {
                    exactTier = tierAccess.Value;
                }
                else if (exactTier != tierAccess.Value)
                {
                    exactTierConflict = true;
                }
            }

            if (!SunriseInventoryValidation.HasSponsorAccess(item, sponsorTier, entitlements))
                temporaryAccessGranted = false;

            requiresSpecialAccess |= item.Access.Entitlements is { Length: > 0 };

            if (purchasedItems.Contains(itemId))
                ownedCount++;

            if (_prototype.TryIndex<EntityPrototype>(item.EntityPrototype, out var entityPrototype))
            {
                itemNames.Add(entityPrototype.Name);
                previewPrototypes.Add(item.EntityPrototype);
            }
            else
            {
                itemNames.Add(itemId);
            }
        }

        var totalItemCount = uniqueItemIds.Count;
        if (totalItemCount == 0 || previewPrototypes.Count == 0)
            return null;

        var previewPrototype = GetValidPreviewPrototype(pack.PreviewEntityPrototype);
        var name = string.IsNullOrWhiteSpace(pack.Name)
            ? Loc.GetString("donation-terminal-inventory-pack-name", ("pack", FormatPackId(pack.Id)))
            : pack.Name;
        var description = string.IsNullOrWhiteSpace(pack.Description)
            ? Loc.GetString("donation-terminal-inventory-pack-description", ("count", totalItemCount))
            : pack.Description;
        var packDescriptionLines = new string[]
        {
            description,
            Loc.GetString(
                "donation-terminal-inventory-pack-owned",
                ("owned", ownedCount),
                ("total", totalItemCount)),
            Loc.GetString("donation-terminal-inventory-pack-items", ("items", string.Join(", ", itemNames))),
        };
        var packDescription = string.Join("\n", packDescriptionLines);
        var owned = ownedCount >= totalItemCount;
        var requiresTierAccess = minimumTier != null || exactTier != null;
        int? requiredTier = null;
        var requiredTierInherited = true;
        if (!exactTierConflict)
        {
            if (exactTier is { } exactTierValue && exactTierValue >= (minimumTier ?? 0))
            {
                requiredTier = exactTierValue;
                requiredTierInherited = false;
            }
            else if (exactTier == null)
            {
                requiredTier = minimumTier;
            }
        }

        var details = BuildStorePackDetails(
            pack,
            description,
            itemNames,
            requiredTier,
            requiresTierAccess,
            requiredTierInherited,
            ownedCount,
            totalItemCount,
            owned,
            temporaryAccessGranted,
            requiresSpecialAccess);

        return new SponsorStoreEntry(
            SponsorStoreEntryKind.Pack,
            null,
            pack.Id,
            name,
            packDescription,
            previewPrototype,
            previewPrototypes.ToArray(),
            requiredTier,
            requiresTierAccess,
            requiredTierInherited,
            pack.Price,
            owned,
            requiresSpecialAccess,
            details,
            ownedCount,
            totalItemCount);
    }

    private string BuildStoreDetails(
        SponsorInventoryItemInfo item,
        EntityPrototype entityPrototype,
        bool owned,
        bool temporaryAccessGranted)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(entityPrototype.Description))
            lines.Add(entityPrototype.Description);

        lines.Add(GetPriceText(item.Price));

        if (item.Access.Tier is { } tierAccess)
        {
            lines.Add(Loc.GetString(
                tierAccess.Inherit
                    ? "donation-terminal-inventory-tier"
                    : "donation-terminal-inventory-tier-exact",
                ("tier", tierAccess.Value)));
        }

        if (item.Access.Entitlements is { Length: > 0 })
            lines.Add(Loc.GetString("donation-terminal-inventory-special-access"));

        if (!owned &&
            (item.Access.Tier != null || item.Access.Entitlements is { Length: > 0 }) &&
            !temporaryAccessGranted)
        {
            lines.Add(Loc.GetString("donation-terminal-inventory-temporary-access-unavailable"));
        }

        var usageJobs = item.Usage?.Jobs ?? [];
        var usageDepartments = item.Usage?.Departments ?? [];
        var excludedUsageJobs = item.Usage?.ExcludeJobs ?? [];
        var jobs = GetJobListText(usageJobs);
        if (!string.IsNullOrWhiteSpace(jobs))
            lines.Add(Loc.GetString("donation-terminal-inventory-jobs", ("jobs", jobs)));

        var departments = GetDepartmentListText(usageDepartments);
        if (!string.IsNullOrWhiteSpace(departments))
        {
            lines.Add(Loc.GetString(
                "donation-terminal-inventory-departments",
                ("departments", departments)));
        }

        var excludedJobs = GetJobListText(excludedUsageJobs);
        if (!string.IsNullOrWhiteSpace(excludedJobs))
        {
            lines.Add(Loc.GetString(
                "donation-terminal-inventory-excluded-jobs",
                ("jobs", excludedJobs)));
        }

        if (owned)
            lines.Add(Loc.GetString("donation-terminal-owned"));

        return string.Join("\n", lines);
    }

    private string BuildStorePackDetails(
        SponsorInventoryPackInfo pack,
        string description,
        List<string> itemNames,
        int? accessTier,
        bool requiresTierAccess,
        bool accessTierInherited,
        int ownedCount,
        int totalItemCount,
        bool owned,
        bool temporaryAccessGranted,
        bool requiresSpecialAccess)
    {
        var lines = new List<string>
        {
            description,
            GetPriceText(pack.Price),
            Loc.GetString(
                "donation-terminal-inventory-pack-owned",
                ("owned", ownedCount),
                ("total", totalItemCount)),
            Loc.GetString("donation-terminal-inventory-pack-items", ("items", string.Join(", ", itemNames))),
        };

        if (accessTier != null)
        {
            lines.Add(Loc.GetString(
                accessTierInherited
                    ? "donation-terminal-inventory-tier"
                    : "donation-terminal-inventory-tier-exact",
                ("tier", accessTier.Value)));
        }
        else if (requiresTierAccess)
        {
            lines.Add(Loc.GetString("donation-terminal-inventory-tier-specific"));
        }

        if (requiresSpecialAccess)
            lines.Add(Loc.GetString("donation-terminal-inventory-special-access"));

        if (!owned &&
            (requiresTierAccess || requiresSpecialAccess) &&
            !temporaryAccessGranted)
        {
            lines.Add(Loc.GetString("donation-terminal-inventory-temporary-access-unavailable"));
        }

        if (owned)
            lines.Add(Loc.GetString("donation-terminal-owned"));

        return string.Join("\n", lines);
    }

    private string? GetValidPreviewPrototype(string? prototype)
    {
        if (string.IsNullOrWhiteSpace(prototype) ||
            !_prototype.HasIndex<EntityPrototype>(prototype))
        {
            return null;
        }

        return prototype;
    }

    private static string FormatPackId(string packId)
    {
        if (string.IsNullOrWhiteSpace(packId))
            return packId;

        var words = packId.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (word.Length == 0)
                continue;

            words[i] = string.Concat(char.ToUpperInvariant(word[0]).ToString(), word.Substring(1));
        }

        return string.Join(" ", words);
    }

    private string GetJobListText(string[]? jobIds)
    {
        if (jobIds is not { Length: > 0 })
            return string.Empty;

        var jobNames = new List<string>();
        foreach (var jobId in jobIds)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                continue;

            if (_prototype.TryIndex<JobPrototype>(jobId, out var jobPrototype))
                jobNames.Add(jobPrototype.LocalizedName);
            else
                jobNames.Add(jobId);
        }

        jobNames.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        return string.Join(", ", jobNames);
    }

    private string GetDepartmentListText(string[]? departmentIds)
    {
        if (departmentIds is not { Length: > 0 })
            return string.Empty;

        var departmentNames = new List<string>();
        foreach (var departmentId in departmentIds)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
                continue;

            if (_prototype.TryIndex<DepartmentPrototype>(departmentId, out var departmentPrototype))
                departmentNames.Add(Loc.GetString(departmentPrototype.Name));
            else
                departmentNames.Add(departmentId);
        }

        departmentNames.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        return string.Join(", ", departmentNames);
    }

    private bool EntryMatchesSearch(SponsorStoreEntry entry, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               entry.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               entry.DetailsText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               entry.Key.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool EntryMatchesFilter(SponsorStoreEntry entry)
    {
        return _storeFilter switch
        {
            StoreCatalogFilter.Purchasable => CanBuyStoreEntry(entry),
            StoreCatalogFilter.Owned => entry.Owned,
            _ => true,
        };
    }

    private int CompareStoreEntries(SponsorStoreEntry left, SponsorStoreEntry right)
    {
        var comparison = left.Owned.CompareTo(right.Owned);
        if (comparison != 0)
            return comparison;

        comparison = left.Price.CompareTo(right.Price);
        if (comparison != 0)
            return comparison;

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private Control CreateStoreCard(SponsorStoreEntry entry)
    {
        var selected = _selectedStoreEntry?.Kind == entry.Kind &&
                       _selectedStoreEntry?.Key == entry.Key;
        var buyText = entry.Owned
            ? Loc.GetString("donation-terminal-owned-short")
            : GetStoreBuyButtonText();

        if (entry.Kind == SponsorStoreEntryKind.Pack)
        {
            var packCard = new SponsorStorePackCard(
                entry.PreviewPrototype,
                entry.ContentPreviewPrototypes,
                entry.Name,
                entry.Price,
                buyText,
                !CanBuyStoreEntry(entry),
                selected,
                entry.Owned,
                entry.OwnedItemCount,
                entry.TotalItemCount);
            packCard.OnSelected += () => SelectStoreEntry(entry);
            packCard.OnBuyPressed += () => ShowPurchaseConfirmation(entry);
            return packCard;
        }

        var itemPreviewPrototype = entry.PreviewPrototype;
        if (string.IsNullOrWhiteSpace(itemPreviewPrototype) && entry.ContentPreviewPrototypes.Length > 0)
            itemPreviewPrototype = entry.ContentPreviewPrototypes[0];

        if (string.IsNullOrWhiteSpace(itemPreviewPrototype))
            return new Control();

        var card = new SponsorStoreItemCard(
            itemPreviewPrototype,
            entry.Name,
            entry.Price,
            buyText,
            !CanBuyStoreEntry(entry),
            selected,
            entry.Owned);
        card.OnSelected += () => SelectStoreEntry(entry);
        card.OnBuyPressed += () => ShowPurchaseConfirmation(entry);
        return card;
    }

    private SponsorStoreEntry? GetVisibleStoreEntry(string? key)
    {
        if (_storeEntries.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var entry in _storeEntries)
            {
                if (entry.Key == key)
                    return entry;
            }
        }

        return _storeEntries[0];
    }

    private void SelectStoreEntry(SponsorStoreEntry entry)
    {
        if (_selectedStoreEntry?.Kind == entry.Kind &&
            _selectedStoreEntry?.Key == entry.Key)
        {
            return;
        }

        _selectedStoreEntry = entry;
        RefreshStoreCatalog();
    }

    private void RefreshStoreDetails()
    {
        StoreDetailsPreviewHost.RemoveAllChildren();
        StoreDetailsTitleLabel.Text = Loc.GetString(_storeCategory == StoreCatalogCategory.Packs
            ? "donation-terminal-shop-details-pack-title"
            : "donation-terminal-shop-details-title");
        StoreDetailsEmptyLabel.Text = Loc.GetString(_storeCategory == StoreCatalogCategory.Packs
            ? "donation-terminal-shop-details-pack-placeholder"
            : "donation-terminal-shop-details-placeholder");

        var entry = _selectedStoreEntry;
        if (entry == null)
        {
            StoreDetailsContent.Visible = false;
            StoreDetailsEmptyLabel.Visible = true;
            StoreDetailsBuyButton.Disabled = true;
            return;
        }

        StoreDetailsEmptyLabel.Visible = false;
        StoreDetailsContent.Visible = true;
        StoreDetailsNameLabel.SetMessage(FormattedMessage.FromUnformatted(entry.Name), SciFiPalette.Text);
        StoreDetailsNameLabel.ToolTip = entry.Name;
        SetCurrencyPriceRow(StoreDetailsPriceLabel, StoreDetailsPriceIcon, StoreDetailsPriceValueLabel, entry.Price);
        StoreDetailsTierValue.Text = GetStoreRequiredLevelText(entry)
            .WrapText(StoreDetailsTierLineLength, StoreDetailsTierLines);
        StoreDetailsTierValue.ToolTip = StoreDetailsTierValue.Text;
        StoreDetailsDescriptionLabel.SetMessage(
            FormattedMessage.FromUnformatted(string.IsNullOrWhiteSpace(entry.Description)
                ? Loc.GetString("donation-terminal-unavailable")
                : entry.Description),
            SciFiPalette.TextMuted);
        StoreDetailsBuyButton.Text = entry.Owned
            ? Loc.GetString("donation-terminal-owned")
            : GetStoreBuyButtonText();
        StoreDetailsBuyButton.Disabled = !CanBuyStoreEntry(entry);

        AddStoreEntryPreview(StoreDetailsPreviewHost, entry, StoreDetailsPreviewSize, 4f, true);
    }

    private void AddStoreEntryPreview(
        Control host,
        SponsorStoreEntry entry,
        int previewSize,
        float scale,
        bool showPackContents)
    {
        if (entry.Kind == SponsorStoreEntryKind.Pack &&
            showPackContents &&
            entry.ContentPreviewPrototypes.Length > 1)
        {
            var grid = new GridContainer
            {
                Columns = 2,
                HSeparationOverride = 4,
                VSeparationOverride = 4,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };

            foreach (var prototype in entry.ContentPreviewPrototypes)
            {
                var itemPreview = new EntityPrototypeView
                {
                    SetSize = new Vector2(50, 50),
                    Scale = new Vector2(2, 2),
                    OverrideDirection = Direction.South,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                };
                itemPreview.SetPrototype(prototype);
                grid.AddChild(itemPreview);
            }

            host.AddChild(new ScrollContainer
            {
                SetSize = new Vector2(previewSize, previewSize),
                HScrollEnabled = false,
                VScrollEnabled = true,
                Children =
                {
                    grid,
                },
            });
            return;
        }

        var previewPrototype = entry.PreviewPrototype;
        if (string.IsNullOrWhiteSpace(previewPrototype) && entry.ContentPreviewPrototypes.Length > 0)
            previewPrototype = entry.ContentPreviewPrototypes[0];

        if (string.IsNullOrWhiteSpace(previewPrototype))
            return;

        var preview = new EntityPrototypeView
        {
            SetSize = new Vector2(previewSize, previewSize),
            Scale = new Vector2(scale, scale),
            OverrideDirection = Direction.South,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        preview.SetPrototype(previewPrototype);
        host.AddChild(preview);
    }

    private string GetStoreRequiredLevelText(SponsorStoreEntry entry)
    {
        if (entry.AccessTier == null)
        {
            if (entry.RequiresTierAccess)
            {
                return entry.RequiresSpecialAccess
                    ? Loc.GetString("donation-terminal-inventory-tier-specific-or-special-access")
                    : Loc.GetString("donation-terminal-inventory-tier-specific");
            }

            return entry.RequiresSpecialAccess
                ? Loc.GetString("donation-terminal-inventory-special-access")
                : Loc.GetString("donation-terminal-shop-details-no-level");
        }

        var tierInfo = GetSponsorTier(entry.AccessTier.Value);
        var tierText = tierInfo == null
            ? Loc.GetString("donation-terminal-sponsor-tier", ("tier", entry.AccessTier.Value))
            : GetSponsorTierName(tierInfo);
        if (!entry.RequiresSpecialAccess)
            return entry.AccessTierInherited
                ? tierText
                : Loc.GetString("donation-terminal-inventory-tier-exact-short", ("tier", tierText));

        return Loc.GetString(
            entry.AccessTierInherited
                ? "donation-terminal-inventory-tier-or-special-access"
                : "donation-terminal-inventory-tier-exact-or-special-access",
            ("tier", tierText));
    }

    private string GetStoreBuyButtonText()
    {
        return Loc.GetString("donation-terminal-buy");
    }

    private bool CanBuyStoreEntry(SponsorStoreEntry entry)
    {
        if (entry.Owned)
            return false;

        var balance = _sponsorInventory.GetBalance();
        return balance != null && balance.Value >= entry.Price;
    }
}
