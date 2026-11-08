// ReSharper disable once CheckNamespace
namespace Content.Server.Database;

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
