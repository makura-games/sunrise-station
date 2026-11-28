using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Roles;

public sealed partial class RelativeJobsCountSystem : EntitySystem
{
    [Dependency] private StationJobsSystem _jobsSystem = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private ISawmill _sawmill = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerJoinedStation);
        SubscribeLocalEvent<StationPostInitEvent>(OnStationInit);

        _sawmill = Logger.GetSawmill("sunrise.relative_job");
    }

    private void OnPlayerJoinedStation(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<RelativeJobsCountComponent>(args.Station, out var relativeJobsComponent))
            return;

        if (args.JobId == null)
            return;

        IncreaseSlotsByOtherJobs(args.Station, args.JobId, relativeJobsComponent);
    }

    private void OnStationInit(ref StationPostInitEvent args)
    {
        if (!TryComp<RelativeJobsCountComponent>(args.Station, out var relativeJobsComponent))
            return;

        // Это должно вызываться один раз перед тем, как игроки заспавнятся за свои работки
        IncreaseSlotsByOnline((args.Station, relativeJobsComponent));
    }

    #region Slot increase logic

    /// <summary>
    /// Увеличивает количество слотов ролей за счет количества общего онлайна
    /// Параметры указаны в прототипе станции через <see cref="RelativeJobsCountComponent"/>
    /// </summary>
    private void IncreaseSlotsByOnline(Entity<RelativeJobsCountComponent> station)
    {
        var totalPlayer = _player.PlayerCount;

        // Проходимся по всем переданным настройкам для ролей
        // И добавляем им слоты
        foreach (var settings in station.Comp.Online)
        {
            if (!_jobsSystem.TryGetJobSlot(station, settings.TargetJob, out _))
                continue;

            // Максимально возможное количество дополнительных слотов.
            // Выставляется как самое низкое из максимальных значений
            var maxSlots = GetMaxSlots(settings, station.Comp);

            // Расчет количества дополнительных слотов. Минимально 0, максимальное зависит от maxSlots
            var additionalSlots = maxSlots >= 0
                ? (int) Math.Clamp(totalPlayer / settings.AnyTargetOnlineIncreaseSlot, 0f, maxSlots)
                : (int) Math.Round(totalPlayer / settings.AnyTargetOnlineIncreaseSlot);

            AddSlots(station, settings, additionalSlots);
        }
    }

    /// <summary>
    /// Увеличивает количество слотов ролей за счет количества других ролей.
    /// Параметры указаны в прототипе станции через <see cref="RelativeJobsCountComponent"/>
    /// </summary>
    private void IncreaseSlotsByOtherJobs(EntityUid station, string playerJobId, RelativeJobsCountComponent component)
    {
        // Проходимся по всем переданным настройкам для ролей
        // И добавляем им слоты
        foreach (var settings in component.Jobs)
        {
            // Проходимся по внутреннему списку ролей, от количества игроках на которых зависит количество слотов для нашей целей роли
            foreach (var (relativeJob, modifier) in settings.Dependency)
            {
                // Так как это вызывается, когда игрок зашел в раунд, то мы проверяем, является ли текущий зашедший игрок нужной нам работкой
                // Если это нужная работка мы увеличим слоты. Если не нужная, то пойдет нахуй
                if (playerJobId != relativeJob.ToString())
                    continue;

                if (!_jobsSystem.TryGetJobSlot(station, settings.TargetJob, out _))
                    continue;

                AddSlots((station, component), settings, modifier);
            }
        }
    }

    private void AddSlots(Entity<RelativeJobsCountComponent> station, IRelativeCountSettings settings, int value)
    {
        var maxSlots = GetMaxSlots(settings, station.Comp);
        var toAdd = maxSlots >= 0 ? Math.Clamp(value, 0, maxSlots) : value;
        if (toAdd == 0)
            return;

        _jobsSystem.TryAdjustJobSlot(station, settings.TargetJob, toAdd, true);
        RemoveAdditionalSlot(station, settings, toAdd);
    }

    private void RemoveAdditionalSlot(Entity<RelativeJobsCountComponent> station, IRelativeCountSettings settings, int value)
    {
        if (!station.Comp.TotalMaxCount.ContainsKey(settings.TargetJob))
        {
            _sawmill.Error($"Target job {settings.TargetJob} not presented in TotalMaxCount dictionary");
            return;
        }

        // Если TotalMaxCount равен -1 (без ограничений), не вычитаем из него
        if (station.Comp.TotalMaxCount[settings.TargetJob] == -1)
            return;

        station.Comp.TotalMaxCount[settings.TargetJob] -= value;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Смотрит, не вышло ли количество слотов данной должности за установленные максимальные пределы
    /// </summary>
    /// <param name="targetJobCount">Текущее количество открытых слотов</param>
    /// <param name="settings">Текущие базовые параметры</param>
    /// <param name="component">Компонент <see cref="RelativeJobsCountComponent"/></param>
    /// <returns>Вышло ли количество слотов данной работы за максимальные пределы</returns>
    private bool IsMaximumSlotsReached(int? targetJobCount,
        IRelativeCountSettings settings,
        RelativeJobsCountComponent component)
    {
        if (settings.MaxSlots == -1)
            return false;

        if (!component.TotalMaxCount.TryGetValue(settings.TargetJob, out var targetCount))
        {
            _sawmill.Error($"Target job {settings.TargetJob} not presented in TotalMaxCount dictionary");

            return true;
        }

        if (targetJobCount >= targetCount)
            return true;

        if (targetJobCount == null || targetJobCount >= settings.MaxSlots)
            return true;

        return false;
    }

    private static int GetMaxSlots(IRelativeCountSettings settings, RelativeJobsCountComponent component)
    {
        if (!component.TotalMaxCount.TryGetValue(settings.TargetJob, out var totalMax))
            return 0;

        if (totalMax == -1)
            return -1;

        return Math.Clamp(Math.Min(settings.MaxSlots, totalMax), 0, int.MaxValue);
    }

    #endregion
}
