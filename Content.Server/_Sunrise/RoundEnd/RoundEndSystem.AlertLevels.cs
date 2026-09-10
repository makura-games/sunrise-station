using Content.Server.AlertLevel;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    /*
     * Emergency-shuttle timing derived from all active alert levels.
     */

    private static TimeSpan GetAlertLevelShuttleTime(
        Entity<AlertLevelComponent> station,
        TimeSpan fallback)
    {
        if (station.Comp.AlertLevels is not { } alertLevels)
            return fallback;

        var duration = alertLevels.Levels.TryGetValue(station.Comp.CurrentLevel, out var currentDetail)
            ? currentDetail.ShuttleTime
            : fallback;

        foreach (var additionalLevel in station.Comp.ActiveAdditionalLevels)
        {
            if (alertLevels.Levels.TryGetValue(additionalLevel, out var detail)
                && detail.ShuttleTime > duration)
            {
                duration = detail.ShuttleTime;
            }
        }

        return duration;
    }
}
