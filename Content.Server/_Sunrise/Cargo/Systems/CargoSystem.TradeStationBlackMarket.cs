using Content.Server._Sunrise.TradeStation;
using Content.Shared._Sunrise.TradeStation;
using Content.Shared.Access.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.UserInterface;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private void ActivateTradeStationBlackMarket(Entity<CargoOrderConsoleComponent> console)
    {
        var existed = EnsureComp<SunriseTradeStationBlackMarketComponent>(console, out var state);
        if (!existed)
            state.PreviousGroups = new(console.Comp.AllowedGroups);

        state.EndTime = Timing.CurTime + TimeSpan.FromMinutes(5);
        console.Comp.AllowedGroups = ["pirateMarket"];
        Dirty(console);
        _appearance.SetData(console, SunriseTradeStationConsoleVisuals.BlackMarket, true);
        _uiSystem.CloseUi(console.Owner, CargoConsoleUiKey.Orders);
        if (_station.GetOwningStation(console) is { } station)
            UpdateOrders(station);
    }

    /// <summary>Checks user access to the temporary ATS black market.</summary>
    public bool CanUseTradeStationBlackMarket(EntityUid console, EntityUid user)
    {
        if (!HasComp<SunriseTradeStationBlackMarketComponent>(console))
            return true;

        foreach (var item in _accessReaderSystem.FindPotentialAccessItems(user))
        {
            if (HasComp<IdCardComponent>(item) && _accessReaderSystem.FindAccessTags(item).Contains("Pirate"))
                return true;
        }

        return false;
    }

    private void OnTradeStationUiAttempt(Entity<SunriseTradeStationConsoleComponent> console, ref ActivatableUIOpenAttemptEvent args)
    {
        if (CanUseTradeStationBlackMarket(console, args.User))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupCursor(Loc.GetString("cargo-console-order-not-allowed"), args.User);
    }

    private void OnTradeStationUiMessageAttempt(Entity<SunriseTradeStationConsoleComponent> console, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey.Equals(CargoConsoleUiKey.Orders) && !CanUseTradeStationBlackMarket(console, args.Actor))
            args.Cancel();
    }

    private void UpdateTradeStationBlackMarkets()
    {
        var query = EntityQueryEnumerator<SunriseTradeStationBlackMarketComponent, CargoOrderConsoleComponent>();
        while (query.MoveNext(out var uid, out var active, out var console))
        {
            if (Timing.CurTime < active.EndTime)
                continue;

            console.AllowedGroups = active.PreviousGroups;
            Dirty(uid, console);
            _appearance.SetData(uid, SunriseTradeStationConsoleVisuals.BlackMarket, false);
            _uiSystem.CloseUi(uid, CargoConsoleUiKey.Orders);
            RemCompDeferred<SunriseTradeStationBlackMarketComponent>(uid);
            if (_station.GetOwningStation(uid) is { } station)
                UpdateOrders(station);
        }
    }
}
