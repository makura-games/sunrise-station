using Content.Shared._Sunrise.Sprite.EdgeConnection;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._Sunrise.Sprite.EdgeConnection;

/// <summary>
/// Handles visual edge connections between entities placed adjacent to each other.
/// Updates appearance data based on neighboring entities with matching connection keys.
/// </summary>
public sealed partial class EdgeConnectionSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private const float MinimumMovementDistance = 0.005f;

    [Dependency] private EntityQuery<EdgeConnectionComponent> _edgeQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EdgeConnectionComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EdgeConnectionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EdgeConnectionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<EdgeConnectionComponent, MoveEvent>(OnMove);

    }

    private void OnInit(Entity<EdgeConnectionComponent> ent, ref ComponentInit args)
    {
        UpdateConnections(ent);
        UpdateNeighbors(ent);
    }

    private void OnAnchorChanged(Entity<EdgeConnectionComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateConnections(ent);
        UpdateNeighbors(ent);
    }

    private void OnShutdown(Entity<EdgeConnectionComponent> ent, ref ComponentShutdown args)
    {
        _appearance.SetData(ent, EdgeConnectionVisuals.ConnectionMask, EdgeConnectionFlags.None);
        // Update neighbors when this entity is removed
        UpdateNeighbors(ent);
    }

    private void OnMove(Entity<EdgeConnectionComponent> ent, ref MoveEvent args)
    {
        var rotationChanged = !args.OldRotation.EqualsApprox(args.NewRotation);
        var positionChanged = args.NewPosition.EntityId != args.OldPosition.EntityId ||
                              (args.NewPosition.Position - args.OldPosition.Position).LengthSquared() >=
                              MinimumMovementDistance * MinimumMovementDistance;

        if (!rotationChanged && !positionChanged)
            return;

        // If entity moved between tiles / grids, old neighbors also need a refresh.
        UpdateNeighborsAtCoordinates(args.OldPosition);
        UpdateConnections(ent);
        UpdateNeighbors(ent);
    }

    private void UpdateConnections(Entity<EdgeConnectionComponent> ent)
    {
        var xform = Transform(ent);

        if (!xform.Anchored || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            _appearance.SetData(ent, EdgeConnectionVisuals.ConnectionMask, EdgeConnectionFlags.None);
            return;
        }

        var mask = EdgeConnectionFlags.None;
        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var allowed = ent.Comp.AllowedDirections;
        var rotation = xform.LocalRotation;

        var worldAllowed = RotateDirections(allowed, rotation);

        if ((worldAllowed & EdgeConnectionFlags.East) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(1, 0), ent.Comp.ConnectionKey, EdgeConnectionFlags.West))
                mask |= EdgeConnectionFlags.East;
        }

        if ((worldAllowed & EdgeConnectionFlags.West) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(-1, 0), ent.Comp.ConnectionKey, EdgeConnectionFlags.East))
                mask |= EdgeConnectionFlags.West;
        }

        if ((worldAllowed & EdgeConnectionFlags.North) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(0, 1), ent.Comp.ConnectionKey, EdgeConnectionFlags.South))
                mask |= EdgeConnectionFlags.North;
        }

        if ((worldAllowed & EdgeConnectionFlags.South) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(0, -1), ent.Comp.ConnectionKey, EdgeConnectionFlags.North))
                mask |= EdgeConnectionFlags.South;
        }

        var localMask = RotateDirectionsInverse(mask, rotation);

        _appearance.SetData(ent, EdgeConnectionVisuals.ConnectionMask, localMask);
    }

    private EdgeConnectionFlags RotateDirections(EdgeConnectionFlags flags, Angle rotation)
    {
        return RotateDirectionsImpl(flags, rotation, clockwise: true);
    }

    private EdgeConnectionFlags RotateDirectionsInverse(EdgeConnectionFlags flags, Angle rotation)
    {
        return RotateDirectionsImpl(flags, rotation, clockwise: false);
    }

    private EdgeConnectionFlags RotateDirectionsImpl(EdgeConnectionFlags flags, Angle rotation, bool clockwise)
    {
        var degrees = (int)Math.Round(rotation.Degrees) % 360;
        if (degrees < 0)
            degrees += 360;

        var quarterTurns = (int)Math.Round(degrees / 90.0) % 4;

        if (!clockwise)
            quarterTurns = (4 - quarterTurns) % 4;

        if (quarterTurns == 0)
            return flags;

        for (var i = 0; i < quarterTurns; i++)
        {
            var rotated = EdgeConnectionFlags.None;

            if (clockwise)
            {
                if ((flags & EdgeConnectionFlags.North) != 0)
                    rotated |= EdgeConnectionFlags.East;
                if ((flags & EdgeConnectionFlags.East) != 0)
                    rotated |= EdgeConnectionFlags.South;
                if ((flags & EdgeConnectionFlags.South) != 0)
                    rotated |= EdgeConnectionFlags.West;
                if ((flags & EdgeConnectionFlags.West) != 0)
                    rotated |= EdgeConnectionFlags.North;
            }
            else
            {
                if ((flags & EdgeConnectionFlags.North) != 0)
                    rotated |= EdgeConnectionFlags.West;
                if ((flags & EdgeConnectionFlags.West) != 0)
                    rotated |= EdgeConnectionFlags.South;
                if ((flags & EdgeConnectionFlags.South) != 0)
                    rotated |= EdgeConnectionFlags.East;
                if ((flags & EdgeConnectionFlags.East) != 0)
                    rotated |= EdgeConnectionFlags.North;
            }

            flags = rotated;
        }

        return flags;
    }

    private bool HasMatchingNeighbor(EntityUid entity, EntityUid gridUid, MapGridComponent grid, Vector2i tile, string key, EdgeConnectionFlags requiredDirection)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        var entityXform = Transform(entity);

        while (anchored.MoveNext(out var other))
        {
            if (other == entity)
                continue;

            if (!_edgeQuery.TryComp(other, out var comp) || comp.ConnectionKey != key)
                continue;

            var otherXform = Transform(other.Value);
            if (!otherXform.Anchored)
                continue;

            var otherWorldAllowed = RotateDirections(comp.AllowedDirections, otherXform.LocalRotation);
            if ((otherWorldAllowed & requiredDirection) == 0)
                continue;

            var entityDegrees = ((int)Math.Round(entityXform.LocalRotation.Degrees) % 360 + 360) % 360;
            var otherDegrees = ((int)Math.Round(otherXform.LocalRotation.Degrees) % 360 + 360) % 360;

            if (entityDegrees == otherDegrees)
                return true;
        }

        return false;
    }

    private void UpdateNeighbors(Entity<EdgeConnectionComponent> ent)
    {
        var xform = Transform(ent);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        UpdateNeighborsAtTileNeighborhood(xform.GridUid.Value, grid, tile);
    }

    private void UpdateNeighborsAtCoordinates(EntityCoordinates coordinates)
    {
        if (!TryComp<MapGridComponent>(coordinates.EntityId, out var grid))
            return;

        var tile = _map.TileIndicesFor(coordinates.EntityId, grid, coordinates);
        UpdateNeighborsAtTileNeighborhood(coordinates.EntityId, grid, tile);
    }

    private void UpdateNeighborsAtTileNeighborhood(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // Update all potentially affected neighbors
        UpdateNeighborsAtTile(gridUid, grid, tile + new Vector2i(1, 0));
        UpdateNeighborsAtTile(gridUid, grid, tile + new Vector2i(-1, 0));
        UpdateNeighborsAtTile(gridUid, grid, tile + new Vector2i(0, 1));
        UpdateNeighborsAtTile(gridUid, grid, tile + new Vector2i(0, -1));
    }

    private void UpdateNeighborsAtTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var other))
        {
            if (!_edgeQuery.TryComp(other, out var comp))
                continue;

            UpdateConnections((other.Value, comp));
        }
    }
}
