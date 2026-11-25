using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Cargo.Systems;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Sunrise.TradeStation;
using Content.Shared.Cargo.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

[TestFixture]
public sealed class TradeStationBlackMarketTest : GameTest
{
    [Test]
    public async Task EmagTemporarilyRestrictsConsoleAndRestoresCatalogue()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        EntityUid console = default;
        EntityUid user = default;
        var handled = false;
        var repeatable = false;
        var denied = false;
        var allowedWithCard = false;
        var renewed = false;
        var pickedUp = false;
        var syndicateDenied = false;

        await server.WaitPost(() =>
        {
            entities.AddComponent<SunriseTradeStationGridComponent>(map.Grid);
            entities.AddComponent<TradeStationComponent>(map.Grid).Hacked = true;
            console = entities.SpawnEntity("SunriseComputerTradeStationOrders", map.GridCoords);
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var minds = entities.System<MindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind.Owner, user);
            entities.System<RoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
            var emag = new GotEmaggedEvent(user, EmagType.Interaction);
            entities.EventBus.RaiseLocalEvent(console, ref emag);
            syndicateDenied = !entities.System<CargoSystem>().CanUseTradeStationSyndicate(console, user);
            handled = emag.Handled;
            repeatable = emag.Repeatable;
            var attempt = new ActivatableUIOpenAttemptEvent(user, false);
            entities.EventBus.RaiseLocalEvent(console, attempt);
            denied = attempt.Cancelled;

            var id = entities.SpawnEntity("PirateIDCard", map.GridCoords);
            pickedUp = entities.System<SharedHandsSystem>().TryPickupAnyHand(user, id);
            allowedWithCard = entities.System<CargoSystem>().CanUseTradeStationBlackMarket(console, user);

            var state = entities.GetComponent<SunriseTradeStationBlackMarketComponent>(console);
            state.EndTime = TimeSpan.Zero;
            var secondEmag = new GotEmaggedEvent(user, EmagType.Interaction);
            entities.EventBus.RaiseLocalEvent(console, ref secondEmag);
            renewed = secondEmag.Handled && secondEmag.Repeatable && state.EndTime > TimeSpan.FromMinutes(4);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(repeatable, Is.True);
            Assert.That(denied, Is.True);
            Assert.That(pickedUp, Is.True, "Не удалось положить карту в руку тестового персонажа.");
            Assert.That(allowedWithCard, Is.True);
            Assert.That(syndicateDenied, Is.True);
            Assert.That(renewed, Is.True);
            Assert.That(entities.GetComponent<CargoOrderConsoleComponent>(console).AllowedGroups.Select(id => id.Id), Is.EqualTo(new[] { "pirateMarket" }));
            Assert.That(entities.System<SharedAppearanceSystem>().TryGetData<bool>(console, SunriseTradeStationConsoleVisuals.BlackMarket, out var pirateScreen) && pirateScreen, Is.True);
        });

        await server.WaitPost(() => entities.GetComponent<SunriseTradeStationBlackMarketComponent>(console).EndTime = TimeSpan.Zero);
        await Pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<SunriseTradeStationBlackMarketComponent>(console), Is.False);
            Assert.That(entities.GetComponent<CargoOrderConsoleComponent>(console).AllowedGroups.Select(id => id.Id), Does.Contain("market"));
            Assert.That(entities.System<SharedAppearanceSystem>().TryGetData<bool>(console, SunriseTradeStationConsoleVisuals.BlackMarket, out var pirateScreen) && pirateScreen, Is.False);
        });
    }
}
