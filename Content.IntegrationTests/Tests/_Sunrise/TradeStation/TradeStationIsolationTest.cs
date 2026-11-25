using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Interaction;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

[TestFixture]
public sealed class TradeStationIsolationTest : GameTest
{
    [Test]
    public async Task OrdinaryCargoConsoleCannotUseTradeStationAccount()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            station = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var cargo = entities.System<CargoSystem>();
            cargo.UpdateBankAccount(station, 1000, "SunriseTradeStation");
            entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            var console = entities.SpawnEntity("ComputerCargoOrders", map.GridCoords);
            entities.System<AccessReaderSystem>().SetActive((console, entities.GetComponent<AccessReaderComponent>(console)), false);
            var user = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            var cash = entities.SpawnEntity("SpaceCash1000", MapCoordinates.Nullspace);
            entities.EventBus.RaiseLocalEvent(console, new InteractUsingEvent(user, cash, console, map.GridCoords));
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleWithdrawFundsMessage(null, 500) { Actor = user });
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleAddOrderMessage("buyer", "test", "HydroponicsTools", 1) { Actor = user });

            var orders = entities.GetComponent<StationCargoOrderDatabaseComponent>(station).Orders["SunriseTradeStation"];
            orders.Add(new CargoOrderData(999, "HydroponicsTools", 1, "buyer", "test", "SunriseTradeStation"));
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleApproveOrderMessage(999) { Actor = user });
        });

        await server.WaitAssertion(() =>
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.EqualTo(1000)));
    }

    [Test]
    public async Task TradeStationConsoleCannotUseOrdinaryStationAccount()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;

        await server.WaitPost(() =>
        {
            station = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationDataComponent>(station);
            entities.AddComponent<StationBankAccountComponent>(station);
            var orders = entities.AddComponent<StationCargoOrderDatabaseComponent>(station);
            orders.Orders["Cargo"] = new();
            entities.System<StationSystem>().AddGridToStation(station, map.Grid);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            var cash = entities.SpawnEntity("SpaceCash1000", MapCoordinates.Nullspace);
            entities.EventBus.RaiseLocalEvent(console, new InteractUsingEvent(user, cash, console, map.GridCoords));
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleWithdrawFundsMessage(null, 1000) { Actor = user });
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleAddOrderMessage("buyer", "test", "HydroponicsTools", 1) { Actor = user });
        });

        await server.WaitAssertion(() =>
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "Cargo"), Is.EqualTo(2000)));
    }
}
