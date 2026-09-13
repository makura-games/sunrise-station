using System.Text.Json;
using Content.Server.Roles.Jobs;
using Content.Shared._Sunrise;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Prometheus;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.PrometheusMetrics;

/// <summary>
/// Система сбора и управления метриками при спавне персонажа.
/// Собирает статистику по все данным о спавне игрока и персонаже игрока, включая важные для статистики данные о теле персонажа:
/// тип тела, голос, пол раса.
/// </summary>
/// <remarks>
/// Другие системы также могут добавлять в эту статистику данные,
/// если собственноручно спавнят персонажей в обход штатной системы и ее ивента <see cref="PlayerSpawnCompleteEvent"/>.
/// <para>
/// Для этого имеются публичные <see cref="PlayerSpawn"/>, <see cref="PlayerCharacterStatsOnSpawn"/>,
/// <see cref="PlayerTraits"/>, <see cref="PlayerMarkings"/>
/// </para>
/// </remarks>
public sealed partial class PlayerSpawnMetricsSystem : EntitySystem
{
    [Dependency] private JobSystem _job = default!;

    private const string NotFound = "not_found";
    private const string RoundStart = "roundstart";
    private const string LateJoin = "latejoin";

    private const string SlimType = "slim";
    private const string GigaType = "giga";
    private const string FatType = "fat";
    private const string NormalType = "normal";

    /// <summary>
    /// Главная метрика спавна игроков.
    /// Собирает информацию о параметрах относящихся к спавну.
    /// </summary>
    [PublicAPI]
    [Access] // Вписать сюда системы, которые будут добавлять данные
    public static readonly Counter PlayerSpawn = Metrics
        .CreateCounter(
            "player_spawn_count",
            "Суммарное накопительное число спавнов игроков. Содержит всю информацию о станции, игроке и методе спавна. Содержит много полезной информации о самом игроке, вроде расы, работы. Добавляется только при спавне игрока стандартным способом",
            new CounterConfiguration()
            {
                LabelNames =
                [
                    "job_id", // Прототип работы персонажа, просто для справочной информации. В идеале добавить доп статистику в конце раунда - процентное соотношение количества работников к макс. слотам.
                    "department_id", // Департамент работы персонажа
                    "station_proto", // Мы должны выделять каждой станции свой ProtoId, чтобы в рамках одного раунда знать, какое распределение между станциями(а вдруг их будет несколько в раунде?)
                    "spawn_type", // Roundstart, latejoin и другие. В ивенте приходят как бул, но я сделаю это именно строками через метод GetSpawnType
                    "spawn_priority", // Это enum SpawnPriorityPreference, где есть None - ничего, Arrivals - прибытие(коридор прибытия) и Cryosleep = криосон
                ],
            });

    /// <summary>
    /// Главная статистика о персонажах игроков.
    /// Собирает информацию о персонаже при заходе в раунд.
    /// </summary>
    [PublicAPI]
    [Access] // Вписать сюда системы, которые будут добавлять данные
    public static readonly Counter PlayerCharacterStatsOnSpawn = Metrics
        .CreateCounter(
            "player_character_stats_on_spawn",
            "Суммарное накопительное число разных параметров персонажа. Содержит всю полезную информацию о параметрах тела персонажа, которым управляет игрок. Добавляется только при спавне игрока стандартным способом",
            new CounterConfiguration()
            {
                LabelNames =
                [
                    "entity_proto", // Прототип ентити игрока, например MobHuman. Может отличаться от расы
                    "species_proto", // Прототип расы игрока(SpeciesPrototype), например Human, Milira
                    "character_body_type", // Тип тела персонажа. Т.к. среди прототипов какой-то хаос я буду передать просто строку slim, normal, giga обозначающую визуальную разницу. В идеале конечно было бы прототип
                    "character_gender", // Гендер персонажа, отличается от Sex наличием двуполого и бесполого гендера. Задается через enum
                    "character_voice_proto", // Прототип голоса персонажа
                ],
            });

    /// <summary>
    /// Главная статистика о разнообразии трейтов игроков.
    /// Собирает информацию о том, какие трейты взял игрок.
    /// </summary>
    [PublicAPI]
    [Access] // Вписать сюда системы, которые будут добавлять данные
    public static readonly Counter PlayerTraits = Metrics
        .CreateCounter(
            "player_traits_count",
            "Суммарное накопительное число использованных трейтов игроками. Содержит прототип трейта, добавляется только при спавне игрока стандартным способом",
            new CounterConfiguration()
            {
                LabelNames =
                [
                    "proto_id", // Прототип трейта, который использует персонаж
                ],
            });

    /// <summary>
    /// Главная статистика о разнообразии макрингов игроков.
    /// Собирает информацию о том, какие маркинги взял игрок.
    /// </summary>
    [PublicAPI]
    [Access] // Вписать сюда системы, которые будут добавлять данные
    public static readonly Counter PlayerMarkings = Metrics
        .CreateCounter(
            "player_markings_count",
            "Суммарное накопительное число использованных маркингов игроками. Содержит прототип маркинга, добавляется только при спавне игрока стандартным способом. Включает в себя в том числе СТАНДАРТНЫЕ или НЕВЫКЛЮЧАЕМЫЕ маркинги",
            new CounterConfiguration()
            {
                LabelNames =
                [
                    "proto_id", // Прототип маркинга, который использует персонаж
                ],
            });

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        GatherSpawnStats(ev);
        GatherCharacterStats(ev);
        GatherPlayerTraits(ev);
        GatherPlayerMarkings(ev);
    }

    private void GatherSpawnStats(PlayerSpawnCompleteEvent ev)
    {
        var jobId = ev.JobId ?? NotFound;
        var departmentId = GetDepartmentId(ev.JobId);
        var stationProto = Prototype(ev.Station)?.ID ?? NotFound;
        var spawnType = GetSpawnType(ev);
        var spawnPriority = EnumToString(ev.Profile.SpawnPriority);

        PlayerSpawn.WithLabels(jobId, departmentId, stationProto, spawnType, spawnPriority).Inc();
    }

    private void GatherCharacterStats(PlayerSpawnCompleteEvent ev)
    {
        var entityProto = Prototype(ev.Mob)?.ID ?? NotFound;
        var speciesProto = ev.Profile.Species.Id;
        var characterBodyType = GetBodyType(ev.Profile.BodyType);
        var characterGender = EnumToString(ev.Profile.Gender);
        var voiceProto = ev.Profile.Voice.Id;

        PlayerCharacterStatsOnSpawn
            .WithLabels(entityProto, speciesProto, characterBodyType, characterGender, voiceProto)
            .Inc();
    }

    private void GatherPlayerTraits(PlayerSpawnCompleteEvent ev)
    {
        foreach (var traitProto in ev.Profile.TraitPreferences)
        {
            PlayerTraits.WithLabels(traitProto).Inc();
        }
    }

    private void GatherPlayerMarkings(PlayerSpawnCompleteEvent ev)
    {
        // Ебанный рот, насколько далеко запрятан ID
        // TODO: Придумать что-то, чтобы убрать из подборки дефолтные маркинги или сделать процентное соотношение дефолт к кастомным там, где есть дефолт.
        // Пока не придумал, как узнать является ли маркинг на персонаже стандартным или нет.
        foreach (var (_, layersDict) in ev.Profile.Appearance.Markings)
        {
            foreach (var (_, markingsList) in layersDict)
            {
                foreach (var markingProto in markingsList)
                {
                    PlayerMarkings.WithLabels(markingProto.MarkingId.Id).Inc();
                }
            }
        }
    }

    /// <summary>
    /// Получает типа спавна персонажа исходя из булевой переменный в ивенте.
    /// </summary>
    /// <remarks>
    /// Перевод используется, т.к. prometheus не должен управлять логикой "что есть лейтджойн, а что раундстарт". Он должен лишь хранить данные, а логика здесь, в сишарпе.
    /// </remarks>
    private string GetSpawnType(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin)
            return LateJoin;
        else
            return RoundStart;
    }

    /// <summary>
    /// Получает ID департамента по ID работы персонажа и возвращает в виде <see langword="string"/>
    /// </summary>
    private string GetDepartmentId(string? job)
    {
        if (string.IsNullOrEmpty(job))
            return NotFound;

        if (!_job.TryGetDepartment(job, out var department))
            return NotFound;

        return department.ID;
    }

    /// <summary>
    /// Расшифровывает тип тела из множества прототипов типа HumanGigaMale, HumanFatFemale в полезный для prometheus вид содержащий лишь полезную информацию: normal, slim, fat, giga описанные в константах.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Если будет какая-то раса включая в себя технические названия вроде normal, slim, fat, giga - все сломается и система будет ложно маркировать их неправильными типами тел т.к. код использует поиск по строке.
    /// </para>
    /// <para>
    /// Чтобы это исправить нужно, чтобы прототип тел были универсальными и ID не включали название расы и пола, это должно описываться только в данных самого прототипа.
    /// </para>
    /// </remarks>
    private string GetBodyType(ProtoId<BodyTypePrototype> body)
    {
        var type = body.Id.ToLowerInvariant();

        if (type.Contains(NormalType))
            return NormalType;

        if (type.Contains(SlimType))
            return SlimType;

        if (type.Contains(GigaType))
            return GigaType;

        if (type.Contains(FatType))
            return FatType;

        return NotFound;
    }

    /// <summary>
    /// Превращает Enum в строку в snake lowercase для удобной работы в prometheus.
    /// Возвращает <see cref="NotFound"/>, если не получилось получить имя Enum.
    /// </summary>
    /// <remarks>
    /// Параметр TEnum списан с метод Enum.GetName, который используется тут.
    /// Если что-то поменяется там - нужно поменять тут для совместимости значений
    /// </remarks>
    private string EnumToString<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = Enum.GetName(value);
        if (name == null)
            return NotFound;

        // я знаю, что нейминг смущает, но это превращает из ThatCaseNaming в that_case_naming для стандартизации имен в prometheus
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
    }
}
