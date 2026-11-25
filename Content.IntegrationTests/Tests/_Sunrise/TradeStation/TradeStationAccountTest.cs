using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.HijackBeacon;
using Content.Shared._Sunrise.TradeStation;
using Content.Shared.Damage.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

[TestFixture]
public sealed class TradeStationAccountTest : GameTest
{
    [TestPrototypes]
    private const string HostPrototype = @"
- type: entity
  id: SunriseTestTradeStationHost
  parent: BaseStation
  components:
  - type: GridSpawn
    groups:
      trade: !type:GridSpawnGroup
        stationGrid: false
        addComponents:
        - type: ProtectedGrid
        - type: TradeStation
        - type: SunriseTradeStationGrid
        paths:
        - /Maps/_Sunrise/Shuttles/trading_outpost.yml
";

    [TestCase("SunriseTestTradeStationHost")]
    [TestCase("SunriseStandardDevStation")]
    public async Task RoundStartSpawnsTradeStationLinkedToMainStation(string stationPrototype)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid mainStation = default;
        EntityUid? tradeGrid = null;
        var tradeGridCount = 0;

        await server.WaitPost(() =>
        {
            mainStation = entities.SpawnEntity(stationPrototype, MapCoordinates.Nullspace);
            var stationData = entities.GetComponent<StationDataComponent>(mainStation);
            entities.System<StationSystem>().AddGridToStation(mainStation, map.Grid);
            var init = new StationPostInitEvent((mainStation, stationData));
            entities.EventBus.RaiseLocalEvent(mainStation, ref init);
            entities.EventBus.RaiseLocalEvent(mainStation, ref init);

            var query = entities.EntityQueryEnumerator<SunriseTradeStationGridComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                tradeGrid = uid;
                tradeGridCount++;
            }
        });

        await server.WaitAssertion(() =>
        {
            var production = (GridSpawnComponent) server.ProtoMan.Index<EntityPrototype>("StandardNanotrasenStation").Components["GridSpawn"].Component;
            var test = (GridSpawnComponent) server.ProtoMan.Index<EntityPrototype>("SunriseTestTradeStationHost").Components["GridSpawn"].Component;
            var productionGroup = (GridSpawnGroup) production.Groups["trade"];
            var testGroup = (GridSpawnGroup) test.Groups["trade"];
            Assert.That(productionGroup.Paths, Is.EquivalentTo(testGroup.Paths));
            Assert.That(productionGroup.AddComponents.Keys, Is.EquivalentTo(testGroup.AddComponents.Keys));
            Assert.That(productionGroup.StationGrid, Is.EqualTo(testGroup.StationGrid));
            Assert.That(tradeGridCount, Is.EqualTo(1));
            Assert.That(tradeGrid, Is.Not.Null);
            var grid = entities.GetComponent<SunriseTradeStationGridComponent>(tradeGrid!.Value);
            Assert.That(grid.MainStation, Is.EqualTo(mainStation));
            Assert.That(grid.Station, Is.Not.Null.And.Not.EqualTo(mainStation));
            Assert.That(entities.HasComponent<TradeStationComponent>(tradeGrid.Value), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(tradeGrid.Value).MapID, Is.EqualTo(map.MapId));
        });
    }

    [Test]
    public async Task TradeStationMapHasConsolesAndSeparateAccount()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid grid = default;
        EntityUid station = default;
        var loaded = false;

        await server.WaitPost(() =>
        {
            loaded = entities.System<MapLoaderSystem>().TryLoadGrid(map.MapId,
                new ResPath("/Maps/_Sunrise/Shuttles/trading_outpost.yml"), out var loadedGrid);
            if (!loaded || loadedGrid == null)
                return;

            grid = loadedGrid.Value;
            entities.AddComponent<SunriseTradeStationGridComponent>(grid);
            station = entities.System<StationSystem>().GetOwningStation(grid) ?? default;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(loaded, Is.True);
            Assert.That(entities.HasComponent<SunriseTradeStationAccountComponent>(station), Is.True);
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.Zero);

            var orders = 0;
            var sales = 0;
            var query = entities.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (transform.GridUid != grid)
                    continue;
                if (metadata.EntityPrototype?.ID == "SunriseComputerTradeStationOrders")
                {
                    orders++;
                    Assert.That(entities.HasComponent<GodmodeComponent>(uid), Is.False);
                }
                if (metadata.EntityPrototype?.ID == "SunriseComputerTradeStationSale")
                {
                    sales++;
                    Assert.That(entities.HasComponent<GodmodeComponent>(uid), Is.False);
                }
            }

            Assert.That(orders, Is.GreaterThan(0));
            Assert.That(sales, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task TradeStationAccountStartsEmptyAndIsExcludedFromHijackFine()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid station = default;
        var initialBalance = -1;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            station = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var cargo = entities.System<CargoSystem>();
            initialBalance = cargo.GetBalanceFromAccount(station, "SunriseTradeStation");
            cargo.UpdateBankAccount(station, 1234, "SunriseTradeStation");
            var ev = new HijackBeaconSuccessEvent(5000);
            entities.EventBus.RaiseEvent(EventSource.Local, ref ev);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<SunriseTradeStationAccountComponent>(station), Is.True);
            Assert.That(initialBalance, Is.Zero);
            Assert.That(entities.System<CargoSystem>().GetAccounts(station), Has.Count.EqualTo(1));
            Assert.That(entities.System<CargoSystem>().GetBalanceFromAccount(station, "SunriseTradeStation"), Is.EqualTo(1234));
        });
    }
}
