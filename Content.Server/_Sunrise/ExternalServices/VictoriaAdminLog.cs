using System.Text.Json.Serialization;
using Content.Server.Database;
using Content.Shared.Database;
using System.Text.Json;

namespace Content.Server._Sunrise.ExternalServices;


/// <summary>
/// Базовый класс для логов в VictoriaLogs.
/// Имеет все необходимые базовые поля для работы.
/// </summary>
/// <param name="message">Сообщение лога, которое будет храниться</param>
/// <param name="time">Время создания лога</param>
/// <param name="server">Айди сервера(словесное) для создания stream внутри VictoriaLogs принадлежащего этому серверу</param>
/// <param name="logSourceType"> Переменная классифицирующая логи по типу источника. Служит для разделения логов сервера
/// на разные стримы для упрощенного поиска.
/// <para>Например, админлоги и логи сервера будут храниться под общим server_id=sunrise, но держать их вместе нет
/// смысла. Намного быстрее и лучше будет создать разные стримы для админлогов и логов сервера</para>
/// </param>
public abstract class BaseVictoriaLog(string message, DateTime time, string server, string logSourceType)
{
    [JsonPropertyName("_msg")]
    public string Message { get; init; } = message;

    [JsonPropertyName("_time")]
    public string Time { get; init; } = time.ToString("O"); // VictoriaLogs требует формат ISO 8601, что и делает параметр o

    [JsonPropertyName(VictoriaLogs.ServerId)]
    public string Server { get; init; } = server;

    [JsonPropertyName(VictoriaLogs.LogSourceType)]
    public string LogSourceType { get; init; } = logSourceType;
}

/// <summary>
/// Класс-обертка для логов под VictoriaLogs для админлогов.
/// </summary>
/// <param name="log">Объект <see cref="AdminLog"/></param>
/// <param name="server">Наименование сервера для создания или поиска stream внутри VictoriaLogs</param>
public sealed class VictoriaAdminLog(AdminLog log, string server) : BaseVictoriaLog(log.Message, log.Date, server, "admin_log")
{
    public int RoundId { get; } = log.RoundId;

    public int Id { get; } = log.Id;

    public Round Round { get; } = log.Round;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogType Type { get; } = log.Type;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogImpact Impact { get; } = log.Impact;

    [JsonPropertyName("jsonb")]
    public JsonDocument Json { get; } = log.Json;

    public List<AdminLogPlayer> Players { get; } = log.Players;
}
