using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HijackBeacon;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

[TestFixture]
public sealed class TradeStationPurchaseTest : GameTest
{
    [TestCase(true, 0)]
    [TestCase(false, 1000)]
    public async Task BlackMarketPurchaseRequiresPirateCard(bool pirateCard, int expectedBalance)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid tradeAccount = default;
        EntityUid mainStation = default;
        var cardPickedUp = !pirateCard;

        await server.WaitPost(() =>
        {
            var grid = entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            tradeAccount = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            mainStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationBankAccountComponent>(mainStation);
            grid.MainStation = mainStation;
            entities.System<CargoSystem>().UpdateBankAccount(tradeAccount, 1000, "SunriseTradeStation");
            entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var buyer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            if (pirateCard)
            {
                var id = entities.SpawnEntity("PirateIDCard", new EntityCoordinates(map.Grid, 4, 4));
                cardPickedUp = entities.System<SharedHandsSystem>().TryPickupAnyHand(buyer, id);
            }

            var emag = new GotEmaggedEvent(buyer, EmagType.Interaction);
            entities.EventBus.RaiseLocalEvent(console, ref emag);
            var message = new CargoConsoleAddOrderMessage("buyer", "test", "PirateMarketDisposableLauncherTraining", 1) { Actor = buyer };
            entities.EventBus.RaiseLocalEvent(console, message);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(cardPickedUp, Is.True);
            var cargo = entities.System<CargoSystem>();
            Assert.That(cargo.GetBalanceFromAccount(tradeAccount, "SunriseTradeStation"), Is.EqualTo(expectedBalance));
            Assert.That(cargo.GetBalanceFromAccount(mainStation, "Cargo"), Is.EqualTo(2000));
        });
    }

    [TestCase(true, 0)]
    [TestCase(false, 50000)]
    public async Task SyndicatePurchaseRequiresAntagonist(bool antagonist, int expectedBalance)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid tradeAccount = default;
        EntityUid mainStation = default;

        await server.WaitPost(() =>
        {
            var grid = entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            entities.AddComponent<TradeStationComponent>(map.Grid).Hacked = true;
            tradeAccount = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            mainStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationBankAccountComponent>(mainStation);
            grid.MainStation = mainStation;
            var cargo = entities.System<CargoSystem>();
            cargo.UpdateBankAccount(tradeAccount, 50000, "SunriseTradeStation");
            entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var buyer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            if (antagonist)
            {
                var minds = entities.System<MindSystem>();
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind.Owner, buyer);
                entities.System<RoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
            }

            var message = new CargoConsoleAddOrderMessage("buyer", "test", "SunriseTradeStationSyndicateSurplus", 1) { Actor = buyer };
            entities.EventBus.RaiseLocalEvent(console, message);
        });

        await server.WaitAssertion(() =>
        {
            var cargo = entities.System<CargoSystem>();
            Assert.That(cargo.GetBalanceFromAccount(tradeAccount, "SunriseTradeStation"), Is.EqualTo(expectedBalance));
            Assert.That(cargo.GetBalanceFromAccount(mainStation, "Cargo"), Is.EqualTo(2000));
        });
    }

    [TestCase(1000, false, false, 375, 2312, true)]
    [TestCase(624, false, false, 624, 2000, false)]
    [TestCase(1000, true, false, 1000, 2000, false)]
    [TestCase(1000, false, true, 375, 2000, false)]
    public async Task PurchaseUsesAvailableFundsAndPallets(int initialBalance, bool occupied, bool hijacked, int expectedTradeBalance, int expectedMainBalance, bool announce)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid tradeAccount = default;
        EntityUid mainStation = default;
        var diagnostics = string.Empty;
        var radioChannels = new List<string>();

        await server.WaitPost(() =>
        {
            var grid = entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            var tradeStation = entities.AddComponent<TradeStationComponent>(map.Grid);
            tradeStation.Hacked = hijacked;
            tradeAccount = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            mainStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationBankAccountComponent>(mainStation);
            grid.MainStation = mainStation;

            var cargo = entities.System<CargoSystem>();
            cargo.UpdateBankAccount(tradeAccount, initialBalance, "SunriseTradeStation");
            var pallet = entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            if (occupied)
                entities.SpawnEntity("CrateGenericSteel", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var buyer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            var consoleComponent = entities.GetComponent<CargoOrderConsoleComponent>(console);
            diagnostics = $"owner={entities.System<StationSystem>().GetOwningStation(console)}, expected={tradeAccount}, " +
                          $"grid={entities.GetComponent<TransformComponent>(console).GridUid}, palletAnchored={entities.GetComponent<TransformComponent>(pallet).Anchored}, " +
                          $"available={cargo.GetAvailableProducts((console, consoleComponent)).Contains("HydroponicsTools")}";

            var message = new CargoConsoleAddOrderMessage("buyer", "test", "HydroponicsTools", 1) { Actor = buyer };
            var radio = entities.System<TradeStationRadioTestSystem>();
            radio.Source = console;
            radio.Channels.Clear();
            entities.EventBus.RaiseLocalEvent(console, message);
            radioChannels.AddRange(radio.Channels);
            radio.Source = null;
        });

        await server.WaitAssertion(() =>
        {
            var cargo = entities.System<CargoSystem>();
            Assert.That(cargo.GetBalanceFromAccount(tradeAccount, "SunriseTradeStation"), Is.EqualTo(expectedTradeBalance), diagnostics);
            Assert.That(cargo.GetBalanceFromAccount(mainStation, "Cargo"), Is.EqualTo(expectedMainBalance));
            if (announce)
                Assert.That(radioChannels, Is.EquivalentTo(new[] { "Supply" }));
            else
                Assert.That(radioChannels, Is.Empty);
        });
    }

    [TestCase("SecurityArmor")]
    [TestCase("ArmorySmg")]
    public async Task SecurityCategoriesNotifySecurity(string product)
    {
        var map = await Pair.CreateTestMap();
        var entities = Pair.Server.EntMan;
        var channels = new List<string>();

        await Pair.Server.WaitPost(() =>
        {
            var grid = entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            var account = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var mainStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationBankAccountComponent>(mainStation);
            grid.MainStation = mainStation;
            entities.System<CargoSystem>().UpdateBankAccount(account, 20000, "SunriseTradeStation");
            entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var buyer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            var radio = entities.System<TradeStationRadioTestSystem>();
            radio.Source = console;
            radio.Channels.Clear();
            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleAddOrderMessage("buyer", "test", product, 1) { Actor = buyer });
            channels.AddRange(radio.Channels);
            radio.Source = null;
        });

        Assert.That(channels, Is.EquivalentTo(new[] { "Supply", "Security" }));
    }

    [TestCase(false, false, "HydroponicsTools", "PaperCargoInvoice")]
    [TestCase(true, false, "HydroponicsTools", "PaperCargoInvoice")]
    [TestCase(true, false, "SunriseTradeStationSyndicateSurplus", "SunrisePaperCargoInvoiceHijacked")]
    [TestCase(false, true, "PirateMarketDisposableLauncherTraining", "SunrisePaperCargoInvoicePirate")]
    [TestCase(true, true, "PirateMarketDisposableLauncherTraining", "SunrisePaperCargoInvoicePirate")]
    public async Task InvoiceMatchesCatalogue(bool hijacked, bool pirate, string product, string expectedInvoice)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;

        await server.WaitPost(() =>
        {
            var grid = entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            entities.AddComponent<TradeStationComponent>(map.Grid).Hacked = hijacked;
            var account = entities.System<StationSystem>().GetOwningStation(map.Grid)!.Value;
            var mainStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent<StationBankAccountComponent>(mainStation);
            grid.MainStation = mainStation;
            if (hijacked)
            {
                var hijack = new TradeStationHijackedEvent();
                entities.EventBus.RaiseLocalEvent(map.Grid, ref hijack);
            }

            entities.System<CargoSystem>().UpdateBankAccount(account, 60000, "SunriseTradeStation");
            entities.SpawnEntity("CargoPalletBuy", map.GridCoords);
            var console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            var buyer = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 4));
            if (pirate)
            {
                var id = entities.SpawnEntity("PirateIDCard", new EntityCoordinates(map.Grid, 4, 4));
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(buyer, id), Is.True);
                var emag = new GotEmaggedEvent(buyer, EmagType.Interaction);
                entities.EventBus.RaiseLocalEvent(console, ref emag);
            }
            else if (product == "SunriseTradeStationSyndicateSurplus")
            {
                var minds = entities.System<MindSystem>();
                var mind = minds.CreateMind(null);
                minds.TransferTo(mind.Owner, buyer);
                entities.System<RoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
            }

            entities.EventBus.RaiseLocalEvent(console, new CargoConsoleAddOrderMessage("buyer", "test", product, 1) { Actor = buyer });
        });

        await server.WaitAssertion(() =>
        {
            var found = false;
            var query = entities.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
                found |= metadata.EntityPrototype?.ID == expectedInvoice;
            Assert.That(found, Is.True);
        });
    }
}

public sealed class TradeStationRadioTestSystem : EntitySystem
{
    public EntityUid? Source;
    public readonly List<string> Channels = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (args.Source == Source)
            Channels.Add(args.ChannelId);
    }
}
