using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Максимальный возраст аккаунта, при котором новый игрок считается пришедшим в SS14 впервые.
    /// </summary>
    public static readonly CVarDef<TimeSpan> MetricsNewAccountThreshold =
        CVarDef.Create("metrics.retention.new_account_threshold", TimeSpan.FromDays(7), CVar.SERVERONLY);

    /// <summary>
    /// Общее игровое время, ниже которого игрок считается новичком в метриках удержания.
    /// </summary>
    public static readonly CVarDef<TimeSpan> MetricsNewbiePlaytime =
        CVarDef.Create("metrics.retention.newbie_playtime", TimeSpan.FromHours(20), CVar.SERVERONLY);

    /// <summary>
    /// Как часто пересчитываются метрики аудитории из базы данных.
    /// </summary>
    public static readonly CVarDef<TimeSpan> MetricsAudienceRefreshInterval =
        CVarDef.Create("metrics.retention.audience_refresh_interval", TimeSpan.FromMinutes(10), CVar.SERVERONLY);

    /// <summary>
    /// Смещение часового пояса в часах для границ календарных дней в когортах удержания.
    /// </summary>
    public static readonly CVarDef<int> MetricsRetentionUtcOffsetHours =
        CVarDef.Create("metrics.retention.utc_offset_hours", 3, CVar.SERVERONLY);
}
