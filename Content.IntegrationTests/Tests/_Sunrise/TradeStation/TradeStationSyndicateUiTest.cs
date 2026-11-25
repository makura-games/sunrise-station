using System.Linq;
using Content.Client.Cargo.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Sunrise.TradeStation;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sunrise.TradeStation;

public sealed class TradeStationSyndicateUiTest : InteractionTest
{
    [TestCase(true, true, true)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    public async Task SyndicateItemIsShownOnlyToEligibleVisitor(bool antagonist, bool hijacked, bool expected)
    {
        await Server.WaitPost(() =>
        {
            SEntMan.AddComponent<SunriseTradeStationGridComponent>(MapData.Grid);
            SEntMan.AddComponent<TradeStationComponent>(MapData.Grid).Hacked = hijacked;
            if (!antagonist)
                return;

            var minds = SEntMan.System<MindSystem>();
            var mind = minds.CreateMind(ClientSession.UserId);
            minds.TransferTo(mind.Owner, SPlayer);
            SEntMan.System<RoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
        });

        await SpawnTarget("SunriseComputerTradeStationOrders");
        await Server.WaitPost(() => SUiSys.OpenUi(STarget!.Value, CargoConsoleUiKey.Orders, SPlayer));
        await RunTicks(30);
        await Client.WaitAssertion(() => AssertSyndicateProduct(expected));
    }

    private void AssertSyndicateProduct(bool expected)
    {
        var window = GetWindow<CargoConsoleMenu>();
        Assert.That(window.SyndicateAvailable, Is.EqualTo(expected));
        var products = GetControlFromField<BoxContainer>("Products", window);
        Assert.That(products.Children.OfType<CargoProductRow>().Any(row => row.Product?.ID == "SunriseTradeStationSyndicateSurplus"), Is.EqualTo(expected));
    }

    [Test]
    public async Task ProductRemainsAfterHijackAndLaterAntagonistAssignment()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.AddComponent<SunriseTradeStationGridComponent>(MapData.Grid);
            SEntMan.AddComponent<TradeStationComponent>(MapData.Grid).Hacked = true;
        });
        await SpawnTarget("SunriseComputerTradeStationOrders");
        await Server.WaitPost(() => SUiSys.OpenUi(STarget!.Value, CargoConsoleUiKey.Orders, SPlayer));
        await RunTicks(30);
        await Client.WaitAssertion(() => AssertSyndicateProduct(false));
        await Client.WaitPost(() => GetWindow<CargoConsoleMenu>().Close());
        await RunTicks(10);
        await Server.WaitPost(() =>
        {
            var minds = SEntMan.System<MindSystem>();
            var mind = minds.CreateMind(ClientSession.UserId);
            minds.TransferTo(mind.Owner, SPlayer);
            SEntMan.System<RoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
        });
        for (var i = 0; i < 2; i++)
        {
            await Client.WaitPost(() => CUiSys.OpenUi(CTarget!.Value, CargoConsoleUiKey.Orders, CPlayer, predicted: true));
            await RunTicks(30);
            await Client.WaitAssertion(() => AssertSyndicateProduct(true));
            await Client.WaitPost(() => GetWindow<CargoConsoleMenu>().Close());
            await RunTicks(10);
        }
    }
}
