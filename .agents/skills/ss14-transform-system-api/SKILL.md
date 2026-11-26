---
name: ss14-transform-system-api
description: A complete reference book for the SharedTransformSystem API in Space Station 14: analysis of all public families of methods, selection of overloads, restrictions and practical application patterns on the server and client. Use when you need to accurately select the TransformSystem method and avoid coordinate space errors.
---

# TransformSystem: API

This skill is the full directory of the public API `SharedTransformSystem` :)
For the general architecture and operation scheme, first read `SS14 Transform System Core`.

## How to read this directory

- For each family, first select a space (`EntityCoordinates` or `MapCoordinates`).
- Then select an overload: `uid`-overload or overload with already resolved `TransformComponent`.
- For bulk cycles, give priority to overloads, where you can transfer existing components/queries.

## 1) Lifecycle and events

- `Initialize()`
Initializing Transform system subscriptions.

- `MoveEventHandler` + `OnGlobalMoveEvent`
Global C# event for movement after local `MoveEvent`.

- `TransformStartupEvent`
Transform component start event.

- `ActivateLerp(EntityUid uid, TransformComponent xform)`
Virtual hook for interpolation on the client. Usually not needed for content code.

## 2) Coordinate transformations and distances

- `IsValid(EntityCoordinates coordinates)`
Checking the validity of coordinates.

- `WithEntityId(EntityCoordinates coordinates, EntityUid entity)`
Translation of coordinates into the space of another entity.

- `ToMapCoordinates(...)`
Overload:
`ToMapCoordinates(EntityCoordinates coordinates, bool logError = true)`
`ToMapCoordinates(NetCoordinates coordinates)`

- `ToWorldPosition(...)`
Overload:
`ToWorldPosition(EntityCoordinates coordinates, bool logError = true)`
`ToWorldPosition(NetCoordinates coordinates)`

- `ToCoordinates(...)`
Overload:
`ToCoordinates(Entity<TransformComponent?> entity, MapCoordinates coordinates)`
`ToCoordinates(MapCoordinates coordinates)`

- `GetGrid(...)`
Overload:
`GetGrid(EntityCoordinates coordinates)`
`GetGrid(Entity<TransformComponent?> entity)`

- `GetMapId(...)`
Overload:
`GetMapId(EntityCoordinates coordinates)`
`GetMapId(Entity<TransformComponent?> entity)`

- `GetMap(...)`
Overload:
`GetMap(EntityCoordinates coordinates)`
`GetMap(Entity<TransformComponent?> entity)`

- `InRange(...)`
Overload:
`InRange(EntityCoordinates coordA, EntityCoordinates coordB, float range)`
`InRange(Entity<TransformComponent?> entA, Entity<TransformComponent?> entB, float range)`

## 3) Mover and tile helper API

- `GetMoverCoordinates(...)`
Overload:
`GetMoverCoordinates(EntityUid uid)`
`GetMoverCoordinates(EntityUid uid, TransformComponent xform)`
`GetMoverCoordinates(EntityCoordinates coordinates, EntityQuery<TransformComponent> xformQuery)`
`GetMoverCoordinates(EntityCoordinates coordinates)`

- `GetMoverCoordinateRotation(EntityUid uid, TransformComponent xform)`
Mover coordinates + world rotation.

- `GetGridOrMapTilePosition(EntityUid uid, TransformComponent? xform = null)`
Tile position on a grid or map.

- `GetGridTilePositionOrDefault(Entity<TransformComponent?> entity, MapGridComponent? grid = null)`
Tile position on the grid, or `Vector2i.Zero`.

- `TryGetGridTilePosition(Entity<TransformComponent?> entity, out Vector2i indices, MapGridComponent? grid = null)`
Safe `Try` option.

## 4) Hierarchy, parent, anchoring

- `AnchorEntity(...)`
Overload:
`AnchorEntity(EntityUid uid, TransformComponent xform, EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)` (obsolete)
`AnchorEntity(Entity<TransformComponent> entity, Entity<MapGridComponent> grid, Vector2i tileIndices)`
`AnchorEntity(EntityUid uid, TransformComponent xform, MapGridComponent grid)` (obsolete)
`AnchorEntity(EntityUid uid)`
`AnchorEntity(EntityUid uid, TransformComponent xform)`
`AnchorEntity(Entity<TransformComponent> entity, Entity<MapGridComponent>? grid = null)`

- `Unanchor(...)`
Overload:
`Unanchor(EntityUid uid)`
`Unanchor(EntityUid uid, TransformComponent xform, bool setPhysics = true)`

- `ContainsEntity(EntityUid parent, Entity<TransformComponent?> child)`
Checking nesting in the transform tree.

- `IsParentOf(TransformComponent parent, EntityUid child)`
Quickly check the parent of a children set.

- `SetGridId(...)`
Overload:
`SetGridId(EntityUid uid, TransformComponent xform, EntityUid? gridId, EntityQuery<TransformComponent>? xformQuery = null)`
`SetGridId(Entity<TransformComponent, MetaDataComponent?> ent, EntityUid? gridId)`
Low-level API; usually not used in content code.

- `ReparentChildren(...)`
Overload:
`ReparentChildren(EntityUid oldUid, EntityUid uid)`
`ReparentChildren(EntityUid oldUid, EntityUid uid, EntityQuery<TransformComponent> xformQuery)`

- `GetParent(...)`
Overload:
`GetParent(EntityUid uid)`
`GetParent(TransformComponent xform)`

- `GetParentUid(EntityUid uid)`

- `SetParent(...)`
Overload:
`SetParent(EntityUid uid, EntityUid parent)`
`SetParent(EntityUid uid, TransformComponent xform, EntityUid parent, TransformComponent? parentXform = null)`
`SetParent(EntityUid uid, TransformComponent xform, EntityUid parent, EntityQuery<TransformComponent> xformQuery, TransformComponent? parentXform = null)`

## 5) Local mutations

- `SetLocalPosition(...)`
Overload:
`SetLocalPosition(TransformComponent xform, Vector2 value)` (obsolete)
`SetLocalPosition(EntityUid uid, Vector2 value, TransformComponent? xform = null)`

- `SetLocalPositionNoLerp(...)`
Overload:
`SetLocalPositionNoLerp(TransformComponent xform, Vector2 value)` (obsolete)
`SetLocalPositionNoLerp(EntityUid uid, Vector2 value, TransformComponent? xform = null)`

- `SetLocalRotationNoLerp(EntityUid uid, Angle value, TransformComponent? xform = null)`

- `SetLocalRotation(...)`
Overload:
`SetLocalRotation(EntityUid uid, Angle value, TransformComponent? xform = null)`
`SetLocalRotation(TransformComponent xform, Angle value)` (obsolete)

- `SetCoordinates(...)`
Overload:
`SetCoordinates(EntityUid uid, EntityCoordinates value)`
`SetCoordinates(Entity<TransformComponent, MetaDataComponent> entity, EntityCoordinates value, Angle? rotation = null, bool unanchor = true, TransformComponent? newParent = null, TransformComponent? oldParent = null)`
`SetCoordinates(EntityUid uid, TransformComponent xform, EntityCoordinates value, Angle? rotation = null, bool unanchor = true, TransformComponent? newParent = null, TransformComponent? oldParent = null)`

- `SetLocalPositionRotation(...)`
Overload:
`SetLocalPositionRotation(TransformComponent xform, Vector2 pos, Angle rot)` (obsolete)
`SetLocalPositionRotation(EntityUid uid, Vector2 pos, Angle rot, TransformComponent? xform = null)`

## 6) World position, rotation, map coordinates

- `GetWorldMatrix(...)`
Overloads: uid / component / uid+query / component+query.

- `GetWorldPosition(...)`
Overloads: uid / component / uid+query / component+query.

- `GetMapCoordinates(...)`
Overload:
`GetMapCoordinates(EntityUid entity, TransformComponent? xform = null)`
`GetMapCoordinates(TransformComponent xform)`
`GetMapCoordinates(Entity<TransformComponent> entity)`

- `SetMapCoordinates(...)`
Overload:
`SetMapCoordinates(EntityUid entity, MapCoordinates coordinates)`
`SetMapCoordinates(Entity<TransformComponent> entity, MapCoordinates coordinates)`

- `GetWorldPositionRotation(...)`
Overloads: uid / component / component+query.

- `GetRelativePositionRotation(...)`
Overload:
`GetRelativePositionRotation(TransformComponent component, EntityUid relative, EntityQuery<TransformComponent> query)` (obsolete)
`GetRelativePositionRotation(TransformComponent component, EntityUid relative)`

- `GetRelativePosition(...)`
Overload:
`GetRelativePosition(TransformComponent component, EntityUid relative, EntityQuery<TransformComponent> query)` (obsolete)
`GetRelativePosition(TransformComponent component, EntityUid relative)`

- `SetWorldPosition(...)`
Overload:
`SetWorldPosition(EntityUid uid, Vector2 worldPos)`
`SetWorldPosition(TransformComponent component, Vector2 worldPos)` (obsolete)
`SetWorldPosition(Entity<TransformComponent> entity, Vector2 worldPos)`

- `GetWorldRotation(...)`
Overloads: uid / component / uid+query / component+query.

- `SetWorldRotationNoLerp(Entity<TransformComponent?> entity, Angle angle)`

- `SetWorldRotation(...)`
Overloads: uid / component / uid+query / component+query.

- `SetWorldPositionRotation(EntityUid uid, Vector2 worldPos, Angle worldRot, TransformComponent? component = null)`

## 7) Batch mathematics and matrix bundle API

- `GetInvWorldMatrix(...)`
Overloads: uid / component / uid+query / component+query.

- `GetWorldPositionRotationMatrix(...)`
Overloads: uid / component / uid+query / component+query.

- `GetWorldPositionRotationInvMatrix(...)`
Overloads: uid / component / uid+query / component+query.

- `GetWorldPositionRotationMatrixWithInv(...)`
Overloads: uid / component / uid+query / component+query.

Use these methods when you need several derivatives at once (`pos+rot+matrix`) without unnecessary repeated passes through the hierarchy.

## 8) Attach/Detach and "placement nearby"

- `AttachToGridOrMap(EntityUid uid, TransformComponent? xform = null)`
Normalization of parent to the actual grid or map.

- `TryGetMapOrGridCoordinates(EntityUid uid, out EntityCoordinates? coordinates, TransformComponent? xform = null)`
Securely obtain current grid/map coordinates.

- `DetachParentToNull(EntityUid uid, TransformComponent xform)` (obsolete alias)

- `DetachEntity(...)`
Overload:
`DetachEntity(EntityUid uid, TransformComponent? xform = null)`
`DetachEntity(Entity<TransformComponent?> ent)`
`DetachEntity(EntityUid uid, TransformComponent xform, MetaDataComponent meta, TransformComponent? oldXform, bool terminating = false)`

- `DropNextTo(Entity<TransformComponent?> entity, Entity<TransformComponent?> target)`
Taking into account containers, otherwise nearby in the world.

- `PlaceNextTo(Entity<TransformComponent?> entity, Entity<TransformComponent?> target)`
With the same parent or target container.

- `SwapPositions(Entity<TransformComponent?> entity1, Entity<TransformComponent?> entity2)`
Swap positions/containers with protection from incorrect parent-loop.

## 9) Legacy and restrictions

- Overloads marked `obsolete` are left for compatibility: in the new code, prefer uid/entity options.
- `SetGridId` and `ActivateLerp` are considered low-level APIs.
- Direct legacy property-setters on `TransformComponent` should not be used for new logic.

## Patterns

- In hot loops, resolve `TransformComponent` once and pass it to overloads.
- To change position and angle at the same time, use `SetLocalPositionRotation`.
- For rendering and spatial-culling, use bundle matrix methods.
- After moves from containers and complex parent operations, execute `AttachToGridOrMap` if normalization is required.
- For drop/spawn nearby, use `DropNextTo` instead of manual `SetParent` + `SetCoordinates`.

## Anti-patterns

- Ignore `Try`-result (`TryGetMapOrGridCoordinates`, `TryGetGridTilePosition`).
- Mix local and world coordinates without `ToMapCoordinates`/`ToCoordinates`.
- Use `SetGridId` as the "normal" way to move.
- Try to manually maintain container hierarchy instead of `DropNextTo/PlaceNextTo`.

## Code examples

### Example 1: teleport with the correct choice of parent (grid or map)

```csharp
_transform.AttachToGridOrMap(entity, transform);

if (_map.TryFindGridAt(mapId, position, out var gridUid, out _))
{
    // We translate world -> local grid and put it in grid-space.
    var gridPos = Vector2.Transform(position, _transform.GetInvWorldMatrix(gridUid));
    _transform.SetCoordinates(entity, transform, new EntityCoordinates(gridUid, gridPos));
}
else
{
    // Fallback in map-space.
    _transform.SetWorldPosition((entity, transform), position);
    _transform.SetParent(entity, transform, mapEntity);
}
```

### Example 2: spawn fallback via DropNextTo

```csharp
var uid = Spawn(protoName, overrides, doMapInit);

// If a container insert fails, it is safe to "drop" nearby.
_xforms.DropNextTo(uid, target);
```

### Example 3: tile-safe check via TryGetGridTilePosition

```csharp
if (args.Grid is {} grid
    && _transform.TryGetGridTilePosition(uid, out var tile)
    && _atmosphere.IsTileAirBlockedCached(grid, tile))
{
    return; // The device is in a locked tile.
}
```

### Example 4: GetGridTilePositionOrDefault for atmospheric calculations

```csharp
var indices = _transform.GetGridTilePositionOrDefault((uid, transform));
var tileMix = _atmosphere.GetTileMixture(transform.GridUid, null, indices, true);
```

### Example 5: changing space via WithEntityId

```csharp
// We convert the coordinates to grid-space, snap it and return it back.
var localPos = _transform.WithEntityId(coords, gridUid).Position;
var snappedGrid = new EntityCoordinates(gridUid, snappedLocalPos);
var backToOriginal = _transform.WithEntityId(snappedGrid, coords.EntityId);
```

### Example 6: Exchange Entity Positions

```csharp
// Returns false if swap is not possible (e.g. parent-loop risk).
if (!_transform.SwapPositions(first, second))
    return;
```

## Mini-checklist for choosing a method

- Do you need translation between spaces?
`ToMapCoordinates` / `ToCoordinates` / `WithEntityId`.
- Need local editing?
`SetLocal*`.
- Do you need a world/map edit?
`SetWorld*` or `SetMapCoordinates`.
- Do you need to change parent/hierarchy?
`SetParent` / `SetCoordinates` / `AttachToGridOrMap`.
- Need a container-safe drop/place/swap?
`DropNextTo` / `PlaceNextTo` / `SwapPositions` ✅
