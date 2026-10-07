using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.ExternalServices.VictoriaLogs;

// TODO: Выделение базовой работы с викторией от админ логов
// TODO: Оптимизации?
// TODO: Посмотреть что можно сделать с серверными логами и подумать можно ли их сразу сюда добавить
// TODO: Раскинуть документацию, описать DTO, добавить пример стандартного лога после фильтрации
// TODO: Не забыть вернуть кеширование
// TODO: Посмотреть что там с пагинацией
// TODO: Проверить как там фаталы при закрытии сервера из-за отписок
// TODO: Уменьшить лимиты на странице и посмотреть что будет
// TODO: Написать тесты, как только будет адекватный готовый парсер logsQL
public sealed partial class VictoriaLogs : IPostInjectInit, IDisposable
{
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;
    private HttpClient? _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        // Обязательно используется snake_case, потому что это стандарт для названий полей в VictoriaLogs
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private bool _enabled;
    private string _baseAddress = string.Empty;
    private string _serverId = string.Empty;

    private const string InsertQuery = $"/insert/jsonline?_stream_fields={DefaultStreamValue}";
    private const string SelectQuery = $"/select/logsql/query";

    private Uri? _insertUri;
    private Uri? _selectUri;

    public const string ServerIdFieldName = "server_id";
    public const string LogSourceTypeFieldName = "log_source_type";
    public const string TimeFieldName = "_time";
    public const string MessageFieldName = "_msg";
    private const string DefaultStreamValue = $"{ServerIdFieldName},{LogSourceTypeFieldName}";

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("VictoriaLogs");

        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged, true);
        _cfg.OnValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged, true);
    }

    private void DisposeAndNullifyClient()
    {
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        DisposeAndNullifyClient();

        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged);
        _cfg.UnsubValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged);
    }

    #region Base input/output

    public async Task<bool> TrySend(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _sawmill.Info(json);

        if (_client == null)
        {
            _sawmill.Error("Found null HttpClient while trying to send data");
            return false;
        }

        var response = await _client!.PostAsync(_insertUri, content);

        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error(response.ReasonPhrase ?? $"Found unsuccessful request with {response.StatusCode} code");
            return false;
        }

        return true;
    }

    #endregion

    #region Cvars

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (enabled && _client == null)
            _client = new();
        else if (!enabled && _client != null)
            DisposeAndNullifyClient();
    }

    private void OnBaseAddressChanged(string baseAddress)
    {
        _baseAddress = baseAddress;

        if (string.IsNullOrEmpty(_baseAddress))
            _sawmill.Error("Enabled VictoriaLogs shouldn't have empty BaseUrl CVar. Every sent request would fail instantly");

        var insertUriString = _baseAddress + InsertQuery;
        if (!Uri.TryCreate(insertUriString, UriKind.Absolute, out _insertUri))
        {
            _sawmill.Error($"Failed to create URI for insert query with {insertUriString}");
        }

        var selectUriString = _baseAddress + SelectQuery;
        if (!Uri.TryCreate(selectUriString, UriKind.Absolute, out _selectUri))
        {
            _sawmill.Error($"Failed to create URI for select query with {selectUriString}");
        }
    }

    private void OnDatabaseStoreChanged(bool enabled)
    {
        _storeInDatabase = enabled;
    }

    private void OnWatchdogKeyChanged(string key)
    {
        _serverId = string.IsNullOrEmpty(key) ? "localhost" : key;
    }

    #endregion
}

