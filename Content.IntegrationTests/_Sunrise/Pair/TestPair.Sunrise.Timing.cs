using Content.IntegrationTests._Sunrise.Patches;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    private const string ProfileSystemsEnvironmentVariable = "SUNRISE_TEST_PROFILE_SYSTEMS";
    private const string ProfileEventsEnvironmentVariable = "SUNRISE_TEST_PROFILE_EVENTS";

    private static bool ProfileSystems => Environment.GetEnvironmentVariable(ProfileSystemsEnvironmentVariable) == "1";
    private static bool ProfileEvents => Environment.GetEnvironmentVariable(ProfileEventsEnvironmentVariable) == "1";

    private async Task InitializeTimingDiagnostics()
    {
        if (ProfileSystems)
        {
            SystemTimingPatch.EnableMetrics(Server.EntMan.EntitySysManager);
            await SystemTimingPatch.TakeSnapshot();
        }

        if (ProfileEvents)
        {
            EventTimingSummaryPatch.Apply();
            await EventTimingSummaryPatch.TakeSnapshot();
        }
    }

    private async Task PrintTimingDiagnostics()
    {
        if (ProfileSystems)
            await SystemTimingPatch.PrintTop10(TestOut);

        if (ProfileEvents)
            await EventTimingSummaryPatch.PrintTop10(TestOut);
    }

    private async Task ResetTimingDiagnostics()
    {
        if (ProfileSystems)
        {
            SystemTimingPatch.EnableMetrics(Server.EntMan.EntitySysManager);
            await SystemTimingPatch.TakeSnapshot();
        }

        if (ProfileEvents)
            await EventTimingSummaryPatch.TakeSnapshot();
    }
}
