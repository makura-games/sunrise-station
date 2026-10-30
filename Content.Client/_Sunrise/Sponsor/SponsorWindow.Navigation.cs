using System;
using Content.Client._Sunrise.Sheetlets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void OnAccountFundsLinkChanged(string url)
    {
        _accountFundsLink = url;
        BalanceButton.Disabled = string.IsNullOrWhiteSpace(url);
    }

    private void OnSponsorDonateUrlTemplateChanged(string urlTemplate)
    {
        _sponsorDonateUrlTemplate = urlTemplate;
        RefreshSponsorDonateUrl();
    }

    private void OnAccountManagementUrlChanged(string url)
    {
        _accountManagementUrl = url;
        MainAccountManageButton.Disabled = string.IsNullOrWhiteSpace(url);
    }

    private void OpenAccountFundsLink()
    {
        if (string.IsNullOrWhiteSpace(_accountFundsLink))
            return;

        _uri.OpenUri(_accountFundsLink);
    }

    private void RefreshSponsorDonateUrl()
    {
        _sponsorDonateUrl = BuildSponsorDonateUrl(_sponsorsManager?.GetSponsorProjectName());
        var disabled = string.IsNullOrWhiteSpace(_sponsorDonateUrl);
        MainSubscriptionPurchaseButton.Disabled = disabled;
        SubscriptionPurchaseButton.Disabled = disabled;
        SubscriptionDetailsPurchaseButton.Disabled = disabled;
    }

    private string BuildSponsorDonateUrl(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(_sponsorDonateUrlTemplate))
            return string.Empty;

        var donateUrl = _sponsorDonateUrlTemplate.Trim();
        var hasProjectPlaceholder =
            donateUrl.Contains(SponsorDonateProjectPlaceholder, StringComparison.OrdinalIgnoreCase);

        if (!hasProjectPlaceholder)
            return donateUrl;

        if (string.IsNullOrWhiteSpace(projectName))
            return string.Empty;

        var escapedProjectName = Uri.EscapeDataString(projectName.Trim());
        return donateUrl
            .Replace(SponsorDonateProjectPlaceholder, escapedProjectName, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenSponsorDonateLink()
    {
        if (string.IsNullOrWhiteSpace(_sponsorDonateUrl))
            RefreshSponsorDonateUrl();

        if (string.IsNullOrWhiteSpace(_sponsorDonateUrl))
            return;

        _uri.OpenUri(_sponsorDonateUrl);
    }

    private void OpenAccountManagementLink()
    {
        if (string.IsNullOrWhiteSpace(_accountManagementUrl))
            return;

        _uri.OpenUri(_accountManagementUrl);
    }

    private void SelectTab(DonationTerminalTab tab)
    {
        if (SubscriptionDetailsContent.Visible)
        {
            SubscriptionDetailsBenefitsList.RemoveAllChildren();
            ClearSponsorTierDetailsPreviews();
        }

        _selectedTab = tab;

        MainContent.Visible = tab == DonationTerminalTab.Main;
        ShopContent.Visible = tab == DonationTerminalTab.Shop;
        SubscriptionsContent.Visible = tab == DonationTerminalTab.Subscriptions;
        SubscriptionDetailsContent.Visible = false;
        PurchaseConfirmationContent.Visible = false;

        switch (tab)
        {
            case DonationTerminalTab.Main:
                Footer.Text = Loc.GetString("donation-terminal-footer-main");
                break;
            case DonationTerminalTab.Shop:
                Footer.Text = Loc.GetString("donation-terminal-footer-shop");
                break;
            case DonationTerminalTab.Subscriptions:
                Footer.Text = Loc.GetString("donation-terminal-footer-subs");
                break;
        }

        SetTabActive(MainTabButton, tab == DonationTerminalTab.Main);
        SetTabActive(ShopTabButton, tab == DonationTerminalTab.Shop);
        SetTabActive(SubscriptionsTabButton, tab == DonationTerminalTab.Subscriptions);
    }

    private static void SetTabActive(Button button, bool active)
    {
        if (active)
            button.AddStyleClass(SunriseStyleClass.StyleClassSciFiTabActive);
        else
            button.RemoveStyleClass(SunriseStyleClass.StyleClassSciFiTabActive);
    }
}
