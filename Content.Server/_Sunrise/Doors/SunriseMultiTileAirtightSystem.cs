using System.Numerics;
using Content.Server._Sunrise.Doors.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Doors.Systems;

/// <summary>
///     Нужна чтобы мультитайловые двойные или тройные шлюзы нормально не пропускали газы
///     Спавнит блокеры на соседних тайлах и регулирует когда блокеры не пропускают газ, когда пропускают. В зависимости от состояния шлюза
/// </summary>
public sealed class SunriseMultiTileAirtightSystem : EntitySystem
{
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly EntProtoId BlockerPrototype = "SunriseMultiTileAirtightBlocker";

    private EntityQuery<AirtightComponent> _airtightQuery;
    private EntityQuery<SunriseMultiTileAirtightBlockerComponent> _blockerQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<SunriseMultiTileAirtightComponent> _multiTileQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<EntityUid> _anchoredEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        _airtightQuery = GetEntityQuery<AirtightComponent>();
        _blockerQuery = GetEntityQuery<SunriseMultiTileAirtightBlockerComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _multiTileQuery = GetEntityQuery<SunriseMultiTileAirtightComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, ReAnchorEvent>(OnReAnchor);
        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, MoveEvent>(OnMoved);
    }

    private void OnMapInit(Entity<SunriseMultiTileAirtightComponent> ent, ref MapInitEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnUnpaused(Entity<SunriseMultiTileAirtightComponent> ent, ref EntityUnpausedEvent args) =>
        Timer.Spawn(0, () => RefreshAfterUnpause(ent.Owner));

    private void RefreshAfterUnpause(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (!_multiTileQuery.TryGetComponent(uid, out var component))
            return;

        var ent = (uid, component);
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnShutdown(Entity<SunriseMultiTileAirtightComponent> ent, ref ComponentShutdown args)
    {
        DeleteBlockers(ent);
    }

    private void OnDoorStateChanged(Entity<SunriseMultiTileAirtightComponent> ent, ref DoorStateChangedEvent args)
    {
        RefreshAirblock(ent);
    }

    private void OnAirtightChanged(Entity<SunriseMultiTileAirtightComponent> ent, ref AirtightChanged args)
    {
        if (!args.AirBlockedChanged)
            return;

        RefreshAirblock(ent);
    }

    private void OnAnchorChanged(Entity<SunriseMultiTileAirtightComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnReAnchor(Entity<SunriseMultiTileAirtightComponent> ent, ref ReAnchorEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnMoved(Entity<SunriseMultiTileAirtightComponent> ent, ref MoveEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    /// <summary>
    ///     Пересоздает блокеры на дополнительных тайлах
    ///     ExtraTiles задаются в локальных координатах двери, поэтому оффсет надо повернуть по направлению двери
    ///     После поворота округяем сразу до целых тайлов, потому что поворот идет через float
    /// </summary>
    private void RefreshGeometry(Entity<SunriseMultiTileAirtightComponent> ent)
    {
        if (Paused(ent))
            return;

        DeleteBlockers(ent);

        if (!_xformQuery.TryGetComponent(ent.Owner, out var xform))
            return;

        if (!xform.Anchored || xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return;

        var baseTile = _transform.GetGridTilePositionOrDefault((ent, xform), grid);
        var rotation = xform.LocalRotation.RoundToCardinalAngle();

        foreach (var local in ent.Comp.ExtraTiles)
        {
            var rotated = rotation.RotateVec(new Vector2(local.X, local.Y));
            var offset = new Vector2i((int)MathF.Round(rotated.X), (int)MathF.Round(rotated.Y));
            var tile = baseTile + offset;

            DeleteStaleBlockersOnTile((gridUid, grid), tile, ent.Owner);

            var coords = GetTileCenter(gridUid, grid, tile);
            var blocker = Spawn(BlockerPrototype, coords);
            var blockerXform = _xformQuery.GetComponent(blocker);

            // Обязательно анкорим на грид и конкретный тайл, ибо airtight будет не на том месте будет и будет адское шоу
            if (!_transform.AnchorEntity((blocker, blockerXform), (gridUid, grid), tile))
            {
                Del(blocker);
                continue;
            }

            ent.Comp.Blockers.Add(blocker);
        }
    }

    /// <summary>
    ///     Синхронизирует Airtight.AirBlocked у всех блокеров у двери
    ///     Если у двери есть AirtightComponent то берем его AirBlocked
    /// </summary>
    private void RefreshAirblock(Entity<SunriseMultiTileAirtightComponent> ent)
    {
        bool blocked;

        if (_airtightQuery.TryGetComponent(ent.Owner, out var doorAirtight))
            blocked = doorAirtight.AirBlocked;
        else
        {
            if (!_doorQuery.TryGetComponent(ent.Owner, out var door))
                return;

            blocked = door.State is DoorState.Closed or DoorState.Welded;
        }

        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!_airtightQuery.TryGetComponent(blocker, out var airtight))
                continue;

            _airtight.SetAirblocked((blocker, airtight), blocked);
        }
    }

    private void DeleteBlockers(Entity<SunriseMultiTileAirtightComponent> ent)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!TerminatingOrDeleted(blocker))
                Del(blocker);
        }

        ent.Comp.Blockers.Clear();
    }

    // удаляем блокер если не привязан шлюз
    private void DeleteStaleBlockersOnTile(Entity<MapGridComponent> gridEnt, Vector2i tile, EntityUid sourceDoor)
    {
        _anchoredEntities.Clear();
        _map.GetAnchoredEntities(gridEnt, tile, _anchoredEntities);

        foreach (var blocker in _anchoredEntities)
        {
            if (!_blockerQuery.HasComp(blocker))
                continue;

            if (TerminatingOrDeleted(blocker))
                continue;

            if (IsManagedByOtherDoor(blocker, sourceDoor))
                continue;

            Del(blocker);
        }

        _anchoredEntities.Clear();
    }

    // Проверка привязки блокера к шлюзу
    private bool IsManagedByOtherDoor(EntityUid blocker, EntityUid sourceDoor)
    {
        var query = EntityQueryEnumerator<SunriseMultiTileAirtightComponent>();
        while (query.MoveNext(out var door, out var multiTile))
        {
            if (door == sourceDoor || TerminatingOrDeleted(door))
                continue;

            if (multiTile.Blockers.Contains(blocker))
                return true;
        }

        return false;
    }

    private static EntityCoordinates GetTileCenter(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // +0.5f чтобы получить центр тайла, а то берет то правый угол, то левый
        var pos = new Vector2(tile.X + 0.5f, tile.Y + 0.5f) * grid.TileSize;
        return new EntityCoordinates(gridUid, pos);
    }
}
