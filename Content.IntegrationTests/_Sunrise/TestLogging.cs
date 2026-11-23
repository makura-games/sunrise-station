using Robust.Shared.Log;

namespace Content.IntegrationTests._Sunrise;

/// <summary>
/// Общие настройки подробности журналов интеграционных тестов.
/// </summary>
internal static class TestLogging
{
    private const string VerboseEnvironmentVariable = "SUNRISE_TEST_VERBOSE_LOGS";

    internal static bool Verbose => Environment.GetEnvironmentVariable(VerboseEnvironmentVariable) == "1";
    internal static string DefaultLevel => Verbose ? nameof(LogLevel.Info) : nameof(LogLevel.Warning);
}
