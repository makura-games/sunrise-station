using System.Collections.Generic;
using Content.Client._Sunrise.Sheetlets;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void RefreshSubscriptionCards()
    {
        SubscriptionCards.RemoveAllChildren();

        var tiers = GetVisibleSponsorTiers();

        if (tiers.Count == 0)
        {
            SubscriptionCards.AddChild(new Label
            {
                Text = Loc.GetString("donation-terminal-subscription-card-empty"),
                FontColorOverride = SciFiPalette.TextMuted,
            });
            return;
        }

        tiers.Sort((a, b) => b.Tier.CompareTo(a.Tier));

        for (var i = 0; i < tiers.Count; i++)
        {
            if (i > 0)
                SubscriptionCards.AddChild(CreateSubscriptionColumnDivider());

            var sponsorTier = tiers[i];
            var benefits = BuildSubscriptionBenefits(sponsorTier);
            var card = new SponsorSubscriptionCard(
                GetSponsorTierName(sponsorTier),
                sponsorTier.Tier,
                benefits,
                GetSubscriptionAccentColor(sponsorTier),
                sponsorTier.Tier > 0 && sponsorTier.Tier == _currentSponsorTier);

            var tier = sponsorTier.Tier;
            card.DetailsPressed += () => OpenSponsorTierDetails(tier);
            SubscriptionCards.AddChild(card);
        }
    }

    private PanelContainer CreateSubscriptionColumnDivider()
    {
        return new PanelContainer
        {
            MinWidth = 1,
            MinHeight = SubscriptionCardMinHeight,
            VerticalExpand = true,
            StyleClasses =
            {
                SunriseStyleClass.SciFiDivider,
            },
        };
    }

    private Color GetSubscriptionAccentColor(int sponsorTier)
    {
        var tierInfo = GetSponsorTier(sponsorTier);
        return tierInfo == null
            ? GetSubscriptionFallbackColor(sponsorTier)
            : GetSubscriptionAccentColor(tierInfo);
    }

    private Color GetSubscriptionAccentColor(SponsorInfo sponsorTier)
    {
        if (!string.IsNullOrWhiteSpace(sponsorTier.OOCColor) &&
            Color.TryFromHex(sponsorTier.OOCColor, out var oocColor))
        {
            return oocColor;
        }

        return GetSubscriptionFallbackColor(sponsorTier.Tier);
    }

    private static Color GetSubscriptionFallbackColor(int sponsorTier)
    {
        return sponsorTier switch
        {
            >= 4 => Color.FromHex("#9EEBFF"),
            3 => Color.FromHex("#B983FF"),
            2 => Color.FromHex("#F0C44F"),
            _ => Color.FromHex("#C8CED8"),
        };
    }

    private List<string> BuildSubscriptionBenefits(SponsorInfo sponsorTier)
    {
        var benefits = new List<string>();

        AddCountBenefit(
            benefits,
            "donation-terminal-subscription-card-benefit-antags",
            sponsorTier.OpenAntags.Length + sponsorTier.PriorityAntags.Length);

        if (sponsorTier.HavePriorityJoin)
            benefits.Add(Loc.GetString("donation-terminal-subscription-card-benefit-priority"));

        if (sponsorTier.ExtraSlots > 0)
        {
            benefits.Add(Loc.GetString(
                "donation-terminal-subscription-card-benefit-slots",
                ("count", sponsorTier.ExtraSlots)));
        }

        if (sponsorTier.AllowedRespawn)
            benefits.Add(Loc.GetString("donation-terminal-subscription-card-benefit-respawn"));

        if (sponsorTier.AllowedFlavor)
            benefits.Add(Loc.GetString("donation-terminal-subscription-card-benefit-flavor"));

        if (sponsorTier.SizeFlavor > 0)
        {
            benefits.Add(Loc.GetString(
                "donation-terminal-subscription-card-benefit-size",
                ("count", sponsorTier.SizeFlavor)));
        }

        AddCountBenefit(
            benefits,
            "donation-terminal-subscription-card-benefit-roles",
            sponsorTier.OpenRoles.Length + sponsorTier.PriorityRoles.Length + sponsorTier.BypassRoles.Length);
        AddCountBenefit(
            benefits,
            "donation-terminal-subscription-card-benefit-ghosts",
            sponsorTier.OpenGhostRoles.Length + sponsorTier.PriorityGhostRoles.Length + sponsorTier.GhostThemes.Length);
        AddCountBenefit(
            benefits,
            "donation-terminal-subscription-card-benefit-customization",
            sponsorTier.AllowedMarkings.Length + sponsorTier.AllowedSpecies.Length + sponsorTier.AllowedVoices.Length);
        AddCountBenefit(
            benefits,
            "donation-terminal-subscription-card-benefit-pets",
            sponsorTier.Pets.Length);

        return benefits;
    }

    private void AddCountBenefit(List<string> benefits, string locKey, int count)
    {
        if (count <= 0)
            return;

        benefits.Add(Loc.GetString(locKey, ("count", count)));
    }
}
