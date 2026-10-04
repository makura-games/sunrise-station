using System.Text.Json.Serialization;
using Content.Server.Database;
using Content.Shared.Database;
using System.Text.Json;

namespace Content.Server._Sunrise.ExternalServices;


public sealed class VictoriaAdminLog(AdminLog log)
{
    public int RoundId { get; } = log.RoundId;

    public int Id { get; } = log.Id;

    public Round Round { get; } = log.Round;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogType Type { get; } = log.Type;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogImpact Impact { get; } = log.Impact;

    [JsonPropertyName("_time")]
    public string Date { get; } = log.Date.ToString("o"); // VictoriaLogs требует формат ISO 8601, что и делает параметр o

    [JsonPropertyName("_msg")]
    public string Message { get; } = log.Message;

    [JsonPropertyName("jsonb")]
    public JsonDocument Json { get; } = log.Json;

    public List<AdminLogPlayer> Players { get; } = log.Players;
}
