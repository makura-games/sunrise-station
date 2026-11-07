using Content.IntegrationTests._Sunrise;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    private void ConfigureLogLevels()
    {
        if (TestLogging.Verbose)
            return;

        Server.ResolveDependency<ILogManager>().RootSawmill.Level = LogLevel.Warning;
        Client.ResolveDependency<ILogManager>().RootSawmill.Level = LogLevel.Warning;
    }
}
