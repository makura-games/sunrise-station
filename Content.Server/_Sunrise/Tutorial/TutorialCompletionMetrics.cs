#pragma warning disable IDE0130
namespace Content.Server.Database;
#pragma warning restore IDE0130

/// <summary>
/// Aggregated tutorial completion data used by Prometheus metrics.
/// </summary>
public sealed record TutorialCompletionMetrics(
    string TutorialId,
    int FirstTimeCompletedPlayers,
    int NewAccountCompletedPlayers,
    int CompletionCount,
    int AccountAgeSamples,
    double? AverageAccountAgeDays,
    DateTimeOffset LastCompletedAt);
