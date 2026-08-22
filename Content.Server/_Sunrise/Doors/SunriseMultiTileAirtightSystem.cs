using System.Numerics;
using Content.Server._Sunrise.Doors.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Sunrise.Doors.Systems;

/// <summary>
///     Нужна чтобы мультитайловые двойные или тройные шлюзы нормально не пропускали газы
///     Спавнит блокеры на соседних тайлах и регулирует когда блокеры не пропускают газ, когда пропускают. В зависимости от состояния шлюза
/// </summary>
public sealed partial class SunriseMultiTileAirtightSystem : EntitySystem
{
    private const string BlockerPrototype = "SunriseMultiTileAirtightBlocker";

    [Dependency] private AirtightSystem _airtight = default!;
    [Dependency] private TransformSystem _transform = default!;

    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseMultiTileAirtightComponent, MapInitEvent>(OnMapInit);
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

    private static EntityCoordinates GetTileCenter(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // +0.5f чтобы получить центр тайла, а то берет то правый угол, то левый
        var pos = new Vector2(tile.X + 0.5f, tile.Y + 0.5f) * grid.TileSize;
        return new EntityCoordinates(gridUid, pos);
    }
}
