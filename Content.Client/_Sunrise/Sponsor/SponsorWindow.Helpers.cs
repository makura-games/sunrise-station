using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    [Dependency] private IResourceCache _resource = default!;
    private const int StoreDetailsPreviewSize = 112;
    private const int StoreDetailsTierLineLength = 32;
    private const int StoreDetailsTierLines = 2;
    private const int PurchasePreviewSize = 112;
    private const int SubscriptionCardMinHeight = 360;
    private const int AccountValueLines = 2;
    private const int PurchaseDetailsLineLength = 34;
    private const int PurchaseDetailsLines = 2;
    private const int StoreStatusLineLength = 82;
    private const int StoreStatusLines = 3;
    private const int BenefitMarkerFontSize = 14;

    private Font? _benefitMarkerFont;

    private string GetPriceText(int price)
    {
        if (price <= 0)
            return Loc.GetString("donation-terminal-inventory-free");

        return Loc.GetString("donation-terminal-inventory-price", ("price", price));
    }

    private void SetCurrencyPriceRow(Label label, TextureRect icon, Label valueLabel, int price)
    {
        if (price <= 0)
        {
            label.Text = Loc.GetString("donation-terminal-inventory-free");
            icon.Visible = false;
            valueLabel.Text = string.Empty;
            return;
        }

        label.Text = Loc.GetString("donation-terminal-inventory-price-label");
        icon.Visible = true;
        valueLabel.Text = price.ToString();
    }

    private Font GetBenefitMarkerFont()
    {
        return _benefitMarkerFont ??= _resource.GetFont(
            [
                "/Fonts/NotoSans/NotoSans-Regular.ttf",
                "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
                "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
            ],
            BenefitMarkerFontSize);
    }
}
