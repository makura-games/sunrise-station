using Content.IntegrationTests.Fixtures;
using Robust.Client.Console;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Networking
{
    [TestFixture]
    public sealed class ReconnectTest : GameTest
    {
        [Test]
        public async Task Test()
        {
            var pair = Pair;
            var server = pair.Server;
            var client = pair.Client;

            var host = client.ResolveDependency<IClientConsoleHost>();
            var netManager = client.ResolveDependency<IClientNetManager>();

            await client.WaitPost(() => host.ExecuteCommand("disconnect"));

            // Run some ticks for the disconnect to complete and such.
            await pair.RunTicksSync(5);

            await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

            // Reconnect.
            client.SetConnectTarget(server);

            // Sunrise edit start - fix RobustToolbox 270.1.0 run level transitions
            var baseClient = client.ResolveDependency<Robust.Client.IBaseClient>();
            await client.WaitPost(() => baseClient.ConnectToServer(new System.Net.DnsEndPoint("localhost", 1212)));
            // Sunrise edit end

            // Run some ticks for the handshake to complete and such.
            await pair.RunTicksSync(10);

            await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());
        }
    }
}
