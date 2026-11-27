using System.Numerics;

using Content.Server._Sunrise.Shipyard;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Interaction;
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

    [Test]
    public async Task VesselInheritsGroupAndSellRate()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            ProtoId<ShipyardVesselPrototype> vesselId = "ShipyardTestInheritedVessel";
            ProtoId<ShipyardVesselPrototype> baseId = "ShipyardTestBaseVessel";
            var vessel = prototypes.Index(vesselId);
            Assert.That(vessel.Group, Is.EqualTo("test"));
            Assert.That(vessel.SellRate, Is.EqualTo(0.25f));
            Assert.That(prototypes.TryIndex(baseId, out _), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TicketDepositDoesNotCreditCargoAccount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var shipyard = entities.System<ShipyardSystem>();
        var cargo = entities.System<CargoSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entities.SpawnEntity("ShipyardTestStation", MapCoordinates.Nullspace);
            entities.System<StationSystem>().AddGridToStation(station, testMap.Grid);
            var console = entities.SpawnEntity("ShipyardTestConsole", testMap.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var ticket = entities.SpawnEntity("SalvageTicket10", testMap.GridCoords);
            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            entities.GetComponent<ShipyardConsoleComponent>(console).UseTickets = true;
            Assert.That(cargo.TrySetBankAccount((station, bank), CargoAccount, 123), Is.True);
            Assert.That(shipyard.TryDepositTicket((console, null), ticket, actor), Is.True);
            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.That(balance, Is.EqualTo(123));
            Assert.That(entities.GetComponent<ShipyardTicketAccountComponent>(station).Balance, Is.EqualTo(10));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TicketPurchaseAndSaleLeaveCargoAccountUntouched()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var cargo = entities.System<CargoSystem>();
        EntityUid station = default;
        EntityUid console = default;
        EntityUid actor = default;

        await server.WaitAssertion(() =>
        {
            station = entities.SpawnEntity("ShipyardTestStation", MapCoordinates.Nullspace);
            entities.System<StationSystem>().AddGridToStation(station, testMap.Grid);
            console = entities.SpawnEntity("ShipyardTestConsole", testMap.GridCoords);
            actor = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            Assert.That(cargo.TrySetBankAccount((station, bank), CargoAccount, 123), Is.True);
            entities.AddComponent<ShipyardTicketAccountComponent>(station).Balance = 1000;
            var component = entities.GetComponent<ShipyardConsoleComponent>(console);
            component.UseTickets = true;
            component.PurchaseDelay = TimeSpan.Zero;
            component.SaleDelay = TimeSpan.Zero;
            entities.EventBus.RaiseLocalEvent(console, new ShipyardConsolePurchaseMessage(TestVesselId)
            {
                Actor = actor,
                UiKey = ShipyardConsoleUiKey.Key,
            });
            Assert.That(entities.GetComponent<ShipyardTicketAccountComponent>(station).Balance, Is.EqualTo(500));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var component = entities.GetComponent<ShipyardConsoleComponent>(console);
            Assert.That(component.CurrentShuttle, Is.Not.Null);
            Assert.That(component.CurrentShuttlePrice, Is.EqualTo(500));
            entities.EventBus.RaiseLocalEvent(console, new ShipyardConsoleSellMessage
            {
                Actor = actor,
                UiKey = ShipyardConsoleUiKey.Key,
            });
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.That(balance, Is.EqualTo(123));
            Assert.That(entities.GetComponent<ShipyardTicketAccountComponent>(station).Balance, Is.EqualTo(750));
        });
        await pair.CleanReturnAsync();
    }

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
          id: ShipyardTestBaseVessel
          abstract: true
          group: test
          sellRate: 0.25

        - type: shipyardVessel
          id: ShipyardTestInheritedVessel
          parent: ShipyardTestBaseVessel
          name: shipyard-vessel-salvage-mining-name
          price: 1000
          gridPath: /Maps/Shuttles/shittle.yml

        - type: shipyardVessel
          id: ShipyardTestVessel
          name: shipyard-vessel-salvage-mining-name
          description: shipyard-vessel-salvage-mining-description
          price: 1000
          group: test
          gridPath: /Maps/Shuttles/shittle.yml

        - type: shipyardVessel
          id: ShipyardTestVesselCustomRate
          name: shipyard-vessel-salvage-mining-name
          description: shipyard-vessel-salvage-mining-description
          price: 1000
          sellRate: 0.25
          group: test
          gridPath: /Maps/Shuttles/shittle.yml
        """;

    [TestCase(TestVesselId, ExpectedSaleValue)]
    [TestCase("ShipyardTestVesselCustomRate", 250)]
    public async Task PurchaseAndSellShuttle(string vesselId, int expectedSaleValue)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var cargo = entities.System<CargoSystem>();
        var stationSystem = entities.System<StationSystem>();
        ProtoId<ShipyardVesselPrototype> testVessel = vesselId;

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
                Assert.That(balance, Is.EqualTo(InitialBalance - VesselPrice + expectedSaleValue));
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

    [Test]
    public async Task SellMessageOutOfRangeDoesNotQueueSale()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var cargo = entities.System<CargoSystem>();
        var interaction = entities.System<SharedInteractionSystem>();
        var shipyard = entities.System<ShipyardSystem>();
        var stationSystem = entities.System<StationSystem>();
        var transform = entities.System<SharedTransformSystem>();

        EntityUid station = default;
        EntityUid console = default;
        EntityUid actor = default;
        EntityUid shuttle = default;
        var accountInitialized = false;

        await server.WaitPost(() =>
        {
            station = entities.SpawnEntity("ShipyardTestStation", MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(station, testMap.Grid);

            console = entities.SpawnEntity("ShipyardTestConsole", testMap.GridCoords);
            actor = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            shuttle = entities.SpawnEntity(null, testMap.GridCoords);

            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            accountInitialized = cargo.TrySetBankAccount((station, bank), CargoAccount, InitialBalance);

            var consoleComponent = entities.GetComponent<ShipyardConsoleComponent>(console);
            consoleComponent.CurrentShuttle = shuttle;
            consoleComponent.CurrentShuttlePrice = VesselPrice;
            consoleComponent.SaleDelay = TimeSpan.Zero;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(accountInitialized, Is.True);
            Assert.That(interaction.InRangeAndAccessible((actor, null), (console, null)), Is.True);
            Assert.That(shipyard.CanSell((console, null), actor, out var reason), Is.True);
            Assert.That(reason, Is.Null);
        });

        await server.WaitPost(() =>
        {
            var consolePosition = transform.GetWorldPosition(console);
            transform.SetWorldPosition(actor,
                consolePosition + new Vector2(SharedInteractionSystem.InteractionRange + 10f, 0f));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(interaction.InRangeAndAccessible((actor, null), (console, null)), Is.False);
            Assert.That(shipyard.CanSell((console, null), actor, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("interaction-system-user-interaction-cannot-reach"));
        });

        await server.WaitPost(() =>
        {
            var sale = new ShipyardConsoleSellMessage
            {
                Actor = actor,
                UiKey = ShipyardConsoleUiKey.Key,
            };
            entities.EventBus.RaiseLocalEvent(console, sale);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var bank = entities.GetComponent<StationBankAccountComponent>(station);
            var consoleComponent = entities.GetComponent<ShipyardConsoleComponent>(console);

            Assert.That(cargo.TryGetAccount((station, bank), CargoAccount, out var balance), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(balance, Is.EqualTo(InitialBalance));
                Assert.That(entities.EntityExists(shuttle), Is.True);
                Assert.That(consoleComponent.CurrentShuttle, Is.EqualTo(shuttle));
            });
        });

        await pair.CleanReturnAsync();
    }
}
