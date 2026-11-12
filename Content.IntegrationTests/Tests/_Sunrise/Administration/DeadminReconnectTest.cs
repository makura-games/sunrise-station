#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Robust.Client;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Sunrise.Administration;

[TestFixture]
public sealed class DeadminReconnectTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        DummyTicker = false,
        Connected = true,
        Dirty = true,
    };

    /// <summary>
    /// Админ, сделавший deadmin, остаётся деадмином после переподключения, а после readmin права возвращаются.
    /// </summary>
    [Test]
    public async Task DeadminSurvivesReconnect()
    {
        var adminMan = Server.ResolveDependency<IAdminManager>();
        var playerMan = Server.ResolveDependency<IPlayerManager>();

        var player = playerMan.Sessions.Single();
        // Локальный клиент тестов получает права хоста (console.loginlocal).
        Assert.That(adminMan.IsAdmin(player), "Клиент тестов не админ, проверять нечего");

        await Server.WaitPost(() => adminMan.DeAdmin(player));
        Assert.That(adminMan.IsAdmin(player), Is.False);

        player = await Reconnect(player.Name);

        Assert.Multiple(() =>
        {
            Assert.That(adminMan.IsAdmin(player), Is.False, "После переподключения админка включилась сама");
            Assert.That(adminMan.IsAdmin(player, includeDeAdmin: true), "После переподключения пропали права админа");
        });

        await Server.WaitPost(() => adminMan.ReAdmin(player));
        player = await Reconnect(player.Name);

        Assert.That(adminMan.IsAdmin(player), "После readmin и переподключения права не вернулись");
    }

    /// <summary>
    /// Отключает клиента, подключает его снова под тем же именем и возвращает новую сессию.
    /// </summary>
    private async Task<ICommonSession> Reconnect(string username)
    {
        var playerMan = Server.ResolveDependency<IPlayerManager>();
        var netManager = Client.ResolveDependency<IClientNetManager>();
        var baseClient = Client.ResolveDependency<IBaseClient>();

        await Client.WaitAssertion(() => netManager.ClientDisconnect("Deadmin reconnect test"));
        await Pair.RunTicksSync(5);
        Assert.That(playerMan.Sessions, Is.Empty);

        Client.SetConnectTarget(Server);
        await Client.WaitPost(() =>
        {
            baseClient.PlayerNameOverride = username;
            baseClient.ConnectToServer(new System.Net.DnsEndPoint("localhost", 1212));
        });
        // Права загружаются асинхронно после входа, даём им время.
        await Pair.RunTicksSync(10);

        var player = playerMan.Sessions.Single();
        Assert.That(player.Status, Is.EqualTo(SessionStatus.InGame));
        return player;
    }
}
