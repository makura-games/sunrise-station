using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Prometheus;
using Robust.Server.DataMetrics;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Tutorial;

/// <summary>
/// Exposes aggregated tutorial completion table data through Prometheus.
/// </summary>
public sealed partial class TutorialMetricsSystem : EntitySystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IMetricsManager _metrics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITaskManager _task = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private static readonly Gauge TutorialFirstTimeCompletedPlayers = Metrics.CreateGauge(
        "ss14_tutorial_completed_players",
        "Players that completed each tutorial sequence for the first time.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialNewAccountCompletedPlayers = Metrics.CreateGauge(
        "ss14_tutorial_new_account_completed_players",
        "Players that first completed each tutorial sequence while their account was new.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialCompletionCount = Metrics.CreateGauge(
        "ss14_tutorial_completion_count",
        "Total completion count recorded for each tutorial sequence.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Counter TutorialStartedTotal = Metrics.CreateCounter(
        "ss14_tutorial_started_total",
        "Tutorial sequence starts, used to measure tutorial popularity.",
        new CounterConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialAccountAgeSamples = Metrics.CreateGauge(
        "ss14_tutorial_account_age_samples",
        "First tutorial completion rows with account age data.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialAverageAccountAgeDays = Metrics.CreateGauge(
        "ss14_tutorial_average_account_age_days",
        "Average account age in days when each tutorial sequence was first completed.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialLastCompletedAtUnixTime = Metrics.CreateGauge(
        "ss14_tutorial_last_completed_at_unixtime",
        "Unix timestamp of the latest completion for each tutorial sequence.",
        new GaugeConfiguration { LabelNames = ["tutorial_id"] });

    private static readonly Gauge TutorialMetricsLastRefreshUnixTime = Metrics.CreateGauge(
        "ss14_tutorial_metrics_last_refresh_unixtime",
        "Unix timestamp of the last successful tutorial metrics refresh.");

    private static readonly Gauge TutorialNewAccountThresholdSeconds = Metrics.CreateGauge(
        "ss14_tutorial_new_account_threshold_seconds",
        "Maximum account age counted as a new account for tutorial completion metrics.");

    private ISawmill _sawmill = default!;
    private CancellationTokenSource? _shutdownToken;
    private TimeSpan _nextRefresh;
    private TimeSpan _newAccountThreshold;
    private bool _refreshing;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("tutorial.metrics");
        _shutdownToken = new CancellationTokenSource();
        Subs.CVar(_cfg, SunriseCCVars.TutorialNewAccountThreshold, value => _newAccountThreshold = value, true);
        _metrics.UpdateMetrics += OnUpdateMetrics;
        TryRefreshTutorialMetrics(force: true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _metrics.UpdateMetrics -= OnUpdateMetrics;
        _shutdownToken?.Cancel();
        _shutdownToken?.Dispose();
        _shutdownToken = null;
    }

    public void RecordTutorialStarted(string tutorialId)
    {
        TutorialStartedTotal.WithLabels(tutorialId).Inc();
    }

    private void OnUpdateMetrics()
    {
        TryRefreshTutorialMetrics(force: false);
    }

    private void TryRefreshTutorialMetrics(bool force)
    {
        if (_refreshing)
            return;

        var now = _timing.RealTime;
        if (!force && _nextRefresh > now)
            return;

        _nextRefresh = now + RefreshInterval;
        _refreshing = true;

        var cancel = _shutdownToken?.Token ?? CancellationToken.None;
        _ = RefreshTutorialMetricsAsync(cancel);
    }

    private async Task RefreshTutorialMetricsAsync(CancellationToken cancel)
    {
        List<TutorialCompletionMetrics>? metrics = null;
        Exception? error = null;
        var newAccountThreshold = _newAccountThreshold < TimeSpan.Zero
            ? TimeSpan.Zero
            : _newAccountThreshold;

        try
        {
            metrics = await _db.GetTutorialCompletionMetricsAsync(newAccountThreshold, cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception e)
        {
            error = e;
        }

        _task.RunOnMainThread(() =>
        {
            if (_shutdownToken == null || cancel.IsCancellationRequested)
                return;

            if (error != null)
                _sawmill.Warning("Failed to refresh tutorial metrics: {0}", error);
            else if (metrics != null)
                ApplyTutorialMetrics(metrics, newAccountThreshold);

            _refreshing = false;
        });
    }

    private static void ApplyTutorialMetrics(
        List<TutorialCompletionMetrics> metrics,
        TimeSpan newAccountThreshold)
    {
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i];
            var tutorialId = metric.TutorialId;

            TutorialFirstTimeCompletedPlayers.WithLabels(tutorialId).Set(metric.FirstTimeCompletedPlayers);
            TutorialNewAccountCompletedPlayers.WithLabels(tutorialId).Set(metric.NewAccountCompletedPlayers);
            TutorialCompletionCount.WithLabels(tutorialId).Set(metric.CompletionCount);
            TutorialAccountAgeSamples.WithLabels(tutorialId).Set(metric.AccountAgeSamples);
            TutorialAverageAccountAgeDays.WithLabels(tutorialId).Set(metric.AverageAccountAgeDays ?? 0);
            TutorialLastCompletedAtUnixTime.WithLabels(tutorialId).Set(metric.LastCompletedAt.ToUnixTimeSeconds());
        }

        TutorialNewAccountThresholdSeconds.Set(newAccountThreshold.TotalSeconds);
        TutorialMetricsLastRefreshUnixTime.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
