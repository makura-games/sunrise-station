using System.Collections.Generic;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void RefreshMainSponsorSummary()
    {
        var active = _currentSponsorTier > 0;
        var sponsorTier = GetSponsorTier(_currentSponsorTier);
        var accent = active ? GetSubscriptionAccentColor(_currentSponsorTier) : Color.Red;

        MainSponsorTierBadgeLabel.Text = active ? SponsorUiHelpers.ToRomanNumeral(_currentSponsorTier) : "X";
        MainSponsorTierBadgeLabel.FontColorOverride = accent;
        MainSponsorTierBadgeCaption.Text = active
            ? Loc.GetString("donation-terminal-subscription-card-tier", ("tier", _currentSponsorTier))
            : Loc.GetString("donation-terminal-rank-none");
        MainSponsorTierBadgeCaption.FontColorOverride = active ? SciFiPalette.TextMuted : accent;
        MainSponsorTierBadgeCaption.ToolTip = MainSponsorTierBadgeCaption.Text;
        if (!active)
            MainSubscriptionValue.Text = Loc.GetString("donation-terminal-rank-none");

        MainSubscriptionValue.FontColorOverride = active ? SciFiPalette.Text : accent;
        MainSubscriptionDescription.SetMessage(
            active
                ? Loc.GetString("donation-terminal-current-rank-hint")
                : Loc.GetString("donation-terminal-current-rank-no-subscription-hint"),
            active ? SciFiPalette.TextMuted : SciFiPalette.Text);

        var sponsorTitle = sponsorTier == null
            ? MainSubscriptionValue.Text ?? Loc.GetString("donation-terminal-sponsor-tier", ("tier", _currentSponsorTier))
            : GetSponsorTierName(sponsorTier);

        MainBenefitsTitle.Text = active
            ? Loc.GetString("donation-terminal-main-benefits-title", ("title", sponsorTitle))
            : Loc.GetString("donation-terminal-main-potential-benefits-title");
        MainBenefitsList.RemoveAllChildren();

        if (!active)
        {
            AddMainPotentialBenefitRows(accent);
            return;
        }

        var benefits = sponsorTier == null
            ? new List<string>()
            : BuildSubscriptionBenefits(sponsorTier);

        if (benefits.Count == 0)
        {
            AddMainBenefitRow(
                Loc.GetString("donation-terminal-subscription-card-no-benefits"),
                SciFiPalette.TextMuted);
            return;
        }

        foreach (var benefit in benefits)
        {
            AddMainBenefitRow(benefit, SciFiPalette.Text);
        }
    }

    private void AddMainPotentialBenefitRows(Color markerColor)
    {
        // \u2715 = ✕
        AddMainBenefitRow(
            Loc.GetString("donation-terminal-main-potential-benefit-priority-antags"),
            SciFiPalette.Text,
            "\u2715",
            markerColor);
        AddMainBenefitRow(
            Loc.GetString("donation-terminal-main-potential-benefit-slots"),
            SciFiPalette.Text,
            "\u2715",
            markerColor);
        AddMainBenefitRow(
            Loc.GetString("donation-terminal-subscription-card-benefit-priority"),
            SciFiPalette.Text,
            "\u2715",
            markerColor);
        AddMainBenefitRow(
            Loc.GetString("donation-terminal-main-potential-benefit-flavor"),
            SciFiPalette.Text,
            "\u2715",
            markerColor);
    }

    private void AddMainBenefitRow(
        string text,
        Color color,
        // ✓
        string marker = "\u2713",
        Color? markerColor = null)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        row.AddChild(new Label
        {
            Text = marker,
            Align = Label.AlignMode.Center,
            MinWidth = 12,
            FontOverride = GetBenefitMarkerFont(),
            FontColorOverride = markerColor ?? SciFiPalette.Accent,
            StyleClasses = { StyleClass.LabelSubText },
            VAlign = Label.VAlignMode.Top,
        });

        var benefitLabel = new RichTextLabel
        {
            MaxWidth = 340,
            StyleClasses = { StyleClass.LabelSubText },
        };
        benefitLabel.SetMessage(FormattedMessage.FromUnformatted(text), color);

        row.AddChild(benefitLabel);
        MainBenefitsList.AddChild(row);
    }
}
