using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared.Administration.Logs;

namespace Content.Server._Sunrise.ExternalServices.VictoriaLogs;

public sealed partial class VictoriaLogs
{
    public const string AdminLogSourceType = "admin_log";

    private bool _storeInDatabase;

    /// <summary>
    /// Пытается отправить админ-логи в VictoriaLogs, сериализуя их для начала в DTO VictoriaAdminLogInsert.
    /// В случае провала одной из попыток пытается отправить снова, чтобы исключить короткие сетевые проблемы.
    /// </summary>
    /// <param name="log">Админ лог типа <see cref="AdminLog"/></param>
    /// <param name="maxRetries">Количество попыток переотправить лог, если предыдущая попытка завершилась неудачей.</param>
    /// <param name="delayMs">Время между попытками переотправить логи в милисекундах</param>
    public async Task<bool> TrySendAdminLog(AdminLog log, int maxRetries = 3, int delayMs = 500)
    {
        if (!_enabled)
            return false;

        if (_insertUri == null)
        {
            _sawmill.Warning($"URI for insert query is null, log {log.Message} won't be saved");
            return false;
        }

        var json = JsonSerializer.Serialize(new VictoriaAdminLogInsert(log, _serverId), _jsonOptions);

        // Цикл повторных попыток
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (await TrySend(json))
                return true;

            _sawmill.Warning($"Failed to send log (attempt {attempt}/{maxRetries}). Retrying...");

            // Ждём перед следующей попыткой (если это не последняя)
            if (attempt < maxRetries)
                await Task.Delay(delayMs);
        }

        _sawmill.Error($"Failed to send admin log after {maxRetries} attempts.");
        return false;
    }

    // TODO: Как-нибудь сообразить, как отсюда выделить общую логику получения логов от конкретной логики админ-логов.
    // Проблема в том, что я не хочу городить лишние абстракции и усложнять код ради ненужной сейчас расширяемости.
    // А придумать как не убить простоту и сделать общую логику переиспользуемой... ну хз не знаю.
    public async Task<List<SharedAdminLog>> SelectLogs(LogFilter? filter = null)
    {
        var logsQl = BuildQuery(filter);
        _sawmill.Info("ЗАПРОС:\n" + logsQl);

        var formData = new Dictionary<string, string>
        {
            ["query"] = logsQl,
            // Здесь можно указать дополнительные (ограниченно поддерживаемые) параметрые вроде limit и т.п.
            // Это что-то вроде fallback, если в запросе они почему-то могут не передаться
            // Но я учел это в построении запроса, а метод пока не общий, поэтому смысла тут прописывать нет.
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _selectUri);
        request.Content = new FormUrlEncodedContent(formData);

        var result = new List<SharedAdminLog>();
        var rawResult = "";

        using var response = await _client!.SendAsync(request);

        // Это я спиздил из логики с базой данных, не уверен, что это правильно надеяться на возмоность default
        var token = filter?.CancellationToken ?? default;

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync(token);
            _sawmill.Error($"Query failed ({(int)response.StatusCode}): {errorText}");
            return result;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);

        // Читаем ответ от VictoriaLogs построчно.
        // Это работает, потому что VictoriaLogs делает каждую строку в ответе валидным JSON,
        // который валиден сам по себе в отрыве от остальной части
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                // Десериализуем каждую строку во временный DTO VictoriaAdminLogResponse.
                // Это нужно, потому что десериализация требует, чтобы тип полей совпадали с тем, что придет в JSON.
                // VictoriaLogs отдает все в формате string, т.е. обернутое в "", даже если это строка или bool.
                // Если подставить туда сразу SharedAdminLog, то будет ошибка из-за типов полей, которые не совпадают.
                // Например, сериализатор увидит "true", но не сможет преобразовать это в bool = true,
                // потому что тип данных по JSON у "true" - string, а не bool.

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
    /// <returns>Запрос на logsQL для получения админлогов по заданным фильтрам из <see cref="LogFilter"/></returns>
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
        _stream:{{{ServerIdFieldName}}="{{_serverId}}", {{LogSourceTypeFieldName}}="{{AdminLogSourceType}}"}
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
            var round = $"{TimeFieldName}:>\"{filter.After.Value.Date:O}\"";
            query.Append(round);
            query.AppendLine();
        }

        // Фильтр ДО по времени
        if (filter?.Before != null)
        {
            // Время обязательно в формате ISO 8601 через параметр O
            var round = $"{TimeFieldName}:<\"{filter.Before.Value.Date:O}\"";
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
            query.Append(SanitizeUserInput(filter.Search));
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
        var limit = filter?.Limit ?? 50_000; // TODO: Перенести в заголовки
        query.Append($"| limit {limit}");
        // В последний раз можно не добавлять новую строку

        // Йобана в рот, отбились.
        return query.ToString();
    }

    /// <summary>
    /// Определяет, нужно ли сохранять/брать логи из базы данных.
    /// Проверяет, включена ли поддержка VictoriaLogs и как настроена опция по хранению логов в базе данных.
    /// </summary>
    /// <returns>Должны ли логи сохраняться/браться из базы данных</returns>
    public bool ShouldUseDatabase()
    {
        if (!_enabled)
            return true;

        return _storeInDatabase;
    }
}
