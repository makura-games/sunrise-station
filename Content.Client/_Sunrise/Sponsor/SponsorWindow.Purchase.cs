using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Shared._Sunrise.Helpers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private const int PurchaseTextMaxLineLength = 34;
    private const int PurchaseTextMaxLines = 2;
    private void ShowPurchaseConfirmation(SponsorStoreEntry entry)
    {
        _pendingPurchaseEntry = entry;
        _purchaseReturnTab = _selectedTab;
        StoreStatusLabel.Text = string.Empty;
        SetPurchaseStatus(string.Empty, SciFiPalette.Accent);

        MainContent.Visible = false;
        ShopContent.Visible = false;
        SubscriptionsContent.Visible = false;
        SubscriptionDetailsContent.Visible = false;
        PurchaseConfirmationContent.Visible = true;
        Footer.Text = Loc.GetString("donation-terminal-footer-purchase");

        RefreshPurchaseConfirmation();
    }

    private void HidePurchaseConfirmation()
    {
        _pendingPurchaseEntry = null;
        SelectTab(_purchaseReturnTab);
    }

    private void RefreshPurchaseConfirmation()
    {
        if (!PurchaseConfirmationContent.Visible || _pendingPurchaseEntry == null)
            return;

        var entry = _pendingPurchaseEntry;
        PurchasePreviewHost.RemoveAllChildren();
        PurchaseNameLabel.Text = entry.Name.WrapText(PurchaseTextMaxLineLength, PurchaseTextMaxLines);
        PurchaseNameLabel.ToolTip = entry.Name;
        SetCurrencyPriceRow(PurchasePriceLabel, PurchasePriceIcon, PurchasePriceValueLabel, entry.Price);
        AddStoreEntryPreview(PurchasePreviewHost, entry, PurchasePreviewSize, 4f, true);

        var balance = _sponsorInventory.GetBalance();
        var afterBalance = balance - entry.Price;

        var totalText = Loc.GetString("donation-terminal-purchase-total", ("price", entry.Price));
        var balanceText = Loc.GetString(
            "donation-terminal-purchase-balance",
            ("balance", balance?.ToString() ?? Loc.GetString("donation-terminal-unavailable")));
        var afterBalanceText = Loc.GetString(
            "donation-terminal-purchase-after-balance",
            ("balance", afterBalance?.ToString() ?? Loc.GetString("donation-terminal-unavailable")));
        SetPurchaseDetailsValue(PurchaseTotalLabel, totalText);
        SetPurchaseDetailsValue(PurchaseBalanceLabel, balanceText);
        SetPurchaseDetailsValue(PurchaseAfterBalanceLabel, afterBalanceText);

        PurchaseConfirmButton.Disabled = !CanBuyStoreEntry(entry);
    }

    private void ConfirmPendingPurchase()
    {
        if (_pendingPurchaseEntry == null)
            return;

        if (!CanBuyStoreEntry(_pendingPurchaseEntry))
        {
            var reason = _pendingPurchaseEntry.Owned
                ? Loc.GetString("donation-terminal-inventory-already-owned")
                : Loc.GetString("donation-terminal-inventory-not-enough-balance");
            SetPurchaseStatus(reason, Color.Red);
            return;
        }

        PurchaseConfirmButton.Disabled = true;
        SetPurchaseStatus(
            Loc.GetString("donation-terminal-inventory-purchase-sent"),
            SciFiPalette.TextMuted);
        _sponsorInventory.PurchaseInventoryItem(_pendingPurchaseEntry.ItemId, _pendingPurchaseEntry.PackId);
    }

    private void SetPurchaseStatus(string text, Color color)
    {
        PurchaseStatusLabel.SetMessage(FormattedMessage.FromUnformatted(text), color);
    }

    private void SetPurchaseDetailsValue(Label label, string text)
    {
        label.Text = text.TruncateWithEllipsis(PurchaseDetailsLineLength);
        label.ToolTip = text;
    }
}
