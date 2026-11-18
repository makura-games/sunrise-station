using System.Collections.Generic;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Client._Sunrise.SponsorInventory;
using Content.Shared._Sunrise.Helpers;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly List<SponsorStoreEntry> _storeEntries = new();
    private readonly Dictionary<StoreCatalogCategory, Button> _storeCategoryButtons = new();
    private readonly Dictionary<StoreCatalogFilter, Button> _storeFilterButtons = new();
    private SunriseInventorySystem _sponsorInventory = default!;
    private StoreCatalogCategory _storeCategory = StoreCatalogCategory.Items;
    private StoreCatalogFilter _storeFilter = StoreCatalogFilter.All;
    private SponsorStoreEntry? _selectedStoreEntry;
    private SponsorStoreEntry? _pendingPurchaseEntry;
    private DonationTerminalTab _purchaseReturnTab = DonationTerminalTab.Shop;

    private void InitializeSponsorShopUi()
    {
        _sponsorInventory = _entity.System<SunriseInventorySystem>();

        StoreSearch.OnTextChanged += _ => RefreshStoreCatalog();
        StoreDetailsBuyButton.OnPressed += _ =>
        {
            if (_selectedStoreEntry != null)
                ShowPurchaseConfirmation(_selectedStoreEntry);
        };
        PurchaseCancelButton.OnPressed += _ => HidePurchaseConfirmation();
        PurchaseConfirmButton.OnPressed += _ => ConfirmPendingPurchase();
        StoreDetailsBuyButton.TextAlign = Label.AlignMode.Center;

        PopulateStoreCategories();
        PopulateStoreFilters();
    }

    private void SubscribeSponsorShopUi()
    {
        _sponsorInventory.InventoryDataChanged += OnSponsorInventoryDataChanged;
        _sponsorInventory.InventoryPurchaseResultReceived += OnSponsorInventoryPurchaseResult;
    }

    private void UnsubscribeSponsorShopUi()
    {
        _sponsorInventory.InventoryDataChanged -= OnSponsorInventoryDataChanged;
        _sponsorInventory.InventoryPurchaseResultReceived -= OnSponsorInventoryPurchaseResult;
    }

    private void RefreshSponsorShopUi()
    {
        RefreshBalance();
        RefreshStoreCatalog();
        RefreshSubscriptionCards();
        _sponsorInventory.RequestInitialData();
    }

    private void OnSponsorInventoryDataChanged()
    {
        RefreshBalance();
        RefreshStoreCatalog();
        RefreshPurchaseConfirmation();
        RefreshSponsorInfo();
    }

    private void OnSponsorInventoryPurchaseResult(SponsorInventoryPurchaseResult result)
    {
        var text = result.Success
            ? Loc.GetString("donation-terminal-inventory-purchase-success")
            : Loc.GetString(
                "donation-terminal-inventory-purchase-failed",
                ("reason", result.Error ?? Loc.GetString("donation-terminal-unavailable")));

        var color = result.Success ? SciFiPalette.Accent : Color.Red;
        StoreStatusLabel.FontColorOverride = color;
        StoreStatusLabel.Text = text.WrapText(StoreStatusLineLength, StoreStatusLines);
        StoreStatusLabel.ToolTip = text;
        SetPurchaseStatus(text, color);

        if (!result.Success)
        {
            PurchaseConfirmButton.Disabled = _pendingPurchaseEntry == null || !CanBuyStoreEntry(_pendingPurchaseEntry);
            return;
        }

        _pendingPurchaseEntry = null;
        SelectTab(_purchaseReturnTab);
    }

    private void RefreshBalance()
    {
        var balance = _sponsorInventory.GetBalance();
        BalanceLabel.Text = balance?.ToString() ?? Loc.GetString("donation-terminal-unavailable");
    }
}
