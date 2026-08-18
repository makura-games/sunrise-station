using Content.Server._Sunrise.Shipyard;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Sunrise.Shipyard;

[TestFixture]
[TestOf(typeof(ShipyardSystem))]
public sealed class ShipyardSystemTest
{
    private const int InitialBalance = 10_000;
    private const int VesselPrice = 1_000;
    private const int ExpectedSaleValue = 500;
    private const string TestVesselId = "ShipyardTestVessel";

    private static readonly ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";

    [TestPrototypes]
    private const string TestPrototypes = """
        - type: entity
          id: ShipyardTestStation
          parent:
          - BaseStation
          - BaseStationCargo

        - type: entity
          id: ShipyardTestConsole
          parent: BaseComputerShipyard
          components:
          - type: ShipyardConsole
            account: Cargo
            vesselGroup: test
            sellRate: 0.5
            maxSellDistance: 10000

        - type: shipyardVessel
          id: ShipyardTestVessel
          name: shipyard-vessel-salvage-mining-name
          description: shipyard-vessel-salvage-mining-description
          price: 1000
          group: test
          gridPath: /Maps/Shuttles/shittle.yml
        """;

    [Test]
    public async Task PurchaseAndSellShuttle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var cargo = entities.System<CargoSystem>();
        var stationSystem = entities.System<StationSystem>();
        ProtoId<ShipyardVesselPrototype> testVessel = TestVesselId;

        EntityUid station = default;
        EntityUid console = default;
        EntityUid actor = default;
        EntityUid shuttle = default;

        await server.WaitAssertion(() =>
        {
            station = entities.SpawnEntity("ShipyardTestStation", MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(station, testMap.Grid);

            console = entities.SpawnEntity("ShipyardTestConsole", testMap.GridCoords);
            actor = entities.SpawnEntity("MobHuman", testMap.GridCoords);

            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            Assert.That(cargo.TrySetBankAccount((station, bank), CargoAccount, InitialBalance), Is.True);

            var consoleComponent = entities.GetComponent<ShipyardConsoleComponent>(console);
            consoleComponent.PurchaseDelay = TimeSpan.Zero;
            consoleComponent.SaleDelay = TimeSpan.Zero;

            var purchase = new ShipyardConsolePurchaseMessage(testVessel)
            {
                Actor = actor,
                UiKey = ShipyardConsoleUiKey.Key,
            };
            entities.EventBus.RaiseLocalEvent(console, purchase);

            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.That(balance, Is.EqualTo(InitialBalance - VesselPrice));
            Assert.That(consoleComponent.CurrentShuttle, Is.Null);
        });

        // Нулевая задержка завершается в следующем ShipyardSystem.Update().
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var consoleComponent = entities.GetComponent<ShipyardConsoleComponent>(console);
            Assert.That(consoleComponent.CurrentShuttle, Is.Not.Null);

            shuttle = consoleComponent.CurrentShuttle.Value;
            Assert.Multiple(() =>
            {
                Assert.That(entities.EntityExists(shuttle), Is.True);
                Assert.That(consoleComponent.CurrentShuttlePrice, Is.EqualTo(VesselPrice));
                Assert.That(consoleComponent.CurrentShuttleVessel, Is.EqualTo(testVessel));
                Assert.That(consoleComponent.InitialShuttleAppraisal, Is.GreaterThan(0));
            });

            var sale = new ShipyardConsoleSellMessage
            {
                Actor = actor,
                UiKey = ShipyardConsoleUiKey.Key,
            };
            entities.EventBus.RaiseLocalEvent(console, sale);
        });

        // Нулевая задержка завершается в следующем ShipyardSystem.Update().
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            var consoleComponent = entities.GetComponent<ShipyardConsoleComponent>(console);

            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(balance, Is.EqualTo(InitialBalance - VesselPrice + ExpectedSaleValue));
                Assert.That(entities.EntityExists(shuttle), Is.False);
                Assert.That(consoleComponent.CurrentShuttle, Is.Null);
                Assert.That(consoleComponent.CurrentShuttlePrice, Is.Zero);
                Assert.That(consoleComponent.CurrentShuttleVessel, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CashDepositDoesNotOverflowOrDeleteCashOnFailure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var cargo = entities.System<CargoSystem>();
        var shipyard = entities.System<ShipyardSystem>();

        await server.WaitAssertion(() =>
        {
            var stationSystem = entities.System<StationSystem>();
            var station = entities.SpawnEntity("ShipyardTestStation", MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(station, testMap.Grid);

            var console = entities.SpawnEntity("ShipyardTestConsole", testMap.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var cash = entities.SpawnEntity("SpaceCash10", testMap.GridCoords);
            var bank = entities.GetComponent<StationBankAccountComponent>(station);

            Assert.That(cargo.TrySetBankAccount((station, bank), CargoAccount, int.MaxValue), Is.True);
            Assert.That(shipyard.TryDepositCash((console, null), cash, actor), Is.False);
            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(balance, Is.EqualTo(int.MaxValue));
                Assert.That(entities.EntityExists(cash), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
