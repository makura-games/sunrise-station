using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Shipyard;

public sealed partial class ShipyardSystem
{
    /*
     * Purchase, staging map, and deployment logic.
     */

    private void OnPurchase(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsolePurchaseMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            Deny(ent, args.Actor, "shipyard-console-access-denied");
            return;
        }

        if (IsConsoleBroken(ent))
        {
            Deny(ent, args.Actor, "shipyard-console-broken");
            return;
        }

        if (HasPendingAction(ent.Owner))
        {
            Deny(ent, args.Actor, "shipyard-console-action-pending");
            return;
        }

        if (ent.Comp.CurrentShuttle is { } current && Exists(current))
        {
            Deny(ent, args.Actor, "shipyard-console-sell-first");
            return;
        }

        if (!TryGetVessel(ent.Comp, args.VesselId, out var vessel))
        {
            Deny(ent, args.Actor, InvalidVesselMessage);
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            Deny(ent, args.Actor, "shipyard-console-station-not-found");
            return;
        }

        if (!_cargo.TryGetAccount((station, bank), ent.Comp.Account, out var balance))
        {
            Deny(ent, args.Actor, "shipyard-console-account-not-found");
            return;
        }

        if (vessel.Price <= 0)
        {
            Deny(ent, args.Actor, InvalidVesselMessage);
            return;
        }

        if (balance < vessel.Price)
        {
            Deny(ent, args.Actor, "shipyard-console-insufficient-funds", ("cost", vessel.Price));
            return;
        }

        _map.CreateMap(out var stagingMapId);
        var purchaseQueued = false;
        try
        {
            if (!_mapLoader.TryLoadGrid(stagingMapId, vessel.GridPath, out var shuttleGrid, rot: vessel.Rotation))
            {
                Deny(ent, args.Actor, "shipyard-console-load-failed");
                return;
            }

            var shuttleUid = shuttleGrid.Value.Owner;
            if (!TryGetPurchasedShuttleDestination(shuttleUid, station, ent.Comp.PriorityTag, out _))
            {
                Deny(ent, args.Actor, "shipyard-console-docking-failed");
                return;
            }

            if (!TryAdjustBankAccount((station, bank), ent.Comp.Account, -vessel.Price))
            {
                Deny(ent, args.Actor, "shipyard-console-transaction-failed");
                return;
            }

            _pendingPurchases.Add(new PendingShipyardPurchase(
                ent.Owner,
                args.Actor,
                station,
                _timing.CurTime + ent.Comp.PurchaseDelay,
                stagingMapId,
                shuttleUid,
                vessel,
                vessel.Price,
                ent.Comp.Account));
            purchaseQueued = true;
            UpdateUi(ent);
        }
        finally
        {
            if (!purchaseQueued)
                DeleteStagingMap(stagingMapId);
        }
    }

    private void UpdatePendingPurchases()
    {
        for (var i = _pendingPurchases.Count - 1; i >= 0; i--)
        {
            var purchase = _pendingPurchases[i];
            if (_timing.CurTime < purchase.CompleteAt)
                continue;

            _pendingPurchases.RemoveAt(i);
            if (!_consoleQuery.TryComp(purchase.Console, out var console))
            {
                RefundPendingPurchase(purchase);
                CleanupPendingPurchase(purchase);
                continue;
            }

            CompletePurchase((purchase.Console, console), purchase);
        }
    }

    private void CompletePurchase(Entity<ShipyardConsoleComponent> ent, PendingShipyardPurchase purchase)
    {
        if (IsConsoleBroken(ent))
        {
            CancelPendingPurchase(ent, purchase, "shipyard-console-broken");
            return;
        }

        if (!_map.MapExists(purchase.StagingMap) || !Exists(purchase.Shuttle))
        {
            CancelPendingPurchase(ent, purchase, "shipyard-console-load-failed");
            return;
        }

        if (_station.GetOwningStation(ent) != purchase.Station ||
            !TryComp<StationBankAccountComponent>(purchase.Station, out _))
        {
            CancelPendingPurchase(ent, purchase, "shipyard-console-station-not-found");
            return;
        }

        if (ent.Comp.CurrentShuttle is { } current && Exists(current))
        {
            CancelPendingPurchase(ent, purchase, "shipyard-console-sell-first");
            return;
        }

        if (!TryGetPurchasedShuttleDestination(
                purchase.Shuttle,
                purchase.Station,
                ent.Comp.PriorityTag,
                out var destination))
        {
            CancelPendingPurchase(ent, purchase, "shipyard-console-docking-failed");
            return;
        }

        try
        {
            DeployPurchasedShuttle(purchase.Shuttle, destination);
        }
        catch (Exception exception)
        {
            Log.Error(
                "Failed to deploy purchased shuttle {Shuttle} (prototype: {Prototype}): {Exception}",
                ToPrettyString(purchase.Shuttle),
                purchase.Vessel.ID,
                exception);
            CancelPendingPurchase(ent, purchase, "shipyard-console-docking-failed");
            return;
        }

        DeleteStagingMap(purchase.StagingMap);

        if (!Exists(purchase.Shuttle))
        {
            var refunded = RefundPendingPurchase(purchase);
            Deny(
                ent,
                purchase.Actor,
                refunded ? "shipyard-console-docking-failed" : "shipyard-console-refund-failed");
            return;
        }

        ent.Comp.CurrentShuttle = purchase.Shuttle;
        ent.Comp.CurrentShuttlePrice = purchase.Price;
        ent.Comp.InitialShuttleAppraisal = _pricing.AppraiseGrid(purchase.Shuttle);
        ent.Comp.CurrentShuttleVessel = purchase.Vessel;
        Dirty(ent);

        _adminLogger.Add(
            LogType.StorePurchase,
            LogImpact.Medium,
            $"{ToPrettyString(purchase.Actor):player} purchased shuttle {ToPrettyString(purchase.Shuttle):shuttle} "
            + $"({purchase.Vessel.ID}) for {purchase.Price} credits from {purchase.Account} using "
            + $"{ToPrettyString(ent):console}.");
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        if (Exists(purchase.Actor))
            _popup.PopupEntity(Loc.GetString("shipyard-console-purchase-success"), ent, purchase.Actor);
        Announce(ent, "shipyard-console-purchase-announcement",
            ("ship", Loc.GetString(purchase.Vessel.Name)), ("cost", purchase.Price));
        UpdateUi(ent);
    }

    private void CancelPendingPurchase(
        Entity<ShipyardConsoleComponent> ent,
        PendingShipyardPurchase purchase,
        string message)
    {
        var refunded = RefundPendingPurchase(purchase);
        CleanupPendingPurchase(purchase);
        Deny(
            ent,
            purchase.Actor,
            refunded ? message : "shipyard-console-refund-failed");
    }

    private bool RefundPendingPurchase(PendingShipyardPurchase purchase)
    {
        if (!TryComp<StationBankAccountComponent>(purchase.Station, out var bank) ||
            !TryAdjustBankAccount((purchase.Station, bank), purchase.Account, purchase.Price))
        {
            Log.Error(
                "Failed to refund {Price} credits for cancelled shipyard purchase {Vessel} at station {Station}",
                purchase.Price,
                purchase.Vessel.ID,
                ToPrettyString(purchase.Station));
            _adminLogger.Add(
                LogType.StoreRefund,
                LogImpact.High,
                $"Failed to refund {purchase.Price} credits to {purchase.Account} for cancelled shipyard purchase "
                + $"{purchase.Vessel.ID} by {ToPrettyString(purchase.Actor):player}.");
            return false;
        }

        _adminLogger.Add(
            LogType.StoreRefund,
            LogImpact.Medium,
            $"Refunded {purchase.Price} credits to {purchase.Account} for cancelled shipyard purchase "
            + $"{purchase.Vessel.ID} by {ToPrettyString(purchase.Actor):player}.");
        return true;
    }

    private void CleanupPendingPurchase(PendingShipyardPurchase purchase)
    {
        if (Exists(purchase.Shuttle))
            QueueDel(purchase.Shuttle);

        DeleteStagingMap(purchase.StagingMap);
    }

    private void DeleteStagingMap(MapId mapId)
    {
        if (_map.MapExists(mapId))
            _map.DeleteMap(mapId);
    }

    private bool TryGetVessel(
        ShipyardConsoleComponent component,
        ProtoId<ShipyardVesselPrototype> vesselId,
        out ShipyardVesselPrototype vessel)
    {
        vessel = default!;
        if (!_prototype.TryIndex(vesselId, out var candidate))
            return false;

        if (candidate.Group != component.VesselGroup && !component.Vessels.Contains(candidate.ID))
            return false;

        vessel = candidate;
        return true;
    }

    private bool TryGetPurchasedShuttleDestination(
        EntityUid shuttleUid,
        EntityUid stationUid,
        string? priorityTag,
        out PurchasedShuttleDestination destination)
    {
        destination = default;
        if (!TryComp<ShuttleComponent>(shuttleUid, out var shuttle) ||
            !TryComp<StationDataComponent>(stationUid, out var stationData))
        {
            return false;
        }

        var bestConfig = string.IsNullOrEmpty(priorityTag)
            ? null
            : GetBestDockingConfig(shuttleUid, stationData, priorityTag, requirePriority: true);
        bestConfig ??= GetBestDockingConfig(shuttleUid, stationData, null, requirePriority: false);

        if (bestConfig != null)
        {
            destination = new PurchasedShuttleDestination(shuttle, bestConfig, null);
            return true;
        }

        var fallbackGrid = _station.GetLargestGrid((stationUid, stationData));
        if (fallbackGrid is not { } grid ||
            !TryComp<TransformComponent>(grid, out var gridTransform) ||
            gridTransform.MapUid is not { } mapUid ||
            !mapUid.IsValid())
        {
            return false;
        }

        destination = new PurchasedShuttleDestination(shuttle, null, grid);
        return true;
    }

    private DockingConfig? GetBestDockingConfig(
        EntityUid shuttleUid,
        StationDataComponent stationData,
        string? priorityTag,
        bool requirePriority)
    {
        DockingConfig? bestConfig = null;
        foreach (var gridUid in stationData.Grids)
        {
            if (gridUid == shuttleUid || !Exists(gridUid))
                continue;

            var config = _docking.GetDockingConfig(shuttleUid, gridUid, priorityTag);
            if (config == null || requirePriority && !_docking.IsConfigPriority(config, priorityTag))
                continue;

            if (bestConfig == null || config.Docks.Count > bestConfig.Docks.Count)
                bestConfig = config;
        }

        return bestConfig;
    }

    private void DeployPurchasedShuttle(EntityUid shuttleUid, PurchasedShuttleDestination destination)
    {
        if (destination.Config is { } config)
        {
            _shuttle.FTLDock((shuttleUid, Transform(shuttleUid)), config);
            return;
        }

        _shuttle.TryFTLDock(shuttleUid, destination.Shuttle, destination.FallbackGrid!.Value);
    }

    private readonly record struct PurchasedShuttleDestination(
        ShuttleComponent Shuttle,
        DockingConfig? Config,
        EntityUid? FallbackGrid);

    private readonly record struct PendingShipyardPurchase(
        EntityUid Console,
        EntityUid Actor,
        EntityUid Station,
        TimeSpan CompleteAt,
        MapId StagingMap,
        EntityUid Shuttle,
        ShipyardVesselPrototype Vessel,
        int Price,
        ProtoId<CargoAccountPrototype> Account);
}
