using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Components;
using Content.Shared._Sunrise.TradeStation;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Station.Components;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private bool TryPurchaseTradeStationOrder(
        EntityUid console,
        CargoOrderConsoleComponent consoleComponent,
        CargoConsoleAddOrderMessage args,
        CargoProductPrototype product,
        EntityUid station,
        StationBankAccountComponent bank,
        StationCargoOrderDatabaseComponent orderDatabase)
    {
        if (!_accessReaderSystem.IsAllowed(args.Actor, console) ||
            args.Amount <= 0 || args.Amount > orderDatabase.Capacity ||
            !TryComp<StationDataComponent>(station, out var stationData) ||
            Transform(console).GridUid is not { } grid ||
            !TryComp<SunriseTradeStationGridComponent>(grid, out var tradeGrid) ||
            !TryGetAccount((station, bank), consoleComponent.Account, out var balance))
            return false;

        var hijacked = TryComp<TradeStationComponent>(grid, out var tradeStation) && tradeStation.Hacked;
        var payStation = !hijacked && !HasComp<SunriseTradeStationBlackMarketComponent>(console);
        var mainStation = tradeGrid.MainStation;
        StationBankAccountComponent? mainBank = null;
        if (payStation && (mainStation == null ||
                           !TryComp(mainStation, out mainBank) ||
                           !TryGetAccount((mainStation.Value, mainBank), "Cargo", out _)))
            return false;

        var unitPrice = product.Group == "market" ? SunriseTradeStationPricing.BaseUnitPrice(product.Cost) : product.Cost;
        var total = unitPrice * args.Amount;
        if (product.Cost < 0 || total > int.MaxValue || total > balance)
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-insufficient-funds", ("cost", total)), args.Actor);
            PlayDenySound(console, consoleComponent);
            return false;
        }

        var order = GetOrderData(args, product, GenerateOrderId(orderDatabase), consoleComponent.Account);
        order.SetApproverData(_identity.GetIdentityShortInfo(args.Actor, console));
        order.Approved = true;
        order.ApprovingConsole = GetNetEntity(console);

        var invoice = HasComp<SunriseTradeStationBlackMarketComponent>(console)
            ? "SunrisePaperCargoInvoicePirate"
            : hijacked && product.Group == "SunriseTradeStationSyndicate"
                ? "SunrisePaperCargoInvoiceHijacked"
                : null;

        if (TryFulfillOrder((station, stationData), order.Account, order, orderDatabase, invoice) == null)
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-unfulfilled"), args.Actor);
            PlayDenySound(console, consoleComponent);
            return false;
        }

        var cost = (int) total;
        UpdateBankAccount((station, bank), -cost, consoleComponent.Account);

        if (payStation && mainStation is { } recipient && mainBank != null)
        {
            UpdateBankAccount((recipient, mainBank), cost / 2, "Cargo");
            var message = Loc.GetString("sunrise-trade-station-order-announcement",
                ("product", product.Name), ("amount", args.Amount), ("cost", cost));
            var notifySecurity = product.Category is "cargoproduct-category-name-security" or "cargoproduct-category-name-armory";

            _radio.SendRadioMessage(console, message, "Supply", console);
            if (notifySecurity)
                _radio.SendRadioMessage(console, message, "Security", console);

            if (_messenger.GetServerEntity(recipient) is var (server, _))
            {
                if (_messenger.GetGroupIdByRadioChannel("Supply") is { } cargoGroup)
                    _messenger.SendSystemMessageToGroup(server, cargoGroup, message);
                if (notifySecurity && _messenger.GetGroupIdByRadioChannel("Security") is { } securityGroup)
                    _messenger.SendSystemMessageToGroup(server, securityGroup, message);
            }
        }

        _audio.PlayPvs(ApproveSound, console);
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} purchased {args.Amount} of {product.ID} at trade station for {cost}");
        return true;
    }
}
