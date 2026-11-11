using System;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.SponsorInventory;

public sealed partial class SunriseInventorySystem
{
    /*
     * Purchases sponsor inventory items and packs through the external sponsor API.
     */
    private async Task TryPurchaseInventoryItem(ICommonSession session, SunriseInventoryPurchaseRequestEvent ev)
    {
        var sponsors = _sponsors;
        if (sponsors == null)
            return;

        if (!TryBuildPurchaseRequest(sponsors, ev, out var request, out var error))
        {
            RaiseNetworkEvent(new SunriseInventoryPurchaseResultEvent(new SponsorInventoryPurchaseResult
            {
                Success = false,
                Error = error,
            }), session);
            return;
        }

        SponsorInventoryPurchaseResult result;
        using (var timeout = new CancellationTokenSource(PurchaseRequestTimeout))
        {
            try
            {
                result = await sponsors.PurchaseSponsorInventoryItemAsync(
                    session.UserId,
                    request,
                    timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                result = new SponsorInventoryPurchaseResult
                {
                    Success = false,
                    Error = "request-timeout",
                };
            }
        }

        RaiseNetworkEvent(new SunriseInventoryPurchaseResultEvent(result), session);

        if (result.Success)
            await SendInitialData(session);
    }

    private static bool TryBuildPurchaseRequest(
        ISharedSponsorsManager sponsors,
        SunriseInventoryPurchaseRequestEvent ev,
        out SponsorInventoryPurchaseRequest request,
        out string error)
    {
        request = new SponsorInventoryPurchaseRequest();
        error = "invalid-request";

        var hasItem = !string.IsNullOrWhiteSpace(ev.ItemId);
        var hasPack = !string.IsNullOrWhiteSpace(ev.PackId);
        if (hasItem == hasPack)
            return false;

        if (!Guid.TryParseExact(ev.IdempotencyKey, "N", out _))
        {
            error = "invalid-idempotency-key";
            return false;
        }

        var config = sponsors.GetSponsorInventoryConfig();
        if (hasItem)
        {
            if (!IsReasonablePurchaseId(ev.ItemId) ||
                !CatalogContainsItem(config, ev.ItemId))
            {
                error = "unknown-item";
                return false;
            }
        }

        if (hasPack)
        {
            if (!IsReasonablePurchaseId(ev.PackId) ||
                !CatalogContainsPack(config, ev.PackId))
            {
                error = "unknown-pack";
                return false;
            }
        }

        request = new SponsorInventoryPurchaseRequest
        {
            ItemId = hasItem ? ev.ItemId : null,
            PackId = hasPack ? ev.PackId : null,
            IdempotencyKey = ev.IdempotencyKey,
        };
        return true;
    }

    private static bool IsReasonablePurchaseId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && id.Length <= MaxInventoryPurchaseIdLength;
    }

    private static bool CatalogContainsItem(SponsorInventoryConfig config, string? itemId)
    {
        foreach (var item in config.Items ?? [])
        {
            if (item?.Id == itemId)
                return true;
        }

        return false;
    }

    private static bool CatalogContainsPack(SponsorInventoryConfig config, string? packId)
    {
        foreach (var pack in config.Packs ?? [])
        {
            if (pack?.Id == packId)
                return true;
        }

        return false;
    }
}
