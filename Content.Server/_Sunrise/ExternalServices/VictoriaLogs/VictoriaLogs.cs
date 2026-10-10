using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.ExternalServices.VictoriaLogs;

// TODO: Выделение базовой работы с викторией от админ логов
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

    private bool _disposed;
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

    /// <summary>
    /// Базовый метод для отправки логов в VictoriaLogs.
    /// Принимает заранее заготовленный JSON с логами и отправляет его в VictoriaLogs.
    /// </summary>
    /// <param name="data">Массив байтов типа <see cref="ArraySegment{T}"/> содержащий JSON строки логов для отправки</param>
    /// <param name="maxRetries">Количество попыток переотправить лог, если предыдущая попытка завершилась неудачей.</param>
    /// <param name="delayMs">Время между попытками переотправить логи в милисекундах</param>
    /// <returns>Получилось или нет совершить отправку</returns>
    // TODO: Выделить общую базу и сделать вариант для простой string json вместо сложного ArraySegment
    public async Task<bool> TrySend(ArraySegment<byte> data, int maxRetries = 3, int delayMs = 500)
    {
        if (_disposed)
            return false;

        var client = GetOrCreateClient();

        DebugTools.Assert(data.Count != 0);
        DebugTools.Assert(maxRetries >= 1);
        DebugTools.Assert(delayMs >= 0);

        // Логика переотправки логов(ретраев).
        // Если по какой-то причине с первого раза не дошло - пробуем пару раз и логгируем проблемные попытки.
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            string? errorReason;
            try
            {
                // ИИ настоятельно порекомендовал мне хранить контент рядом с запросом, не вынося из его из цикла.
                // Там что-то связанное с возможными dispose между попытками + изменением самого контента внутри запроса.
                using var content = new ByteArrayContent(data.Array!, data.Offset, data.Count);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var response = await client.PostAsync(_insertUri, content);

                // Единственный успешный выход тут.
                // Если успешно отправили - выходим из метода и цикла заявляя об успехе
                // Все что ниже - обработка и логгирование ошибок, в нормальных случаях код ниже не исполняется.
                if (response.IsSuccessStatusCode)
                    return true;

                // Все ошибки за исключением серверных ошибок(начинающихся с 500) нет смысла повторять.
                // Единственное исключение - 429(too many request), его имеет смысл повторить.
                // Сразу выходим из цикла и возвращаем false
                var code = (int)response.StatusCode;
                if (code is >= 400 and < 500 && code != 429)
                {
                    _sawmill.Error($"Failed to send logs, HTTP {code} ({response.ReasonPhrase}). Skipped retrying because it's useless here");
                    return false;
                }

                errorReason = $"HTTP {code} ({response.ReasonPhrase})";
            }
            catch (HttpRequestException ex)
            {
                // Это выбрасывает сам HttpClient при внутренних редких ошибках.
                // Обычно он отдает ошибку в статус код, но редко может и выбросить эту ошибку.
                // Например, при проблемах с сертификатами или DNS.
                errorReason = $"Network error: {ex.Message}";
            }
            catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // По умолчанию эта ошибка кидается в 2 случаях - отмена при реальной отмене и таймауте.
                // Почему так? Потому что исторически сложилось.
                // Поэтому тут есть проверка, что выпал именно нужный нам подтип.
                errorReason = "Request timed out";
            }
            catch (Exception)
            {
                // Здесь буду все остальные ошибки, которые хуй знает что вообще означают.
                // Я думаю, что при них нет смысла повторять. Я и так усложнил код и выбрал все известные мне случаи,
                // когда есть смысл ретраить.
                return false;
            }

            // Ждём перед следующей попыткой (если это не последняя)
            if (attempt < maxRetries)
            {
                _sawmill.Warning($"{errorReason}. Failed to send logs (attempt {attempt}/{maxRetries}). Retrying...");
                await Task.Delay(delayMs);
            }
            else
            {
                // Если не делаем ретрай, то не стоит об этом сообщать.
                _sawmill.Warning($"{errorReason}. Failed to send logs (attempt {attempt}/{maxRetries}).");
            }
        }

        _sawmill.Error($"Failed to send logs after {maxRetries} attempts.");
        return false;
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(VictoriaLogs));

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
        _disposed = true;
        _enabled = false;
    }

    #endregion

    #region Cvars

    private void OnEnabledChanged(bool enabled)
    {
        if (_disposed)
            return;

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

