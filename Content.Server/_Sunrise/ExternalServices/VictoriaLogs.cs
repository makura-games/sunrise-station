using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration.Logs;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.ExternalServices;

// TODO: Разделение на партиалы
// TODO: Выделение базовой работы с викторией от админ логов
// TODO: Оптимизации?
// TODO: Посмотреть что можно сделать с серверными логами и подумать можно ли их сразу сюда добавить
// TODO: Причесать константы и зарезервированные имена
// TODO: Раскинуть документацию, описать DTO, добавить пример стандартного лога после фильтрации
// TODO: Не забыть вернуть кеширование
// TODO: Посмотреть что там с пагинацией
// TODO: Проверить как там фаталы при закрытии сервера из-за отписок
// TODO: Уменьшить лимиты на странице и посмотреть что будет
// TODO: Тесты попробовать сделать
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
    private bool _storeInDatabase;
    private string _serverId = string.Empty;

    private const string InsertQuery = $"/insert/jsonline?_stream_fields={DefaultStreamValue}";
    private const string SelectQuery = $"/select/logsql/query";

    private Uri? _insertUri;
    private Uri? _selectUri;

    public const string ServerId = "server_id";
    public const string LogSourceType = "log_source_type";
    private const string DefaultStreamValue = $"{ServerId},{LogSourceType}";

    public const string AdminLogSourceType = "admin_log";

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("VictoriaLogs");

        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged, true);
        _cfg.OnValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged, true);
    }

    public async Task HandleDefaultAdminLog(AdminLog log)
    {
        if (!_enabled)
            return;

        var json = JsonSerializer.Serialize(new VictoriaAdminLogInsert(log, _serverId), _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _sawmill.Info(json);

        if (_insertUri == null)
        {
            _sawmill.Warning($"URI for insert query is null, log {log.Message} won't be saved");
            return;
        }

        var response = await _client!.PostAsync(_insertUri, content);

        if (!response.IsSuccessStatusCode)
            _sawmill.Error(response.ReasonPhrase ?? $"Found unsuccessful request with {response.StatusCode} code");
    }

    public async Task<List<SharedAdminLog>> SelectLogs(LogFilter? filter = null)
    {
        var logsQl = BuildQuery(filter);
        _sawmill.Info("ЗАПРОС:\n" + logsQl);

        var formData = new Dictionary<string, string>
        {
            ["query"] = logsQl,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _selectUri);
        request.Content = new FormUrlEncodedContent(formData);

        var result = new List<SharedAdminLog>();
        var rawResult = "";

        using var response = await _client!.SendAsync(request);

        var token = filter?.CancellationToken ?? default;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                rawResult += line + "\n";
                var log = JsonSerializer.Deserialize<VictoriaAdminLogResponse>(line, _jsonOptions);
                var sharedLog = new SharedAdminLog
                {
                    Date = log.Time,
                    Id = log.Id,
                    Impact = log.Impact,
                    Message = log.Message,
                    Players = log.Players,
                    Type = log.Type,
                };

                result.Add(sharedLog);
            }
            catch (Exception e)
            {
                _sawmill.Error(e.Message);
            }
        }

        _sawmill.Info("ОТВЕТ:\n" + rawResult);
        return result;
    }

    /// <summary>
    /// Создает LogsQL запрос к VictoriaLogs с применением фильтра составленного админом в UI.
    /// </summary>
    /// <param name="filter">Фильтр по которому стоит отбирать данные</param>
    /// <returns></returns>
    private string BuildQuery(LogFilter? filter = null)
    {
        var query = new StringBuilder();

        // Этапы создания запроса будут
        // 1. Stream
        // 2. Зарезервированные поля VictoriaLogs, за исключением _msg, его в самом конце.
        // 3. Фильтры по данным из UI игры
        // 4. Лимиты
        // 5. Сортировка
        // 6. Пагинация(разбиение по страницам)

        // Обязательный параметр - stream, который определяет откуда брать логи.
        // Ради оптимизации хранения логи в VictoriaLogs разделены по логике. Т.е. админлоги отдельно, серверные отдельно.
        // Stream как раз определяет эти "раздельные камеры хранения" - проще говоря коробки
        // Набор уникальных переменных переданных в stream = новая уникальная коробка.
        // Тут мы указываем из какой коробки брать логи - айди сервера(ласт, рыба, фаер) + тип логов(придуманная мной
        // переменная для разделения разных по логике логов в разные стримы, например админ логи и серверные логи)
        var streams = $$"""
        _stream:{{{ServerId}}="{{_serverId}}", {{LogSourceType}}="{{AdminLogSourceType}}"}
        """;
        query.Append(streams);
        query.AppendLine();

        // Дальше фильтры по времени, которые считаются специальными полями VictoriaLogs,
        // поэтому по логике я решил, что стоит их указать после стримов,
        // которые тоже начинаются с _ и считаются спец. полями

        // Фильтр ПОСЛЕ по времени
        // Его стоит указать раньше, чем ДО, т.к. логов ПОСЛЕ какого-то времени чаще всего меньше, чем ДО(оптимизация)
        if (filter?.After != null)
        {
            // Время обязательно в формате ISO 8601 через параметр O
            // ВАЖНО: Т.к. в формате ISO 8601 используется двоеточие, нам необходимо запихнуть время в кавычки "",
            // потому что в VictoriaLogs двоеточние считается спец.символом для указания переменных.
            // Если что как выглядит время -> 2026-10-05T00:00:00.0000000Z
            var round = $"_time:>\"{filter.After.Value.Date:O}\"";
            query.Append(round);
            query.AppendLine();
        }

        // Фильтр ДО по времени
        if (filter?.Before != null)
        {
            // Время обязательно в формате ISO 8601 через параметр O
            var round = $"_time:<\"{filter.Before.Value.Date:O}\"";
            query.Append(round);
            query.AppendLine();
        }

        // Дальше будет разбор каждого фильтра и превращения переменной класса в строку запроса
        // ВАЖНО: Запрос читается слева направо(не как у евреев),
        // поэтому для оптимизации важно срезать как можно больше логов первее, чтобы не усложнять поиск

        // Фильтр по раунду. Тут все понятно
        if (filter?.Round is > 0)
        {
            var round = $"round_id:{filter.Round}";
            query.Append(round);
            query.AppendLine();
        }

        // Наконец-то дошли до самого запроса пользователя.
        // Все очень просто - просто строка всегда ищется в поле _msg, если поля не указаны прямо.
        // Я поместил сообщение сюда, хотя ниже еще будут фильтры, в целях оптимизации.
        // Поиск фильтра ниже явно нагрузит сервер сильнее, чем сделает пользы убрав лишние логи.
        if (!string.IsNullOrEmpty(filter?.Search))
        {
            query.Append(filter.Search);
            query.AppendLine();
        }

        // Тип действий игрока, который не очень интуитивно назван типом лога.
        // Условно - ударил, изменился урон, повзаимодействовал и т.п = все тут
        if (filter?.Types is { Count: > 0 })
        {
            var formattedTypes = string.Join(", ", filter.Types.Select(t => $"\"{t}\""));
            var types = $"type:in({formattedTypes})";
            query.Append(types);
            query.AppendLine();
        }

        // Важность сделанного действия.
        // Чем выше - тем серьёзнее залогированный проступок.
        // Например: Low, Medium, Extreme
        if (filter?.Impacts is { Count: > 0 })
        {
            var formattedImpacts = string.Join(", ", filter.Impacts.Select(t => $"\"{t}\""));
            var impacts = $"impact:in({formattedImpacts})";
            query.Append(impacts);
            query.AppendLine();
        }

        // Здесь на больную голову виздена свалился камень,
        // потому что эта логика воссоздана из бессмысленного нагромождения логики в оригинале.

        // Эта ужасная непонятная конструкция отвечает за фильтр по игрокам, который имеется в админ-меню.
        // Поясним за термины:
        // 1. IncludePlayers = включать в выборку действия привязанные к игрокам.
        // 2. AnyPlayers = включать в выборку действия от любого из перечисленных игроков.
        // 3. AllPlayers = включать в выборку действия от всех перечисленных игроков сразу. Только их всех вместе!
        // 4. IncludeNonPlayers = включать действия не связанные с игроками, когда есть фильтр по игрокам(all/any) <- бессмысленное говно
        const string withoutAnyPlayer = "players:=\"[]\"";
        if (filter?.IncludePlayers ?? false)
        {
            // И так реализация этой хуйни выглядит очень нечитаемо и страшно.
            // И я не знаю, как ее улучшить кроме как написать длинный комментарий с пояснениями

            // Идея в том, чтобы фильтр игроков был в одной своей большой скобке (игрок1 ИЛИ игрок2)
            // + рядом добавилось условие "ИЛИ БЕЗ ИГРОКОВ"
            // Примерно полный вариант будет выглядеть как-то так: ((игрок1 ИЛИ игрок2 ИЛИ игрок3) ИЛИ БЕЗ ИГРОКОВ)
            // Если одновременно будут включены И + ИЛИ фильтры игроков(О БОЖЕ), это будет выглядеть как-то так
            // ((игрок1 И игрок2 И игрок3) ИЛИ БЕЗ ИГРОКОВ) И ((игрок1 ИЛИ игрок2 ИЛИ игрок3) ИЛИ БЕЗ ИГРОКОВ)
            // Можно ли это как-то упростить - я хз, но выглядит ужасно только из-за "ИЛИ БЕЗ ИГРОКОВ" ака IncludeNonPlayers
            var includeNonPlayersValue = filter.IncludeNonPlayers ? $" OR {withoutAnyPlayer}" : "";

            // Фильтр по игрокам ИЛИ: Должны быть любые игроки из списка
            if (filter.AnyPlayers is { Length: > 0})
            {
                var formattedAnyPlayers = string.Join(", ",
                    filter.AnyPlayers.Select(guid => $"\"{guid}\""));
                var anyPlayers = $"(players:json_array_contains_any({formattedAnyPlayers}){includeNonPlayersValue})";
                // Итоговая запись будет выглядеть как-то так: (players:in("guid1", "guid2") OR players:="[]")

                query.Append(anyPlayers);
                query.AppendLine();
            }

            // Фильтр по игрокам И: Должны быть все перечисленные в списке игроки
            if (filter.AllPlayers is { Length: > 0})
            {
                // К сожалению функции на подобии in для "all" формата нет, поэтому используем базовый метод через AND
                var allPlayersFormatted = string.Join(" AND ",
                    filter.AllPlayers.Select(guid => $"players:=\"{guid}\""));
                var allPlayers = $"(({allPlayersFormatted}){includeNonPlayersValue})";
                // Итоговая запись будет выглядеть как-то так: ((players:="guid1" AND players:="guid2") OR players:="[]")

                query.Append(allPlayers);
                query.AppendLine();
            }
        }
        else
        {
            // Если не включать игроков(IncludePlayers = false) - то должны быть только те, где игроки не участвовали
            // т.е. запись players:="[]"
            query.Append(withoutAnyPlayer);
            query.AppendLine();
        }

        // Сортировка по времени создания лога
        // ВАЖНО: Сортировка должна быть ДО лимитирования!
        // Так как чтение пайпов(|) идет слева направо,
        // то VictoriaLogs сначала отрежет первые попавшиеся логи, а потом отсортирует.
        // В итоге логи будут каждый раз рандомные, потому что VictoriaLogs всегда подает первые попавшиеся логи
        var order = filter?.DateOrder == DateOrder.Ascending ? "asc" : "desc";
        query.Append($"| sort by (_time {order})");
        query.AppendLine();

        // Лимитирование.
        // Нужно всегда задавать хардлимит, чтобы сервер случайно не загрузил 9999 гигабайт данных в память.
        var limit = filter?.Limit ?? 50_000;
        query.Append($"| limit {limit}");
        // В последний раз можно не добавлять новую строку

        // Йобана в рот, отбились.
        return query.ToString();
    }

    public bool ShouldUseDatabase()
    {
        if (!_enabled)
            return true;

        return _storeInDatabase;
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
}

