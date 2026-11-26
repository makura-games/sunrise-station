---
name: ss14-physics-system-api
description: A complete reference to the SharedPhysicsSystem public API in Space Station 14: all method families, overloads, limitations, rare methods and practical examples of use on the server and client. Use it when you need to accurately select the PhysicsSystem method and not break contacts/collisions/prediction.
---

# PhysicsSystem: API

This skill is the public API directory `SharedPhysicsSystem` :)
For the general architecture, first read `SS14 Physics System Core`.

## How to use this directory

1. First select the task type:
- strength/speed,
- body type/sleep/conflict,
- fixtures/shapes,
- contacts/raycasts/distances,
- transform transformations.

2. Then select the level:
- safe gameplay method,
- or a low-level engine method (rarely needed).

3. If fixtures/collisions have changed:
- check if `RegenerateContacts` is needed.

## 1) Runtime and world level

- `Initialize()`
- `Shutdown()`
- `Step(float frameTime, bool prediction)`
- `SetGravity(Vector2 value)`
- `UpdateIsPredicted(EntityUid? uid, PhysicsComponent? physics = null)` (virtual)

Public runtime fields/state:
- `Gravity`
- `AwakeBodies`
- `EffectiveCurTime` (substep-aware time)

When to use:
- gameplay code usually does not call `Initialize/Shutdown/Step` directly.
- `SetGravity` and `UpdateIsPredicted`-flow are useful.

## 2) Impulses and forces

- `ApplyAngularImpulse(EntityUid uid, float impulse, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `ApplyForce(EntityUid uid, Vector2 force, Vector2 point, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `ApplyForce(EntityUid uid, Vector2 force, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `ApplyTorque(EntityUid uid, float torque, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `ApplyLinearImpulse(EntityUid uid, Vector2 impulse, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `ApplyLinearImpulse(EntityUid uid, Vector2 impulse, Vector2 point, FixturesComponent? manager = null, PhysicsComponent? body = null)`

Nuance:
- the methods themselves try to wake up the body; there will be no effect for immobile bodies.

## 3) Body dynamics and condition

### 3.1 Speeds, damping, weight

- `DestroyContacts(PhysicsComponent body)`
- `DestroyContact(Contact contact)`
- `ResetDynamics(EntityUid uid, PhysicsComponent body, bool dirty = true)`
- `ResetMassData(EntityUid uid, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetAngularVelocity(EntityUid uid, float value, bool dirty = true, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetLinearVelocity(EntityUid uid, Vector2 velocity, bool dirty = true, bool wakeBody = true, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetAngularDamping(EntityUid uid, PhysicsComponent body, float value, bool dirty = true)`
- `SetLinearDamping(EntityUid uid, PhysicsComponent body, float value, bool dirty = true)`
- `SetFriction(EntityUid uid, PhysicsComponent body, float value, bool dirty = true)`
- `SetInertia(EntityUid uid, PhysicsComponent body, float value, bool dirty = true)`
- `SetLocalCenter(EntityUid uid, PhysicsComponent body, Vector2 value)`

### 3.2 Awake/sleep/prediction

- `[Obsolete] SetAwake(EntityUid uid, PhysicsComponent body, bool value, bool updateSleepTime = true)`
- `SetAwake(Entity<PhysicsComponent> ent, bool value, bool updateSleepTime = true)`
- `SetSleepingAllowed(EntityUid uid, PhysicsComponent body, bool value, bool dirty = true)`
- `SetSleepTime(PhysicsComponent body, float value)`
- `WakeBody(EntityUid uid, bool force = false, FixturesComponent? manager = null, PhysicsComponent? body = null)`

### 3.3 Body mode and collision

- `TrySetBodyType(EntityUid uid, BodyType value, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetBodyType(EntityUid uid, BodyType value, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetBodyStatus(EntityUid uid, PhysicsComponent body, BodyStatus status, bool dirty = true)`
- `SetCanCollide(EntityUid uid, bool value, bool dirty = true, bool force = false, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetFixedRotation(EntityUid uid, bool value, bool dirty = true, FixturesComponent? manager = null, PhysicsComponent? body = null)`

Critical:
- `BodyType` affects the solver.
- `BodyStatus` is a game status flag, not a replacement for `BodyType` ⚠️

## 4) Speeds in map terms

- `GetLinearVelocity(EntityUid uid, Vector2 point, PhysicsComponent? component = null, TransformComponent? xform = null)`
- `GetMapLinearVelocity(EntityCoordinates coordinates)`
- `GetMapLinearVelocity(EntityUid uid, PhysicsComponent? component = null, TransformComponent? xform = null)`
- `GetMapAngularVelocity(EntityUid uid, PhysicsComponent? component = null, TransformComponent? xform = null)`
- `GetMapVelocities(EntityUid uid, PhysicsComponent? component = null, TransformComponent? xform = null)`

When to use:
- any logic in world/map-space, especially with a parent hierarchy.

## 5) Fixtures: material and collision profile

- `SetDensity(EntityUid uid, string fixtureId, Fixture fixture, float value, bool update = true, FixturesComponent? manager = null)`
- `SetFriction(EntityUid uid, string fixtureId, Fixture fixture, float value, bool update = true, FixturesComponent? manager = null)`
- `SetHard(EntityUid uid, Fixture fixture, bool value, FixturesComponent? manager = null)`
- `SetRestitution(EntityUid uid, Fixture fixture, float value, bool update = true, FixturesComponent? manager = null)`
- `ScaleFixtures(Entity<FixturesComponent?> ent, float factor)`
- `IsCurrentlyHardCollidable(Entity<FixturesComponent?, PhysicsComponent?> bodyA, Entity<FixturesComponent?, PhysicsComponent?> bodyB)`
- `IsHardCollidable(Entity<FixturesComponent?, PhysicsComponent?> bodyA, Entity<FixturesComponent?, PhysicsComponent?> bodyB)`
- `AddCollisionMask(EntityUid uid, string fixtureId, Fixture fixture, int mask, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetCollisionMask(EntityUid uid, string fixtureId, Fixture fixture, int mask, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `RemoveCollisionMask(EntityUid uid, string fixtureId, Fixture fixture, int mask, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `AddCollisionLayer(EntityUid uid, string fixtureId, Fixture fixture, int layer, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `SetCollisionLayer(EntityUid uid, string fixtureId, Fixture fixture, int layer, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `RemoveCollisionLayer(EntityUid uid, string fixtureId, Fixture fixture, int layer, FixturesComponent? manager = null, PhysicsComponent? body = null)`

Practice:
- after significant collision/fixture changes, `RegenerateContacts` is often needed.

## 5.1) CollisionGroup: full map of layers/masks :)

### Rule of Contact (Source of Truth)

Two fixtures are considered to be in contact if at least one condition is met:
- `(A.mask & B.layer) != 0`
- `(B.mask & A.layer) != 0`

That is, this is **not strict two-way filtering**, but the logic “at least one side wants contact.”

```csharp
// Basic contact check of a pair of fixtures.
var shouldCollide =
    (fixtureA.CollisionMask & fixtureB.CollisionLayer) != 0 ||
    (fixtureB.CollisionMask & fixtureA.CollisionLayer) != 0;
```

Important:
- `shouldCollide` is responsible for contact in broadphase/narrowphase.
- Physical "resting" additionally requires `Hard = true` on both sides.

### Matrix of basic layer/mask bits (all basic flags)

`✓` - the layer will be caught by this mask, `·` - it will not.

| layer \ mask | Opaque | Impassable | MidImpassable | HighImpassable | LowImpassable | GhostImpassable | BulletImpassable | InteractImpassable | DoorPassable |
|---|---|---|---|---|---|---|---|---|---|
| Opaque | ✓ | · | · | · | · | · | · | · | · |
| Impassable | · | ✓ | · | · | · | · | · | · | · |
| MidImpassable | · | · | ✓ | · | · | · | · | · | · |
| HighImpassable | · | · | · | ✓ | · | · | · | · | · |
| LowImpassable | · | · | · | · | ✓ | · | · | · | · |
| GhostImpassable | · | · | · | · | · | ✓ | · | · | · |
| BulletImpassable | · | · | · | · | · | · | ✓ | · | · |
| InteractImpassable | · | · | · | · | · | · | · | ✓ | · |
| DoorPassable | · | · | · | · | · | · | · | · | ✓ |

### Ready-made CollisionGroups: what they are and who to issue them to

The `Who to grant to` column describes a practical intent (a typical content profile), and not a hard and fast rule.

| Group | Use as | Composition | To whom to issue |
|---|---|---|---|
| `None` | layer/mask | `0` | temporarily disabled filter, service cases |
| `Opaque` | layer | base bit | objects that block light/part of the rays |
| `Impassable` | layer/mask bit | base bit | full-fledged obstacles on the ground |
| `MidImpassable` | layer/mask bit | base bit | "medium height": mobs, racks, some furniture |
| `HighImpassable` | layer/mask bit | base bit | top of obstacles (tables, high blockers) |
| `LowImpassable` | layer/mask bit | base bit | low obstacles important for small/under-table logic |
| `GhostImpassable` | layer/mask bit | base bit | Ghost/Watcher Blockers |
| `BulletImpassable` | layer/mask bit | base bit | everything that should stop bullets |
| `InteractImpassable` | layer/mask bit | base bit | lock `InRangeUnobstructed`/interaction |
| `DoorPassable` | layer/mask bit | base bit | special "door-passable" surfaces |
| `MapGrid` | layer | map/grid specialbit | grid system layer |
| `AllMask` | mask | `-1` | rare force cases (almost always redundant) |
| `SingularityLayer` | layer+mask | `Opaque+Impassable+Mid+High+Low+Bullet+Interact+DoorPassable` | singularity/similar "all-consuming" entities |
| `MobMask` | mask | `Impassable+High+Mid+Low` | humanoids/regular ground mobs |
| `MobLayer` | layer | `Opaque+BulletImpassable` | base layer of normal mobs |
| `SmallMobMask` | mask | `Impassable+Low` | small mobs (mice, etc.) |
| `SmallMobLayer` | layer | `Opaque+BulletImpassable` | small mobs layer |
| `FlyingMobMask` | mask | `Impassable+High` | small flying entities |
| `FlyingMobLayer` | layer | `Opaque+BulletImpassable` | flying mobs layer |
| `LargeMobMask` | mask | `Impassable+High+Mid+Low` | large entities/vehicles/mechs |
| `LargeMobLayer` | layer | `Opaque+High+Mid+Low+BulletImpassable` | large objects that "take up" more height |
| `MachineMask` | mask | `Impassable+Mid+Low` | large machines/stationary structures |
| `MachineLayer` | layer | `Opaque+Mid+Low+BulletImpassable` | machines, automatic machines, frames |
| `ConveyorMask` | layer or mask in place | `Impassable+Mid+Low+DoorPassable` | conveyors and door compatibility |
| `CrateMask` | mask | `Impassable+High+Low` | containers/cages that require "low traffic" under flaps |
| `TableMask` | mask | `Impassable+Mid` | tables/surfaces with which other tables must interact |
| `TableLayer` | layer | `MidImpassable` | tables, railings, part of narrow barriers |
| `TabletopMachineMask` | mask | `Impassable+High` | desktop machines/windows |
| `TabletopMachineLayer` | layer | `Opaque+BulletImpassable` | small desktop devices |
| `GlassAirlockLayer` | layer | `High+Mid+Bullet+Interact` | glass airlock/window profiles |
| `AirlockLayer` | layer | `Opaque+GlassAirlockLayer` | regular airlock profiles |
| `HumanoidBlockLayer` | layer | `High+Mid` | assemblies/intermediate "human blocks" |
| `SlipLayer` | layer | `Mid+Low` | non-hard slip/puddle/trap triggers |
| `ItemMask` | mask | `Impassable+High` | objects, grenades, small props |
| `ThrownItem` | layer | `Impassable+High+Bullet` | special throw profiles (rare point case) |
| `WallLayer` | layer | `Opaque+Impassable+High+Mid+Low+Bullet+Interact` | full walls/barriers |
| `GlassLayer` | layer | `Impassable+High+Mid+Low+Bullet+Interact` | windows/glass obstacles |
| `HalfWallLayer` | layer | `Mid+Low` | "semi-high" obstacles |
| `FlimsyLayer` | layer | `Opaque+High+Mid+Low+Interact` | "fragile" walls that should not catch bullets like wall |
| `SpecialWallLayer` | layer+mask | `Opaque+High+Mid+Low+Bullet` | force-wall type: blocks movement/bullets, but does not block interact the same way as `WallLayer` |
| `FullTileMask` | mask | `Impassable+High+Mid+Low+Interact` | full tile blocker (walls, windows, doors, memorials, etc.) |
| `FullTileLayer` | layer | `Opaque+High+Mid+Low+Bullet+Interact` | rare non-hard/full-tile sensors and special fixtures |
| `SubfloorMask` | mask | `Impassable+Low` | underground/pipes/underground networks |

### Quick delivery templates (from current configs)

- Humanoid/most mobs: `mask = MobMask`, `layer = MobLayer`.
- Small mob: `mask = SmallMobMask`, `layer = SmallMobLayer`.
- Flying mob: `mask = FlyingMobMask`, `layer = FlyingMobLayer`.
- Base structure/machine: `mask = MachineMask`, `layer = MachineLayer` or `Mid+Low` for generic base.
- Table: `mask = TableMask`, `layer = TableLayer`.
- Desktop machine: `mask = TabletopMachineMask`, `layer = TabletopMachineLayer`.
- Window: `mask = FullTileMask`, `layer = GlassLayer`.
- Regular door: `mask = FullTileMask`, `layer = AirlockLayer` (with welding in `WallLayer`).
- Glass door/window: `mask = FullTileMask` or `TabletopMachineMask` according to door type, `layer = GlassAirlockLayer`.
- Wall: `mask = FullTileMask`, `layer = WallLayer`.
- Item: `mask = ItemMask`, `layer` often `0` (if the layer is not needed).
- Slippery sensor/trap: `hard = false`, `mask = ItemMask`, `layer = SlipLayer`.
- Underground pipe: `mask = SubfloorMask`, usually with collision turned off to the desired anchored-state.
- Observer/incorporator: more often `layer = GhostImpassable`, `mask = 0`.
- Singularity: symmetric profile `layer = mask = SingularityLayer`.

### Who contacts whom: matrix of typical profiles

`✓` - contact is possible via `layer/mask`, `·` - no.
For `Airlock` the standard profile is adopted here: `layer = AirlockLayer`, `mask = FullTileMask`.
For `Item` and `SubfloorPipe` the common case with null `layer` is accepted.

| Profile | Mob | SmallMob | FlyingMob | Machine | Table | Airlock | Wall | Item | SlipTrigger(non-hard) | SubfloorPipe |
|---|---|---|---|---|---|---|---|---|---|---|
| Mob | · | · | · | ✓ | ✓ | ✓ | ✓ | · | ✓ | · |
| SmallMob | · | · | · | ✓ | · | · | ✓ | · | ✓ | · |
| FlyingMob | · | · | · | · | · | ✓ | ✓ | · | · | · |
| Machine | ✓ | ✓ | · | ✓ | ✓ | ✓ | ✓ | · | ✓ | ✓ |
| Table | ✓ | · | · | ✓ | ✓ | ✓ | ✓ | · | ✓ | · |
| Airlock | ✓ | · | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | · |
| Wall | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Item | · | · | · | · | · | ✓ | ✓ | · | · | · |
| SlipTrigger(non-hard) | ✓ | ✓ | · | ✓ | ✓ | ✓ | ✓ | · | · | ✓ |
| SubfloorPipe | · | · | · | ✓ | · | · | ✓ | · | ✓ | · |

### Patterns for setting up collision

- Use ready-made pairs (`MobMask+MobLayer`, `TabletopMachineMask+TabletopMachineLayer`) instead of manually assembling bits.
- For full-tile blockers, keep `mask = FullTileMask`, and change the behavior to `layer` (`WallLayer`, `GlassLayer`, `AirlockLayer`).
- For triggers (puddles/sliding/sensors) set `hard = false`, and filter through `layer/mask`.
- For special passes (conveyor under doors, underground) use `DoorPassable`/`SubfloorMask`, not ad-hoc exceptions.

### Anti-patterns collision settings

- Give `AllMask` to regular gameplay entities.
- Mix `WallLayer` and `SpecialWallLayer` without an explicit goal for interaction/blocking behavior.
- Include `InteractImpassable` where the object must be shot/passed for interaction checks.
- Change `layer/mask` and do not re-sync contacts (`RegenerateContacts`) if there is a noticeable change in behavior.

### Example configurations

```yaml
# Typical mob: standard ground collision.
fixtures:
  fix1:
    shape: !type:PhysShapeCircle { radius: 0.35 }
    mask: [MobMask]
    layer: [MobLayer]
```

```yaml
# Full Tile Blocker (Wall): Blocks passage, interactions, and bullets.
fixtures:
  fix1:
    shape: !type:PhysShapeAabb { bounds: "-0.5,-0.5,0.5,0.5" }
    mask: [FullTileMask]
    layer: [WallLayer]
```

```yaml
# Sliding trap: not hard, but gives a contact event based on the required mask.
fixtures:
  floortrap:
    hard: false
    shape: !type:PhysShapeAabb { bounds: "-0.4,-0.4,0.4,0.4" }
    mask: [ItemMask]
    layer: [SlipLayer]
```

## 6) Shapes API (fixture geometry)

- `SetRadius(EntityUid uid, string fixtureId, Fixture fixture, IPhysShape shape, float radius, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetPositionRadius(EntityUid uid, string fixtureId, Fixture fixture, PhysShapeCircle shape, Vector2 position, float radius, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetPosition(EntityUid uid, string fixtureId, Fixture fixture, PhysShapeCircle circle, Vector2 position, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetVertices(EntityUid uid, string fixtureId, Fixture fixture, EdgeShape edge, Vector2 vertex0, Vector2 vertex1, Vector2 vertex2, Vector2 vertex3, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `SetVertices(EntityUid uid, string fixtureId, Fixture fixture, PolygonShape poly, Vector2[] vertices, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`

## 7) Contacts and contact selections

- `RegenerateContacts(Entity<PhysicsComponent?> entity)`
- `GetTouchingContacts(Entity<FixturesComponent?> entity, string? ignoredFixtureId = null)`
- `GetContacts(Entity<FixturesComponent?> entity, bool includeDeleting = false)`
- `GetContactingEntities(Entity<PhysicsComponent?> ent, HashSet<EntityUid> contacting, bool approximate = false)`
- `GetContactingEntities(EntityUid uid, PhysicsComponent? body = null, bool approximate = false)`
- `IsInContact(PhysicsComponent body, bool approximate = false)`

`ContactEnumerator`:
- `MoveNext(out Contact? contact)`
- useful for iteration without unnecessary allocations.

## 8) Transformations of physics and bounds

- `GetRelativePhysicsTransform(Transform worldTransform, Entity<TransformComponent?> relative)`
- `GetRelativePhysicsTransform(Entity<TransformComponent?> entity, Entity<TransformComponent?> relative)`
- `GetLocalPhysicsTransform(EntityUid uid, TransformComponent? xform = null)`
- `GetPhysicsTransform(EntityUid uid, TransformComponent? xform = null)`
- `GetWorldAABB(EntityUid uid, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `GetHardAABB(EntityUid uid, FixturesComponent? manager = null, PhysicsComponent? body = null, TransformComponent? xform = null)`
- `GetHardCollision(EntityUid uid, FixturesComponent? manager = null)`
- `GetHardCollision(FixturesComponent manager)` (static)

## 9) Spatial query and ray API

- `TryCollideRect(Box2 collider, MapId mapId, bool approximate = true)`
- `GetEntitiesIntersectingBody(EntityUid uid, int collisionMask, bool approximate = true, PhysicsComponent? body = null, FixturesComponent? fixtureComp = null, TransformComponent? xform = null)`
- `[Obsolete] GetCollidingEntities(MapId mapId, in Box2 worldAABB)`
- `[Obsolete] GetCollidingEntities(MapId mapId, in Box2Rotated worldBounds)`
- `IntersectRayWithPredicate(MapId mapId, CollisionRay ray, float maxLength = 50F, Func<EntityUid, bool>? predicate = null, bool returnOnFirstHit = true)`
- `IntersectRayWithPredicate<TState>(MapId mapId, CollisionRay ray, TState state, Func<EntityUid, TState, bool> predicate, float maxLength = 50F, bool returnOnFirstHit = true)`
- `IntersectRay(MapId mapId, CollisionRay ray, float maxLength = 50, EntityUid? ignoredEnt = null, bool returnOnFirstHit = true)`
- `IntersectRayPenetration(MapId mapId, CollisionRay ray, float maxLength, EntityUid? ignoredEnt = null)`
- `TryGetDistance(EntityUid uidA, EntityUid uidB, out float distance, TransformComponent? xformA = null, TransformComponent? xformB = null, FixturesComponent? managerA = null, FixturesComponent? managerB = null, PhysicsComponent? bodyA = null, PhysicsComponent? bodyB = null)`
- `TryGetNearestPoints(EntityUid uidA, EntityUid uidB, out Vector2 pointA, out Vector2 pointB, TransformComponent? xformA = null, TransformComponent? xformB = null, FixturesComponent? managerA = null, FixturesComponent? managerB = null, PhysicsComponent? bodyA = null, PhysicsComponent? bodyB = null)`
- `TryGetNearest(EntityUid uidA, EntityUid uidB, out Vector2 pointA, out Vector2 pointB, out float distance, Transform xfA, Transform xfB, FixturesComponent? managerA = null, FixturesComponent? managerB = null, PhysicsComponent? bodyA = null, PhysicsComponent? bodyB = null)`
- `TryGetNearest(EntityUid uid, MapCoordinates coordinates, out Vector2 point, out float distance, TransformComponent? xformA = null, FixturesComponent? manager = null, PhysicsComponent? body = null)`
- `TryGetNearest(EntityUid uidA, EntityUid uidB, out Vector2 point, out Vector2 pointB, out float distance, TransformComponent? xformA = null, TransformComponent? xformB = null, FixturesComponent? managerA = null, FixturesComponent? managerB = null, PhysicsComponent? bodyA = null, PhysicsComponent? bodyB = null)`

## 10) Physics step events (useful for controllers)

- `PhysicsUpdateBeforeSolveEvent`
- `PhysicsUpdateAfterSolveEvent`

Usage:
- subscribe for logic that should go before/after solve within the same tick.

## 11) API selection patterns :)

- Need a controlled push: `ApplyLinearImpulse`.
- We need a stable “engine”/traction: `ApplyForce` + correct `SetSleepingAllowed`.
- It is necessary to “lift” the gameplay entity into the air: `SetBodyStatus(InAir)` + adjust the mask/layer if necessary.
- It is necessary to temporarily disable collisions: `SetCanCollide(false)` or `SetHard(false)` for specific fixtures.
- A contact reaction is needed after a sudden change in collision: `RegenerateContacts`.
- We need the correct distance between complex shapes: `TryGetNearest*`.
- Need conversion between grid/map spaces: `GetRelativePhysicsTransform`/`GetLocalPhysicsTransform`.

## 12) Anti-patterns

- Confuse `BodyType` and `BodyStatus`.
- Edit fixture/body fields directly, bypassing the API.
- Insert/remove from container without extinguishing physics.
- Use `[Obsolete]` query methods where there are already more precise `TryGetNearest`/ray methods.
- Call rare engine methods (`Step`, `Initialize`, `DestroyContact`) from regular gameplay logic.

## 13) Examples from code

### Example 1: shuttle/motor via power and sleep

```csharp
if (finalForce.Length() > 0f)
    PhysicsSystem.ApplyForce(shuttleUid, finalForce, body: body);

// Without entering, sleep can be allowed so as not to keep the body “eternally angry.”
PhysicsSystem.SetSleepingAllowed(shuttleUid, body, true);
```

### Example 2: contact iterator as a "who's nearby" source

```csharp
var contacts = PhysicsSystem.GetContacts(conveyorUid);
while (contacts.MoveNext(out var contact))
{
    var other = contact.OtherEnt(conveyorUid);
    PhysicsSystem.WakeBody(other);
}
```

### Example 3: re-check contacts after changing physical mode

```csharp
_physics.SetBodyType(uid, BodyType.Dynamic, fixtures, body, xform);
_physics.SetCanCollide(uid, true, manager: fixtures, body: body);
_broadphase.RegenerateContacts((uid, body, fixtures, xform));
```

### Example 4: nearest-query for fair geometric distance

```csharp
if (!_physics.TryGetNearest(uidA, uidB, out var pointA, out var pointB, out var distance, xfA, xfB))
    return;

if (distance <= interactionRange)
{
    // Objects are actually within the radius, taking into account the shapes of the fixtures.
}
```

### Example 5: runtime switching "in the air" with sleep prohibition

```csharp
_physics.SetBodyStatus(target, targetPhysics, BodyStatus.InAir, false);
_physics.SetSleepingAllowed(target, targetPhysics, false);
_physics.WakeBody(target, body: targetPhysics);
```

### Example 6: point raycast with entity filter

```csharp
var ray = new CollisionRay(origin, direction, mask);
var results = _physics.IntersectRayWithPredicate(
    mapId,
    ray,
    maxDistance,
    uid => uid == source || uid == ignored,
    returnOnFirstHit: false);
```

### Example 7: mass scale of physical forms via system API

```csharp
// Used for consistent visual and physics scaling.
_physics.ScaleFixtures(entity, factor);
```

### Example 8: Selecting local physics-transform (not just local transform)

```csharp
// For some intersection checks, the physics-space of the current broadphase is needed.
var physXf = PhysicsSystem.GetLocalPhysicsTransform(uid);
```

## 14) Mini-checklist before calling the method

- Is the correct API layer selected (high-level gameplay vs low-level engine)?
- Is there a risk that the object is now in a container/on another map?
- Is `WakeBody` needed?
- After changing fixture/collision, is `RegenerateContacts` needed?
- Don't you use `[Obsolete]` where there is an actual method?
