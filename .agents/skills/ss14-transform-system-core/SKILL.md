---
name: ss14-transform-system-core
description: An in-depth practical guide to TransformSystem in Space Station 14: coordinate model (EntityCoordinates/MapCoordinates), parent-grid-map hierarchy, safe movement, anchor/unbind and client/server patterns. Use for teleports, entity transfers, containers, anchoring and spatial optimizations.
---

# TransformSystem: Core

This skill covers exactly the architecture and working techniques of TransformSystem :)
For the full API catalog, see the separate skill `SS14 Transform System API`.

## Mental model

Transform to SS14 = space tree:

1. `ParentUid`: immediate parent in the hierarchy.
2. `GridUid`: the grid on which the entity is located (or `null`).
3. `MapUid` / `MapID`: the card where the entity is located.
4. `EntityCoordinates`: local coordinates relative to `ParentUid`.
5. `MapCoordinates`: world coordinates inside the map.

Key idea:
- first think about what space the current coordinates are in;
- then choose an API that explicitly translates or saves this space.

## Moving pattern

Basic flow when moving through `SetCoordinates(...)`:

1. If necessary, remove the anchor (`Unanchor`), if the movement allows it.
2. Update the local position/rotation and, when changing the parent, recalculate the map/grid membership.
3. Raise motion events (`MoveEvent`, `EntParentChangedMessage`) and update spatial subsystems.
4. Perform post-step traversal so that the entity ends up on the correct grid/map parent.

Practical consequence:
- do not mix direct mutation `TransformComponent` and system methods;
- system methods hold tree, broadphase and network state invariants ✅

## Quick API selection

1. Need to shift in local space without changing parent?
- `SetLocalPosition`, `SetLocalRotation`, `SetLocalPositionRotation`.
2. Need to be placed at a world point/angle?
- `SetWorldPosition`, `SetWorldRotation`, `SetWorldPositionRotation`, or `SetMapCoordinates`.
3. Do you need to transfer between parent-space (container/entity/grid/map)?
- `SetCoordinates`, then, if necessary, `AttachToGridOrMap`.
4. Should I “put it next to it” taking into account the containers?
- `DropNextTo` or `PlaceNextTo`.
5. Do you need to correctly compare the distance between different parent-spaces?
- `InRange` rather than manual subtraction of local vectors.

## Patterns

- Do explicit normalization after teleport: `SetCoordinates` -> `AttachToGridOrMap`.
- To change the coordinate system, use `ToMapCoordinates`/`ToCoordinates`.
- For overlay/visualization, take pairs of matrices through `GetWorldPositionRotationMatrixWithInv`.
- For tile logic, use helper methods (`GetGridOrMapTilePosition`, `TryGetGridTilePosition`).
- For container-resistant drops, use `DropNextTo`.

## Anti-patterns

- Change transform fields directly, bypassing `SharedTransformSystem` ⚠️
- Compare local positions of different parent trees as if they were one space.
- Teleport via `SetCoordinates` and forget to rebind to the real grid.
- Use low-level `SetGridId` in game code without a strict reason.
- Log the position without fallback via `TryGetMapOrGridCoordinates`.

## Code examples

### Example 1: safe dash with parent-space normalization

```csharp
// 1) Transfer the entity to the target EntityCoordinates (from the ability).
_transform.SetCoordinates(user, xform, args.Target);

// 2) Normalize parent to the actual grid/map at the target point.
_transform.AttachToGridOrMap(user, xform);
```

### Example 2: “implanting” a projectile using SetParent

```csharp
// We stop physics and make the projectile static.
_physics.SetLinearVelocity(projectile, Vector2.Zero, body: body);
_physics.SetBodyType(projectile, BodyType.Static, body: body);

// Re-attach the projectile to the target.
_transform.SetParent(projectile, projectileXform, target);

// We apply local offset after the parent-change.
_transform.SetLocalPosition(
    projectile,
    projectileXform.LocalPosition + rotation.RotateVec(embedOffset),
    projectileXform);
```

### Example 3: overlay rendering using world+inv matrix

```csharp
var (_, _, worldMatrix, invWorldMatrix) =
    _transform.GetWorldPositionRotationMatrixWithInv(gridXform, xforms);

// We translate the camera bounds into local grid coordinates.
var localBounds = invWorldMatrix.TransformBox(worldBounds).Enlarged(grid.TileSize * 2);

// We draw in the local space of the grid.
drawHandle.SetTransform(worldMatrix);
```

### Example 4: anchoring/unanchoring using system methods only

```csharp
if (!xform.Anchored)
    _transform.AnchorEntity(uid, xform);

// ...game logic...

if (xform.Anchored)
    _transform.Unanchor(uid, xform);
```

### Example 5: pop-up direction via mover-coordinates

```csharp
// MoverCoordinates gives operational coordinates in grid/map terms.
var moverCoords = _transform.GetMoverCoordinates(observer);

// Based on them, we select the side of the signature.
var horizontalDir = moverCoords.X <= popupOrigin.X ? 1f : -1f;
```

## Server and client usage guidelines

- Server patterns: anchor/unanchor, swap/drop/place, safe teleport, transfer to containers and back.
- Client patterns: matrix transformations for overlay, UI positioning through mover coords, tile checks through helper methods.
- General principle: server-authoritative state + client mapping calculation without violating transform invariants.

## Mini-checklist before changes

- It is clear in which space the input coordinates are located.
- To change space, use `ToMapCoordinates`/`ToCoordinates`.
- After the teleport, `AttachToGridOrMap` was normalized (if logically necessary).
- Anchor logic is implemented via `AnchorEntity`/`Unanchor`.
- No direct mutation of legacy component setters.
