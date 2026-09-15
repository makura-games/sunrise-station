using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.BUI;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared.Cargo.Components;

namespace Content.Server._Sunrise.Shipyard;

public sealed partial class ShipyardSystem
{
    /*
     * Server-side BUI state construction and bank updates.
     */

    private void OnUiOpened(Entity<ShipyardConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnBankBalanceUpdated(Entity<StationBankAccountComponent> ent, ref BankBalanceUpdatedEvent args)
    {
        var query = EntityQueryEnumerator<ShipyardConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (!_ui.IsUiOpen(uid, ShipyardConsoleUiKey.Key) ||
                _station.GetOwningStation(uid) != ent.Owner)
            {
                continue;
            }

            UpdateUi((uid, console));
        }
    }

    private void UpdateUi(Entity<ShipyardConsoleComponent> ent)
    {
        var balance = 0;
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is { } station &&
            TryComp<StationBankAccountComponent>(station, out var bank))
        {
            balance = _cargo.GetBalanceFromAccount((station, bank), ent.Comp.Account);
        }

        var currentSellValue = 0;
        if (ent.Comp.CurrentShuttle is not { } shuttle || !Exists(shuttle))
        {
            if (ent.Comp.CurrentShuttle is not null)
                ClearShuttle(ent);
        }
        else
        {
            currentSellValue = GetCurrentSellValue(ent.Comp, shuttle);
        }

        var vessels = new List<ShipyardVesselData>();
        var added = new HashSet<string>();
        foreach (var vessel in _prototype.EnumeratePrototypes<ShipyardVesselPrototype>())
        {
            if (vessel.Group != ent.Comp.VesselGroup || !added.Add(vessel.ID))
                continue;

            vessels.Add(new ShipyardVesselData(vessel, vessel.Price));
        }

        foreach (var vesselId in ent.Comp.Vessels)
        {
            if (!_prototype.TryIndex(vesselId, out ShipyardVesselPrototype? vessel) || !added.Add(vessel.ID))
                continue;

            vessels.Add(new ShipyardVesselData(vessel, vessel.Price));
        }

        _ui.SetUiState(ent.Owner, ShipyardConsoleUiKey.Key, new ShipyardConsoleInterfaceState(
            ent.Comp.Account,
            balance,
            ent.Comp.CurrentShuttleVessel,
            currentSellValue,
            HasPendingAction(ent.Owner),
            vessels));
    }
}
