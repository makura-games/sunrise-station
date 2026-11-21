using Content.Server._Sunrise.Disease.Components;
using Content.Server.Administration.Managers;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Overlays;
using Content.Shared.Administration;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Рассылает метки оверлея болезней тем, кому он положен.
/// </summary>
/// <remarks>
/// Админ, включивший оверлей командой, видит все очаги станции: заражённые предметы, облака
/// и носителей с названием болезни и стадией. Владелец очков вирусолога видит только заражённые
/// предметы и без подробностей — больных людей очки не выдают.
/// Данные о болезнях живут только на сервере, поэтому клиенту уходят готовые метки, а не компоненты.
/// </remarks>
public sealed partial class DiseaseOverlaySystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseSourceSystem _source = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;

    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;

    /// <summary>
    /// Как часто метки обновляются.
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Права, нужные для админского оверлея.
    /// </summary>
    private const AdminFlags RequiredFlags = AdminFlags.Admin;

    /// <summary>
    /// Админы, включившие оверлей.
    /// </summary>
    private readonly HashSet<ICommonSession> _adminViewers = new();

    /// <summary>
    /// Кому в прошлый раз ушли непустые метки: им нужно отдельно выключить оверлей.
    /// </summary>
    private HashSet<ICommonSession> _receivers = new();

    private HashSet<ICommonSession> _nextReceivers = new();

    private readonly List<DiseaseOverlayEntry> _adminEntries = new();

    /// <summary>
    /// Очаги станции, собранные для текущего обновления. Переиспользуется, а не помнит состояние.
    /// </summary>
    private readonly List<(MapId Map, NetEntity Entity)> _sources = new();

    private TimeSpan _nextUpdate;

    public override void Shutdown()
    {
        base.Shutdown();

        _adminViewers.Clear();
        _receivers.Clear();
        _nextReceivers.Clear();
    }

    /// <summary>
    /// Надетые очки начинают работать сразу, не дожидаясь следующего обновления.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnVisionEquipped(Entity<DiseaseVisionComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) != 0)
            _nextUpdate = _timing.CurTime;
    }

    /// <summary>
    /// Снятые очки гаснут сразу: иначе подсветка висела бы до следующего обновления.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnVisionUnequipped(Entity<DiseaseVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) != 0)
            _nextUpdate = _timing.CurTime;
    }

    /// <summary>
    /// Включает или выключает админский оверлей для сессии.
    /// </summary>
    /// <returns>True, если оверлей теперь включён.</returns>
    public bool ToggleAdminOverlay(ICommonSession session)
    {
        _nextUpdate = _timing.CurTime;

        if (_adminViewers.Remove(session))
            return false;

        if (!_admin.HasAdminFlag(session, RequiredFlags))
            return false;

        _adminViewers.Add(session);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;
        Refresh();
    }

    private void Refresh()
    {
        _nextReceivers.Clear();
        _sources.Clear();

        SendAdminEntries();
        SendVisionEntries();

        // Тем, кто перестал видеть оверлей, уходит пустой список, чтобы клиент его выключил.
        foreach (var session in _receivers)
        {
            if (!_nextReceivers.Contains(session) && session.Status != SessionStatus.Disconnected)
                RaiseNetworkEvent(new DiseaseOverlayUpdateEvent(new List<DiseaseOverlayEntry>()), session);
        }

        (_receivers, _nextReceivers) = (_nextReceivers, _receivers);
    }

    private void SendAdminEntries()
    {
        // Вышедшие и снявшие права админы выпадают из списка сами.
        _adminViewers.RemoveWhere(session =>
            session.Status == SessionStatus.Disconnected || !_admin.HasAdminFlag(session, RequiredFlags));

        if (_adminViewers.Count == 0)
            return;

        BuildAdminEntries();

        foreach (var session in _adminViewers)
        {
            RaiseNetworkEvent(new DiseaseOverlayUpdateEvent(new List<DiseaseOverlayEntry>(_adminEntries)), session);
            _nextReceivers.Add(session);
        }
    }

    private void SendVisionEntries()
    {
        // Очаги собираются на первом же владельце очков и переиспользуются остальными,
        // а без владельцев не собираются вовсе.
        var collected = false;
        var query = EntityQueryEnumerator<DiseaseVisionComponent>();
        while (query.MoveNext(out var uid, out var vision))
        {
            if (!_inventory.TryGetContainingSlot(uid, out var slot) || (slot.SlotFlags & vision.Slots) == 0)
                continue;

            var wearer = Transform(uid).ParentUid;
            if (!_actorQuery.TryComp(wearer, out var actor))
                continue;

            // У админа с включённым оверлеем и так всё видно, а двое очков не дают двух списков.
            var session = actor.PlayerSession;
            if (_nextReceivers.Contains(session))
                continue;

            if (!collected)
            {
                collected = true;
                CollectSources();
            }

            var entries = BuildVisionEntries(Transform(wearer).MapID);
            RaiseNetworkEvent(new DiseaseOverlayUpdateEvent(entries), session);
            _nextReceivers.Add(session);
        }
    }

    /// <summary>
    /// Собирает все очаги станции с болезнями.
    /// </summary>
    private void BuildAdminEntries()
    {
        _adminEntries.Clear();

        var sources = EntityQueryEnumerator<DiseaseSourceComponent>();
        while (sources.MoveNext(out var uid, out var source))
        {
            var diseases = new List<DiseaseOverlayDisease>();
            foreach (var disease in _source.GetDiseases((uid, source)))
            {
                if (TryGetOverlayDisease(disease, 0, out var entry))
                    diseases.Add(entry);
            }

            if (diseases.Count > 0)
                _adminEntries.Add(new DiseaseOverlayEntry(GetNetEntity(uid), DiseaseOverlayKind.Source, diseases));
        }

        var clouds = EntityQueryEnumerator<DiseaseCloudComponent>();
        while (clouds.MoveNext(out var uid, out var cloud))
        {
            if (cloud.Disease is not { } disease || !TryGetOverlayDisease(disease, 0, out var entry))
                continue;

            _adminEntries.Add(new DiseaseOverlayEntry(GetNetEntity(uid), DiseaseOverlayKind.Cloud, [entry]));
        }

        var carriers = EntityQueryEnumerator<DiseaseCarrierComponent>();
        while (carriers.MoveNext(out var uid, out var carrier))
        {
            var diseases = new List<DiseaseOverlayDisease>(carrier.Diseases.Count);
            foreach (var instance in _disease.GetDiseases((uid, carrier)))
            {
                if (TryGetOverlayDisease(instance.Disease, instance.Stage + 1, out var entry))
                    diseases.Add(entry);
            }

            if (diseases.Count > 0)
                _adminEntries.Add(new DiseaseOverlayEntry(GetNetEntity(uid), DiseaseOverlayKind.Carrier, diseases));
        }
    }

    /// <summary>
    /// Собирает заражённые предметы карты, где находится владелец очков, без названий болезней.
    /// </summary>
    /// <remarks>
    /// Радиуса у очков нет: что из присланного окажется видно, решает обычная видимость клиента.
    /// Метку на предмет вне PVS клиент не рисует, а стоит предмету попасть в поле зрения —
    /// ореол появляется сам. Карта всё же проверяется, чтобы шахтёру на планете не уходили
    /// метки со станции и наоборот.
    /// </remarks>
    private List<DiseaseOverlayEntry> BuildVisionEntries(MapId map)
    {
        var entries = new List<DiseaseOverlayEntry>();
        foreach (var (sourceMap, entity) in _sources)
        {
            if (sourceMap != map)
                continue;

            entries.Add(new DiseaseOverlayEntry(entity, DiseaseOverlayKind.Source, new List<DiseaseOverlayDisease>()));
        }

        return entries;
    }

    /// <summary>Собирает заразные очаги станции с их картами.</summary>
    private void CollectSources()
    {
        var query = EntityQueryEnumerator<DiseaseSourceComponent>();
        while (query.MoveNext(out var uid, out var source))
        {
            if (_source.GetDiseases((uid, source)).Count > 0)
                _sources.Add((Transform(uid).MapID, GetNetEntity(uid)));
        }
    }

    private bool TryGetOverlayDisease(EntityUid disease, int stage, out DiseaseOverlayDisease entry)
    {
        if (!_registry.TryGetData(disease, out var data))
        {
            entry = default;
            return false;
        }

        entry = new DiseaseOverlayDisease(data.Name, data.StrainCode, stage, data.Stages.Count);
        return true;
    }
}
