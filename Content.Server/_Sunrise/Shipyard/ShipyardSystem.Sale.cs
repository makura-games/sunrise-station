using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Station.Components;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Shipyard;

public sealed partial class ShipyardSystem
{
    /*
     * Delayed sale validation and shuttle appraisal logic.
     */

    private void OnSell(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsoleSellMessage args)
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

        if (ent.Comp.CurrentShuttle is not { } shuttleUid || !Exists(shuttleUid))
        {
            ClearShuttle(ent);
            Deny(ent, args.Actor, "shipyard-console-no-shuttle");
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            Deny(ent, args.Actor, "shipyard-console-station-not-found");
            return;
        }

        if (!IsShuttleNearStation(shuttleUid, station, ent.Comp.MaxSellDistance))
        {
            Deny(ent, args.Actor, "shipyard-console-shuttle-too-far", ("distance", ent.Comp.MaxSellDistance));
            return;
        }

        var occupantCount = GetShuttleOccupantCount(shuttleUid);
        if (occupantCount > 0)
        {
            Deny(ent, args.Actor, "shipyard-console-shuttle-occupied", ("count", occupantCount));
            return;
        }

        if (!_cargo.TryGetAccount((station, bank), ent.Comp.Account, out _))
        {
            Deny(ent, args.Actor, "shipyard-console-account-not-found");
            return;
        }

        var soldName = GetCurrentShuttleName(ent.Comp);
        _pendingActions.Add(new PendingShipyardAction(
            ent.Owner,
            args.Actor,
            _timing.CurTime + ent.Comp.SaleDelay,
            soldName));
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("shipyard-console-sale-queued",
            ("delay", ent.Comp.SaleDelay.TotalSeconds), ("ship", soldName)), ent, args.Actor);
        Announce(ent, "shipyard-console-sale-queued-announcement",
            ("ship", soldName), ("delay", ent.Comp.SaleDelay.TotalSeconds));
        UpdateUi(ent);
    }

    private void CompleteSale(Entity<ShipyardConsoleComponent> ent, PendingShipyardAction action)
    {
        if (IsConsoleBroken(ent))
        {
            CancelAction(ent, action, "shipyard-console-broken");
            return;
        }

        if (ent.Comp.CurrentShuttle is not { } shuttleUid || !Exists(shuttleUid))
        {
            ClearShuttle(ent);
            CancelAction(ent, action, "shipyard-console-no-shuttle");
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            CancelAction(ent, action, "shipyard-console-station-not-found");
            return;
        }

        if (!IsShuttleNearStation(shuttleUid, station, ent.Comp.MaxSellDistance))
        {
            CancelAction(ent, action, "shipyard-console-shuttle-too-far", ("distance", ent.Comp.MaxSellDistance));
            return;
        }

        var occupantCount = GetShuttleOccupantCount(shuttleUid);
        if (occupantCount > 0)
        {
            CancelAction(ent, action, "shipyard-console-shuttle-occupied", ("count", occupantCount));
            return;
        }

        var refund = GetCurrentSellValue(ent.Comp, shuttleUid);
        if (!TryAdjustBankAccount((station, bank), ent.Comp.Account, refund))
        {
            CancelAction(ent, action, "shipyard-console-transaction-failed");
            return;
        }

        _adminLogger.Add(
            LogType.StoreRefund,
            LogImpact.Medium,
            $"{ToPrettyString(action.Actor):player} sold shuttle {ToPrettyString(shuttleUid):shuttle} "
            + $"for {refund} credits to {ent.Comp.Account} using {ToPrettyString(ent):console}.");
        QueueDel(shuttleUid);
        ClearShuttle(ent);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        PopupActionActor(ent, action, "shipyard-console-sell-success");
        Announce(ent, "shipyard-console-sale-announcement", ("ship", action.ShipName!), ("refund", refund));
        UpdateUi(ent);
    }

    private bool HasPendingAction(EntityUid console)
    {
        foreach (var purchase in _pendingPurchases)
        {
            if (purchase.Console == console)
                return true;
        }

        foreach (var action in _pendingActions)
        {
            if (action.Console == console)
                return true;
        }

        return false;
    }

    private void CancelAction(
        Entity<ShipyardConsoleComponent> ent,
        PendingShipyardAction action,
        string message,
        params (string Key, object Value)[] args)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
        PopupActionActor(ent, action, message, args);
        Announce(ent, "shipyard-console-sale-cancelled-announcement",
            ("ship", action.ShipName ?? Loc.GetString("shipyard-console-unknown-shuttle")),
            ("reason", Loc.GetString(message, args)));
        UpdateUi(ent);
    }

    private void PopupActionActor(
        Entity<ShipyardConsoleComponent> ent,
        PendingShipyardAction action,
        string message,
        params (string Key, object Value)[] args)
    {
        if (Exists(action.Actor))
            _popup.PopupEntity(Loc.GetString(message, args), ent, action.Actor);
    }

    private bool IsShuttleNearStation(EntityUid shuttleUid, EntityUid stationUid, float maxDistance)
    {
        if (!TryComp<StationDataComponent>(stationUid, out var stationData))
            return false;

        var shuttleTransform = Transform(shuttleUid);
        var shuttlePosition = _transform.GetWorldPosition(shuttleUid);
        var maxDistanceSquared = maxDistance * maxDistance;
        foreach (var gridUid in stationData.Grids)
        {
            if (gridUid == shuttleUid || !Exists(gridUid))
                continue;

            var gridTransform = Transform(gridUid);
            if (gridTransform.MapID != shuttleTransform.MapID)
                continue;

            if ((_transform.GetWorldPosition(gridUid) - shuttlePosition).LengthSquared() <= maxDistanceSquared)
                return true;
        }

        return false;
    }

    private int GetShuttleOccupantCount(EntityUid shuttleUid)
    {
        var occupants = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<GhostComponent>(uid))
                continue;

            if (IsEntityOnShuttle(uid, shuttleUid, xform))
                occupants.Add(uid);
        }

        var ssdQuery = EntityQueryEnumerator<SSDIndicatorComponent, MindContainerComponent, TransformComponent>();
        while (ssdQuery.MoveNext(out var uid, out var ssd, out var mind, out var xform))
        {
            if (HasComp<GhostComponent>(uid))
                continue;

            var hasMind = mind.HasMind;
            if (!hasMind && mind.LastMindStored is { } lastMind)
                hasMind = Exists(lastMind);

            if (!ssd.IsSSD || !hasMind)
                continue;

            if (IsEntityOnShuttle(uid, shuttleUid, xform))
                occupants.Add(uid);
        }

        return occupants.Count;
    }

    private bool IsEntityOnShuttle(EntityUid uid, EntityUid shuttleUid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform, false))
            return false;

        if (xform.GridUid == shuttleUid || xform.ParentUid == shuttleUid)
            return true;

        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (parent == shuttleUid)
                return true;

            if (!TryComp<TransformComponent>(parent, out var parentXform))
                return false;

            if (parentXform.GridUid == shuttleUid)
                return true;

            parent = parentXform.ParentUid;
        }

        return false;
    }

    private void ClearShuttle(Entity<ShipyardConsoleComponent> ent)
    {
        ent.Comp.CurrentShuttle = null;
        ent.Comp.CurrentShuttlePrice = 0;
        ent.Comp.InitialShuttleAppraisal = 0;
        ent.Comp.CurrentShuttleVessel = null;
        Dirty(ent);
    }

    private string GetCurrentShuttleName(ShipyardConsoleComponent component)
    {
        return component.CurrentShuttleVessel is { } vesselId &&
               _prototype.TryIndex(vesselId, out ShipyardVesselPrototype? vessel)
            ? Loc.GetString(vessel.Name)
            : Loc.GetString("shipyard-console-unknown-shuttle");
    }

    // The refund is reduced proportionally to how much the vessel's valuation has decreased
    // relative to the valuation at the time of purchase (for example, due to equipment removal).
    private int GetCurrentSellValue(ShipyardConsoleComponent component, EntityUid shuttleUid)
    {
        var maximumRefund = component.CurrentShuttlePrice * Math.Clamp((double) component.SellRate, 0d, 1d);
        if (component.InitialShuttleAppraisal <= 0)
            return (int) Math.Round(maximumRefund);

        var currentAppraisal = Math.Max(0, _pricing.AppraiseGrid(shuttleUid));
        var retainedValue = Math.Clamp(currentAppraisal / component.InitialShuttleAppraisal, 0d, 1d);
        return (int) Math.Round(maximumRefund * retainedValue);
    }

    private readonly record struct PendingShipyardAction(
        EntityUid Console,
        EntityUid Actor,
        TimeSpan CompleteAt,
        string? ShipName);
}
