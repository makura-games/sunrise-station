using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Interaction;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

[TestFixture]
public sealed class TradeStationFundsTest : GameTest
{
    [TestCase(true, 1000)]
    [TestCase(false, 0)]
    public async Task BlackMarketRequiresPirateCardForCashDeposit(bool pirateCard, int expectedBalance)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;
        var pickedUp = !pirateCard;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            station = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            if (pirateCard)
            {
                var id = entities.SpawnEntity("PirateIDCard", map.GridCoords);
                pickedUp = entities.System<SharedHandsSystem>().TryPickupAnyHand(user, id);
            }
            var emag = new GotEmaggedEvent(user, EmagType.Interaction);
            entities.EventBus.RaiseLocalEvent(console, ref emag);
            var cash = entities.SpawnEntity("SpaceCash1000", MapCoordinates.Nullspace);
            entities.EventBus.RaiseLocalEvent(console, new InteractUsingEvent(user, cash, console, map.GridCoords));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.EqualTo(expectedBalance));
        });
    }

    [Test]
    public async Task SaleDepositsProceedsInTradeStationAccount()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            station = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            entities.SpawnEntity("CargoPalletSell", map.GridCoords);
            entities.SpawnEntity("SpaceCash1000", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationSale", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            entities.EventBus.RaiseLocalEvent(console, new CargoPalletSellMessage { Actor = user });
        });

        await server.WaitAssertion(() =>
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.EqualTo(1000)));
    }

    [Test]
    public async Task VisitorsCanDepositAndWithdrawEntireBalance()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;
        var deposited = 0;
        var withdrawalBlocked = 0;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            station = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var cash = entities.SpawnEntity("SpaceCash1000", MapCoordinates.Nullspace);
            var deposit = new InteractUsingEvent(user, cash, console, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(console, deposit);
            var cargo = entities.System<CargoSystem>();
            deposited = cargo.GetBalanceFromAccount(station, "SunriseTradeStation");

            var invalidTransfer = new CargoConsoleWithdrawFundsMessage("Medical", 100) { Actor = user };
            entities.EventBus.RaiseLocalEvent(console, invalidTransfer);
            withdrawalBlocked = cargo.GetBalanceFromAccount(station, "SunriseTradeStation");

            var withdrawal = new CargoConsoleWithdrawFundsMessage(null, 1000) { Actor = user };
            entities.EventBus.RaiseLocalEvent(console, withdrawal);
        });

        await Pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(deposited, Is.EqualTo(1000));
            Assert.That(withdrawalBlocked, Is.EqualTo(1000));
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.Zero);
            var query = entities.EntityQueryEnumerator<CashComponent, StackComponent>();
            var cashTotal = 0;
            while (query.MoveNext(out var uid, out _, out var stack))
                cashTotal += entities.System<SharedStackSystem>().GetCount((uid, stack));
            Assert.That(cashTotal, Is.EqualTo(1000));
        });
    }
}
