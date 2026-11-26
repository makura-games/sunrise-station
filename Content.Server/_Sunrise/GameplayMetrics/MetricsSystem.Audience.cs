using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Prometheus;

namespace Content.Server._Sunrise.GameplayMetrics;

/// <summary>
/// Метрики аудитории из базы данных: уникальные и новые игроки за окно и когортное удержание новых игроков.
/// </summary>
public sealed partial class MetricsSystem
{
    private static readonly AudienceWindow[] AudienceWindows =
    [
        new("1d", TimeSpan.FromDays(1)),
        new("7d", TimeSpan.FromDays(7)),
        new("30d", TimeSpan.FromDays(30)),
    ];

    // Дни считаются от календарного дня первого входа: d1 — вернулся на следующий день, w1 — в любой из дней 1–7.
    private static readonly RetentionSpec[] RetentionSpecs =
    [
        new("d1", 1, 1),
        new("d7", 7, 7),
        new("d30", 30, 30),
        new("w1", 1, 7),
    ];

    private static readonly Gauge AudienceUniquePlayers = Metrics.CreateGauge(
        "ss14_audience_unique_players",
        "Unique players that successfully connected to this server within the rolling window.",
        new GaugeConfiguration { LabelNames = ["window"] });

    private static readonly Gauge AudienceNewPlayers = Metrics.CreateGauge(
        "ss14_audience_new_players",
        "Unique players first seen in the database within the rolling window that connected to this server.",
        new GaugeConfiguration { LabelNames = ["window"] });

    private static readonly Gauge RetentionCohortPlayers = Metrics.CreateGauge(
        "ss14_retention_cohort_players",
        "New players of the latest fully observed cohort day for each retention period.",
        new GaugeConfiguration { LabelNames = ["retention"] });

    private static readonly Gauge RetentionReturnedPlayers = Metrics.CreateGauge(
        "ss14_retention_returned_players",
        "Players of the cohort that came back to this server during the retention period.",
        new GaugeConfiguration { LabelNames = ["retention"] });

    private static readonly Gauge AudienceMetricsLastRefreshUnixTime = Metrics.CreateGauge(
        "ss14_audience_metrics_last_refresh_unixtime",
        "Unix timestamp of the last successful audience metrics refresh.");

    private CancellationTokenSource? _audienceCancel;
    private TimeSpan _audienceRefreshInterval;
    private TimeSpan _nextAudienceRefresh;
    private TimeSpan _retentionUtcOffset;
    private bool _audienceRefreshing;

    private void InitializeAudience()
    {
        _audienceCancel = new CancellationTokenSource();

        Subs.CVar(_cfg, SunriseCCVars.MetricsAudienceRefreshInterval, value => _audienceRefreshInterval = value, true);
        Subs.CVar(_cfg, SunriseCCVars.MetricsRetentionUtcOffsetHours,
            value => _retentionUtcOffset = TimeSpan.FromHours(value), true);
    }

    private void ShutdownAudience()
    {
        _audienceCancel?.Cancel();
        _audienceCancel?.Dispose();
        _audienceCancel = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_audienceRefreshing || _audienceCancel == null)
            return;

        var now = _gameTiming.RealTime;
        if (now < _nextAudienceRefresh)
            return;

        _nextAudienceRefresh = now + _audienceRefreshInterval;
        _audienceRefreshing = true;
        _ = RefreshAudienceMetricsAsync(_audienceCancel.Token);
    }

    private async Task RefreshAudienceMetricsAsync(CancellationToken cancel)
    {
        try
        {
            var server = await _dbEntry.ServerEntity;
            var now = DateTime.UtcNow;

            foreach (var window in AudienceWindows)
            {
                var counts = await _db.GetAudienceCountsAsync(server.Id, now - window.Length, cancel);
                AudienceUniquePlayers.WithLabels(window.Label).Set(counts.UniquePlayers);
                AudienceNewPlayers.WithLabels(window.Label).Set(counts.NewPlayers);
            }

            var offset = _retentionUtcOffset;
            var today = ToLocalDay(now, offset);
            var maxLastDay = 0;
            foreach (var spec in RetentionSpecs)
            {
                maxLastDay = Math.Max(maxLastDay, spec.LastDay);
            }

            // Самая старая когорта — today - (maxLastDay + 1), самая свежая — today - 2.
            var firstSeenFrom = FromLocalDay(today.AddDays(-(maxLastDay + 1)), offset);
            var firstSeenTo = FromLocalDay(today.AddDays(-1), offset);
            var connections = await _db.GetNewPlayerConnectionsAsync(server.Id, firstSeenFrom, firstSeenTo, cancel);

            ApplyRetention(connections, today, offset);
            AudienceMetricsLastRefreshUnixTime.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Система выгружается, обновление больше не нужно.
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to refresh audience metrics: {e}");
        }
        finally
        {
            _audienceRefreshing = false;
        }
    }

    private static void ApplyRetention(List<NewPlayerConnection> connections, DateTime today, TimeSpan offset)
    {
        var players = new Dictionary<Guid, (DateTime FirstDay, HashSet<DateTime> ActiveDays)>();
        foreach (var connection in connections)
        {
            if (!players.TryGetValue(connection.UserId, out var player))
            {
                player = (ToLocalDay(connection.FirstSeenTime, offset), new HashSet<DateTime>());
                players[connection.UserId] = player;
            }

            player.ActiveDays.Add(ToLocalDay(connection.ConnectionTime, offset));
        }

        foreach (var spec in RetentionSpecs)
        {
            // Берём самую свежую когорту, у которой период возврата уже полностью прошёл.
            var cohortDay = today.AddDays(-(spec.LastDay + 1));
            var cohort = 0;
            var returned = 0;

            foreach (var (firstDay, activeDays) in players.Values)
            {
                // В когорту входят только новые игроки, в первый же день зашедшие именно на этот сервер.
                if (firstDay != cohortDay || !activeDays.Contains(cohortDay))
                    continue;

                cohort++;

                for (var day = spec.FirstDay; day <= spec.LastDay; day++)
                {
                    if (!activeDays.Contains(cohortDay.AddDays(day)))
                        continue;

                    returned++;
                    break;
                }
            }

            RetentionCohortPlayers.WithLabels(spec.Label).Set(cohort);
            RetentionReturnedPlayers.WithLabels(spec.Label).Set(returned);
        }
    }

    private static DateTime ToLocalDay(DateTime utc, TimeSpan offset)
    {
        return DateTime.SpecifyKind((utc + offset).Date, DateTimeKind.Unspecified);
    }

    private static DateTime FromLocalDay(DateTime localDay, TimeSpan offset)
    {
        return DateTime.SpecifyKind(localDay - offset, DateTimeKind.Utc);
    }

    private readonly record struct AudienceWindow(string Label, TimeSpan Length);

    private readonly record struct RetentionSpec(string Label, int FirstDay, int LastDay);
}
