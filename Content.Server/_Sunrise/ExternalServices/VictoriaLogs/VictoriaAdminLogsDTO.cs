using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Server.Database;
using Content.Shared.Database;

namespace Content.Server._Sunrise.ExternalServices.VictoriaLogs;


/// <summary>
/// Базовый интерфейс для логов в VictoriaLogs.
/// Имеет все необходимые базовые поля для работы.
/// </summary>
public interface IBaseVictoriaLog
{
    /// <summary>
    /// Сообщение лога, которое будет храниться
    /// </summary>
    [JsonPropertyName(VictoriaLogs.MessageFieldName)]
    public string Message { get; init; }

    /// <summary>
    /// Время создания лога
    /// </summary>
    [JsonPropertyName(VictoriaLogs.TimeFieldName)]
    public DateTime Time { get; init; }

    /// <summary>
    /// Айди сервера(словесное) для создания stream внутри VictoriaLogs принадлежащего этому серверу
    /// </summary>
    [JsonPropertyName(VictoriaLogs.ServerIdFieldName)]
    public string Server { get; init; }

    /// <summary>
    /// Переменная классифицирующая логи по типу источника. Служит для разделения логов сервера
    /// на разные стримы для упрощенного поиска.
    /// <para>
    /// Например, админлоги и логи сервера будут храниться под общим server_id=sunrise, но держать их вместе нет
    /// смысла. Намного быстрее и лучше будет создать разные стримы для админлогов и логов сервера
    /// </para>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.LogSourceTypeFieldName)]
    public string LogSourceType { get; init; }
}

// Админлоги ниже

// ВАЖНОЕ ДОПУЩЕНИЕ!!
// Здесь представлены не все поля, которые присутствуют в оригинальном AdminLog
// 1. Поле Json(jsonb) удалено, потому что в коде нигде не используется. Не вижу смысла его хранить.
// Если вдруг оно станет использоваться - нужно добавить пошаманить с выборкой логов(причина по которой я не хотел тащить).
// 2. Поле Players изменено с List<AdminLogPlayer> на простой массив Guid.
// Это сделано по причине того, что данные в AdminLogPlayer не используются за исключением самого Guid
// + дублируют уже присутствующие данные(например Round)
// 3. Round удалено, потому что данные оттуда дублируют другие данные + не используется для получения реальных данных.

// По факту указанное выше это Foreign и Primary ключи для базы данных, так как оригинальный класс AdminLog для нее и создан.
// Возможно не все, но большая часть этих данных бесполезна и скорее всего никогда не будет использоваться
// Если будет - молитесь нейронке, чтобы это вставить.


public readonly record struct VictoriaAdminLogInsert : IBaseVictoriaLog
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.MessageFieldName)]
    public string Message { get; init; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.TimeFieldName)]
    public DateTime Time { get; init; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.ServerIdFieldName)]
    public string Server { get; init; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.LogSourceTypeFieldName)]
    public string LogSourceType { get; init; }

    public int RoundId { get; init; }

    public int Id { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogType Type { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogImpact Impact { get; init; }

    public Guid[] Players { get; init; }

    public VictoriaAdminLogInsert(AdminLog log, string server)
    {
        Message = log.Message;
        Time = log.Date;
        Server = server;
        LogSourceType = VictoriaLogs.AdminLogSourceType;
        RoundId = log.RoundId;
        Id = log.Id;
        Type = log.Type;
        Impact = log.Impact;
        Players = ExtractPlayers(log.Players);
    }

    private static Guid[] ExtractPlayers(List<AdminLogPlayer>? players)
    {
        if (players == null || players.Count == 0)
            return [];

        var result = new Guid[players.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = players[i].PlayerUserId;
        }
        return result;
    }
}

public readonly record struct VictoriaAdminLogResponse : IBaseVictoriaLog
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.MessageFieldName)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.TimeFieldName)]
    public DateTime Time { get; init; } = default;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.ServerIdFieldName)]
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [JsonPropertyName(VictoriaLogs.LogSourceTypeFieldName)]
    public string LogSourceType { get; init; } = string.Empty;

    // Автоматически преобразует строку "33" в число 33
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; init; } = 0;

    // Автоматически преобразует строку "49" в число 49
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int RoundId { get; init; } = 0;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogType Type { get; init; } = LogType.Unknown;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogImpact Impact { get; init; } = LogImpact.Low;

    // 1. Просто читаем сырую строку от VictoriaLogs вида "[\"uuid\", ...]"
    [JsonPropertyName("players")]
    public string? PlayersRaw { get; init; }

    // 2. В одну строчку парсим её в нормальный Guid[] при обращении к свойству
    [JsonIgnore]
    public Guid[] Players => !string.IsNullOrEmpty(PlayersRaw) && PlayersRaw != "[]"
        ? JsonSerializer.Deserialize<Guid[]>(PlayersRaw) ?? []
        : [];

    public VictoriaAdminLogResponse() { }
}
