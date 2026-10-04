using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{

    /// <summary>
    /// Включена ли поддержка Victoria Logs
    /// </summary>
    public static readonly CVarDef<bool> VictoriaLogsEnabled =
        CVarDef.Create("victoria_logs.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Включена ли поддержка Victoria Logs
    /// </summary>
    public static readonly CVarDef<string> VictoriaLogsBaseAddress =
        CVarDef.Create("victoria_logs.base_address", "http://127.0.0.1:9428", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Будет ли работать сохранение логов в базу данных(стандартный способ хранения логов).
    /// При false VictoriaLogs заменит собой стандартное хранение.
    /// </summary>
    public static readonly CVarDef<bool> VictoriaLogsStoreInDatabase =
        CVarDef.Create("victoria_logs.store_in_database", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
