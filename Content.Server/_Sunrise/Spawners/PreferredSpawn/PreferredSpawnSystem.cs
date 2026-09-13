using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Spawners.PreferredSpawn;

/// <summary>
/// Обрабатывает спавн для ролей, имеющих особое поведение спавна.
/// Эта система запускается перед стандартной SpawnPointSystem, чтобы переопределить
/// логику спавна по умолчанию для этих ролей.
/// </summary>
public sealed partial class PreferredSpawnSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        // Подписываемся на событие перед SpawnPointSystem, чтобы наша логика обработки
        // ролей срабатывала первой и могла переопределить стандартное поведение.
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(SpawnPointSystem) });
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        // Если другая система уже обработала спавн, пропускаем.
        if (args.SpawnResult != null)
            return;

        // Обрабатываем спавн, находя подходящие позиции.
        var spawnPositions = FindPreferredSpawnPositions(args);

        // Если подходящих позиций не найдено, позволяем другим системам обработать спавн.
        if (spawnPositions.Count == 0)
            return;

        // Выбираем случайную позицию из найденных и спавним игрока.
        var spawnLocation = _random.Pick(spawnPositions);
        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLocation,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }


    private List<EntityCoordinates> FindPreferredSpawnPositions(PlayerSpawningEvent args)
    {
        // Если DesiredSpawnPointType равно Unset, это означает, что стандартная SpawnPointSystem
        // попытается найти спавнер самостоятельно. В этом случае мы не должны мешать ей,
        // поэтому не будем пытаться найти спавнеры.
        if (args.DesiredSpawnPointType == SpawnPointType.Unset)
            return new();

        // Сначала пытаемся найти спавнеры для желаемого типа спавна.
        var positions = GetValidPreferredSpawners(args, args.DesiredSpawnPointType);

        // Запасной вариант: если для позднего присоединения (DesiredSpawnPointType == LateJoin)
        // не найдено спавнеров, предназначенных именно для позднего присоединения,
        // то в качестве отката пытаемся найти обычные спавнеры типа "Job" (предназначенные для старта раунда)
        // для этой же роли. Это гарантирует, что игроки всегда смогут заспавниться,
        // даже если специализированные LateJoin-спавнеры отсутствуют.
        if (positions.Count == 0 && args.DesiredSpawnPointType == SpawnPointType.LateJoin)
        {
            positions = GetValidPreferredSpawners(args, SpawnPointType.Job);
        }

        return positions;
    }

    /// <summary>
    /// Получает список действительных точек спавна для заданного типа спавна.
    /// </summary>
    private List<EntityCoordinates> GetValidPreferredSpawners(PlayerSpawningEvent args, SpawnPointType targetSpawnPointType)
    {
        var validPositions = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<PreferredSpawnComponent, SpawnPointComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var preferredComp, out var spawnPoint, out var xform))
        {
            // Пропускаем спавнеры, находящиеся не на той станции.
            if (args.Station != null && _station.GetOwningStation(uid, xform) != args.Station)
                continue;

            // Проверяем, подходит ли этот спавнер для текущей работы и желаемого типа спавна.
            if (IsValidPreferredSpawnerInternal(spawnPoint, preferredComp, args.Job, targetSpawnPointType))
            {
                validPositions.Add(xform.Coordinates);
            }
        }

        return validPositions;
    }

    /// <summary>
    /// Внутренний вспомогательный метод для проверки действительности preferred спавнера.
    ///
    /// Логика двойной проверки типов спавна:
    /// 1. SpawnPoint.SpawnType - базовая категория спавнера (Job/LateJoin/Whatever)
    /// 2. PreferredSpawnTypes - opt-in фильтр для PreferredSpawnSystem
    ///
    /// Спавнер участвует в preferred спавне ТОЛЬКО ЕСЛИ:
    /// - Его базовый SpawnType соответствует искомому типу
    /// - И этот тип явно указан в PreferredSpawnTypes
    /// </summary>
    private bool IsValidPreferredSpawnerInternal(SpawnPointComponent spawnPoint, PreferredSpawnComponent preferredComp, ProtoId<JobPrototype>? job, SpawnPointType targetSpawnPointType)
    {
        // Проверяем базовую категорию спавнера
        // Например: для Job спавна ищем спавнеры с SpawnType = Job
        if (spawnPoint.SpawnType != targetSpawnPointType)
            return false;

        // Дополнительная фильтрация по конкретной роли (опционально)
        if (spawnPoint.Job != null && job != null && spawnPoint.Job != job)
            return false;

        // Opt-in проверка: спавнер должен явно поддерживать этот тип через PreferredSpawnTypes
        // Это предотвращает случайное использование обычных спавнеров в preferred логике
        return preferredComp.PreferredSpawnTypes.Contains(targetSpawnPointType);
    }
}
