using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.ExternalServices.VictoriaLogs;

// TODO: Выделение базовой работы с викторией от админ логов
// TODO: Посмотреть что можно сделать с серверными логами и подумать можно ли их сразу сюда добавить
// TODO: Раскинуть документацию, описать DTO, добавить пример стандартного лога после фильтрации
// TODO: Не забыть вернуть кеширование
// TODO: Написать тесты, как только будет адекватный готовый парсер logsQL
public sealed partial class VictoriaLogs : IPostInjectInit, IDisposable
{
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private const string InsertQuery = $"/insert/jsonline?_stream_fields={DefaultStreamValue}";
    private const string SelectQuery = "/select/logsql/query";

    public const string ServerIdFieldName = "server_id";
    public const string LogSourceTypeFieldName = "log_source_type";
    public const string TimeFieldName = "_time";
    public const string MessageFieldName = "_msg";
    private const string DefaultStreamValue = $"{ServerIdFieldName},{LogSourceTypeFieldName}";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        // Обязательно используется snake_case, потому что это стандарт для названий полей в VictoriaLogs
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private ISawmill _sawmill = default!;
    private HttpClient? _client;

    private bool _enabled;
    private string _baseAddress = string.Empty;
    private string _serverId = string.Empty;

    private Uri? _insertUri;
    private Uri? _selectUri;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("VictoriaLogs");

        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged, true);
        _cfg.OnValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged, true);
    }

    #region Base input/output

    public async Task<bool> TrySend(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
#if DEBUG
        _sawmill.Verbose(json);
#endif
        try
        {
            var client = GetOrCreateClient();
            var response = await client.PostAsync(_insertUri, content);
            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Error(response.ReasonPhrase ?? $"Found unsuccessful request with {response.StatusCode} code");
                return false;
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to send log - {ex.Message}. Log:\n{json}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Очищает пользовательский ввод и предотвращает поломку поиска/injection атаки.
    /// Затем преобразует строку так, чтобы исключить зависимость от регистра слов + сделать поиск по словам в любом порядке.
    /// </summary>
    /// <param name="userInput">Строка введенная пользователем для очистки</param>
    /// <returns>
    /// Возвращает очищенную строку вида _msg:i("сообщение") _msg:i("сообщение два").
    /// Символы вроде слешей и кавычек экранируются и возвращаются в экранированном виде для безопасной отправки в logsQL обработчик.
    /// </returns>
    private static string SanitizeUserInput(string userInput)
    {
        // Общий план экранирования будет такой
        // 1. Сначала экранируем опасные символы, разрывающие строки - слеши и кавычки
        // 2. Затем проходимся по итоговому сообщению и оборачиваем каждое слово в _msg:i("сообщение")
        // В итоге оно будет как-то так _msg:i("сообщение") _msg:i("сообщение два")
        // Все остальные опасные символы будут обезопашены за счет нахождения в кавычках

        var result = new StringBuilder(32);

        // Обязательно сначала нужно заменить слеши, а потом кавычки.
        // Если сделать это наоборот, то добавленные нами спецсимволы
        // для экранирования кавычек(\") удвоятся и все сломается.

        // @"\\" - это два слеша
        // "\"" - это один символ кавычки
        // "\\\"" - это слеш и кавычка (\")
        userInput = userInput
            .Replace(@"\", @"\\")
            .Replace("\"", "\\\"");

        // Тут странная запись (char[]?)null заставляет автоматически подставить все пробельные
        // или символы новые строки в качестве разделителей.
        // Было бы славно просто передать null, но тогда C# не может определить какой из 3ех методов я вызываю

        foreach (var word in userInput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            // Этот страшный код оборачивает каждое слово в форму вида _msg:i("сообщение")
            // Это нужно, чтобы поиск:
            // 1. Был регистронезависимым
            // 2. Искал слова в любом порядке, а не только том, в котором напишет админ
            // 3. Экранировал все страшные символы запечатав их в кавычки
            result.Append(MessageFieldName).Append(":i(\"").Append(word).Append("\") ");
        }

        // + очищаем лишний пробел после последней строки, который всегда будет в конце строки.
        return result.ToString().TrimEnd();
    }

    #endregion

    #region Life cycle

    /// <summary>
    /// Получает или создает настроенный HttpClient для отправки запроса к VictoriaLogs
    /// </summary>
    private HttpClient GetOrCreateClient()
    {
        // Клиент не должен существовать/запрашиваться, если система выключена
        DebugTools.Assert(_enabled || _client == null);
        if (_client != null)
            return _client;

        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        return _client;
    }

    private void DisposeAndNullifyClient()
    {
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        DisposeAndNullifyClient();
    }

    #endregion

    #region Cvars

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (enabled && _client == null)
            GetOrCreateClient();
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

