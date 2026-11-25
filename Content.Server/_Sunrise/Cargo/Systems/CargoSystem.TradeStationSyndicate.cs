using Content.Server._Sunrise.TradeStation;
using Content.Shared._Sunrise.TradeStation;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.HijackBeacon;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    /// <summary>Checks antagonist access to Syndicate supplies after the ATS hijack.</summary>
    public bool CanUseTradeStationSyndicate(EntityUid console, EntityUid user)
    {
        return HasComp<SunriseTradeStationConsoleComponent>(console) &&
               !HasComp<SunriseTradeStationBlackMarketComponent>(console) &&
               Transform(console).GridUid is { } grid &&
               TryComp<TradeStationComponent>(grid, out var station) && station.Hacked &&
               _mind.TryGetMind(user, out var mind, out _) && _roles.MindIsAntagonist(mind);
    }

    private void OnTradeStationSyndicateRequest(Entity<SunriseTradeStationConsoleComponent> ent, ref SunriseTradeStationSyndicateRequest args)
    {
        SendTradeStationSyndicateAccess(ent, args.Actor);
    }

    private void OnTradeStationSyndicateUiOpened(EntityUid uid, SunriseTradeStationConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(CargoConsoleUiKey.Orders))
            SendTradeStationSyndicateAccess(uid, args.Actor);
    }

    private void SendTradeStationSyndicateAccess(EntityUid console, EntityUid user)
    {
        var allowed = CanUseTradeStationSyndicate(console, user);
        _uiSystem.ServerSendUiMessage(console, CargoConsoleUiKey.Orders,
            new SunriseTradeStationSyndicateResponse(allowed), user);
    }

    private void OnTradeStationHijacked(Entity<TradeStationComponent> ent, ref TradeStationHijackedEvent args)
    {
        var query = EntityQueryEnumerator<SunriseTradeStationConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.GridUid == ent.Owner)
                _uiSystem.CloseUi(uid, CargoConsoleUiKey.Orders);
        }
    }
}
