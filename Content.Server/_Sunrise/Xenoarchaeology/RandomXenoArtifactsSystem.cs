using System.Linq;
using Content.Server._Sunrise.Helpers;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Helpers;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Doors.Electronics;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Xenoarchaeology;

public sealed partial class RandomXenoArtifactsSystem : EntitySystem
{
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SunriseHelpersSystem _helpers = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    /// <summary>
    /// Соотношение предметов к предметам-артефактам. В % процентах.
    /// Стандартное соотношение: <see cref="SunriseCCVars.ItemToArtifactRatio"/>
    /// </summary>
    private float _itemToArtifactRatio;
    private bool _enabled = SunriseCCVars.EnableRandomArtifacts.DefaultValue;

    private static readonly EntProtoId BaseParent = "BaseRandomItemXenoArtifactComponents";
    private static EntityPrototype? _baseParentPrototype;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    [Dependency] private EntityQuery<DoorElectronicsComponent> _doorElectronicsQuery = default!;
    [Dependency] private EntityQuery<ApcElectronicsComponent> _apcElectronicsQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;

    [Dependency] private EntityQuery<StationRandomXenoArtifactComponent> _availableStationQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        _configuration.OnValueChanged(SunriseCCVars.EnableRandomArtifacts, OnCvarChanged, true);
        _configuration.OnValueChanged(SunriseCCVars.ItemToArtifactRatio, r => _itemToArtifactRatio = r, true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_enabled)
            return;

        DeleteOldArtifacts();
        CreateRandomArtifacts();
    }

    #region Creation

    /// <summary>
    /// Превращает часть предметов в артефакты
    /// Количество зависит от выставленного значения в <see cref="SunriseCCVars.ItemToArtifactRatio"/>
    /// </summary>
    public void CreateRandomArtifacts()
    {
        if (!_prototype.TryIndex(BaseParent, out _baseParentPrototype))
        {
            Log.Error("Error while creating BaseParent for RandomXenoArtifactsSystem");

            return;
        }

        var items = _helpers.GetAll<ItemComponent>()
            .Where(e => IsAppropriate(e.Owner))
            .ToList()
            .ShuffleRobust(_random)
            .TakePercentage(_itemToArtifactRatio);

        var count = 0;
        foreach (var item in items)
        {
            MakeArtifact(item);
            count++;
        }

        Log.Debug($"{count} items made into artifacts");
    }

    /// <summary>
    /// Проверяет, подходит ли выбранный предмет для превращения в артефакт-предмет
    /// </summary>
    private bool IsAppropriate(Entity<TransformComponent?> ent)
    {
        if (!_xformQuery.Resolve(ref ent))
            return false;

        var station = _station.GetOwningStation(ent, ent.Comp);

        if (!_availableStationQuery.HasComp(station))
            return false;

        // Блеклист компонентов, которые не должны становиться артефактами.
        // Все это какие-то предметы, внутри других предметов, которые достаются через жопу.
        // Поэтому делать их артефактами ну такое себе

        if (_doorElectronicsQuery.HasComp(ent))
            return false;

        if (_apcElectronicsQuery.HasComp(ent))
            return false;

        if (_organQuery.HasComp(ent))
            return false;

        return true;
    }

    private void MakeArtifact(Entity<ItemComponent> ent)
    {
        EntityManager.AddComponents(ent, _baseParentPrototype!.Components, false);
    }

    /// <summary>
    /// Удаляет старые артефакты.
    /// Теперь только предметы
    /// </summary>
    private void DeleteOldArtifacts()
    {
        var query = AllEntityQuery<XenoArtifactComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            QueueDel(uid);
        }
    }

    #endregion

    /// <summary>
    /// Обрабатывает изменение текущих настроек сервера.
    /// Поддерживает переключение в моменте игры
    /// </summary>
    private void OnCvarChanged(bool enabled)
    {
        if (_enabled == enabled)
            return;

        if (!enabled)
        {
            var items = _helpers.GetAll<XenoArtifactRandomItemMarkerComponent>();

            foreach (var item in items)
            {
                RemComp<XenoArtifactComponent>(item);
            }
        }
        else
        {
            CreateRandomArtifacts();
        }

        _enabled = enabled;
    }
}
