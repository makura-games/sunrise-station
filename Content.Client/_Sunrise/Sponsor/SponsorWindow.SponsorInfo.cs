using System;
using System.Collections.Generic;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void RefreshSponsorInfo()
    {
        RefreshSponsorDonateUrl();

        if (_sponsorsManager == null)
            return;

        var session = _player.LocalSession;
        if (session == null)
        {
            _currentSponsorTier = 0;
            SetSubscriptionText(Loc.GetString("donation-terminal-unavailable"));
            RefreshSubscriptionCards();
            return;
        }

        var currentTier = _sponsorInventory.GetSponsorTier();
        if (!_sponsorsManager.ClientIsSponsor() && currentTier <= 0)
        {
            _currentSponsorTier = 0;
            SetSubscriptionText(Loc.GetString("donation-terminal-rank-none"));
            RefreshSubscriptionCards();
            return;
        }

        if (!_sponsorsManager.TryGetOocTitle(session.UserId, out var sponsorTitle) ||
            string.IsNullOrWhiteSpace(sponsorTitle))
        {
            _currentSponsorTier = currentTier;
            SetSubscriptionText(GetCurrentSponsorDisplayText(currentTier));
            RefreshSubscriptionCards();
            return;
        }

        var sponsorTier = GetSponsorTier(sponsorTitle);
        _currentSponsorTier = sponsorTier?.Tier ?? currentTier;
        SetSubscriptionText(sponsorTier == null
            ? sponsorTitle
            : GetSponsorTierName(sponsorTier));
        RefreshSubscriptionCards();
    }

    private void RefreshSponsorTiers(List<SponsorInfo> sponsorTiers)
    {
        _sponsorTiers.Clear();
        _sponsorTiers.AddRange(sponsorTiers);
        RefreshSubscriptionCards();
        RefreshSponsorInfo();
    }

    private SponsorInfo? GetSponsorTier(string sponsorTitle)
    {
        foreach (var sponsorTier in _sponsorTiers)
        {
            if (string.Equals(sponsorTier.Title, sponsorTitle, StringComparison.OrdinalIgnoreCase))
                return sponsorTier;
        }

        return null;
    }

    private SponsorInfo? GetSponsorTier(int sponsorTier)
    {
        foreach (var tier in _sponsorTiers)
        {
            if (tier.Tier == sponsorTier)
                return tier;
        }

        return null;
    }

    private SponsorInfo? GetVisibleSponsorTier(int sponsorTier)
    {
        var tier = GetSponsorTier(sponsorTier);
        return tier is { IsTechnical: false } ? tier : null;
    }

    private List<SponsorInfo> GetVisibleSponsorTiers()
    {
        var visibleSponsorTiers = new List<SponsorInfo>();

        foreach (var sponsorTier in _sponsorTiers)
        {
            if (!sponsorTier.IsTechnical)
                visibleSponsorTiers.Add(sponsorTier);
        }

        return visibleSponsorTiers;
    }

    private string GetCurrentSponsorDisplayText(int sponsorTier)
    {
        if (sponsorTier <= 0)
            return Loc.GetString("donation-terminal-sponsor-active");

        var tierInfo = GetSponsorTier(sponsorTier);
        return tierInfo == null
            ? Loc.GetString("donation-terminal-sponsor-tier", ("tier", sponsorTier))
            : GetSponsorTierName(tierInfo);
    }

    private string GetSponsorTierName(SponsorInfo sponsorTier)
    {
        if (string.IsNullOrWhiteSpace(sponsorTier.Title))
            return Loc.GetString("donation-terminal-sponsor-tier", ("tier", sponsorTier.Tier));

        return sponsorTier.Title;
    }

    private void SetSubscriptionText(string text)
    {
        MainSubscriptionValue.Text = text;
        CurrentSubscriptionValue.Text = text;
        RefreshMainSponsorSummary();
        RefreshSponsorTierDetails();
    }
}
