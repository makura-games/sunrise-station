---
name: ss14-physics-system-core
description: Deep practical guide to PhysicsSystem in Space Station 14: simulation device (broadphase, contacts, islands, solver), connection with Transform/Container/Anchoring, client prediction part and working application patterns. Use it when you need to understand how physics actually works and where it is safe to embed game logic.
---

# PhysicsSystem: Core

This skill covers the architecture and mental model of PhysicsSystem :)
For the full directory of the public API, see the separate skill `SS14 Physics System API`.

## Mental model

Physics in SS14 is convenient to think of as a pipeline of 6 layers:

1. `PhysicsComponent`
- Body state: `BodyType`, `CanCollide`, speed, damping, mass, sleep, etc.

2. `FixturesComponent`
- Set of physical body shapes (hitboxes/sensors), their `layer/mask/hard`, material parameters.

3. Broadphase
- Proxy trees on maps/grids, quick search for potential pairs.

4. Contacts
- Nodes of fixture-vs-fixture pairs, lifecycle `Start/Touching/End`.

5. Islands + Solver
- Body/contact/joint graphs, solved in batches and partially in parallel.

6. Sync with Transform
- After solve, the position/rotation is put back into the transform tree.

Key idea:
- Game logic should almost always change physics through system methods (`Set*`, `Apply*`, `RegenerateContacts`, `WakeBody`), and not through direct component fields ✅

## How the tick of physics really works

Typical step (including substeps):

1. `PhysicsUpdateBeforeSolveEvent`.
2. Search for new potential pairs (`FindNewContacts`).
3. Update contacts (`CollideContacts`) and events `Start/EndCollide`.
4. Solve islands (`Step` -> integration of speeds -> constraints -> positions).
5. `PhysicsUpdateAfterSolveEvent`.
6. Finalization/lerp data on the last substep.

Practical conclusion:
- Everything that should act “in the same physical step” must be placed before solve (usually in controller systems or before-solve events).

## Connection with Transform, Anchoring and Containers

PhysicsSystem is tightly connected to the transform hierarchy:

- When parent-change occurs, the behavior of the body is recalculated and the map speed is saved.
- Anchor/unanchor changes not only the transform status, but also the physical mode (through the system flow).
- In a container, the body is usually transferred to `CanCollide = false`, the speeds are reset to zero.

Consequence:
- Any operations “inserted into container”, “dropped parent”, “anchored/unanchored” cannot be considered only visual; this is a full-fledged physical transition ⚠️

## Client and server

General:
- Both the server and the client use a common physical layer and the same key invariants.

Differences:
- The server is authoritative.
- The client additionally controls the prediction flag of entities and carefully recalculates the touching state of contacts when resetting the state in order to reduce mispredict effects.

## Short decision tree

1. Need a one-time push/jerk?
- `ApplyLinearImpulse` / `ApplyAngularImpulse`.

2. Do you need constant thrust/torque?
- `ApplyForce` / `ApplyTorque`.

3. Do you need to switch the physical mode of an object?
- `SetBodyType` (+ if necessary `SetCanCollide`, `SetFixedRotation`, `SetSleepingAllowed`).

4. Do you need to change collision behavior without changing your body type?
- `SetCanCollide` or fixture-API (`SetCollisionMask/Layer`, `SetHard`).

5. Do you need to reassemble the current contacts after a sudden change of collision/fixtures?
- `RegenerateContacts`.

6. Do you need geometry and distances based on real hitboxes?
- `TryGetNearest*`, `GetWorldAABB`, `GetHardAABB`, ray/intersection query API.

## Optimization when using PhysicsSystem :)

### 1) Component optimizations (the cheapest and most effective)

- `CollisionWake`: automatically turns off `CanCollide` for sleeping bodies on the grid (if there are no joints/critical contacts), and turns it back on when waking up.
- `CollideOnAnchor`: Binds `CanCollide` to the anchor/unanchor state.
- These two approaches are usually better than manually "pulling" `SetCanCollide` in 100 places.

```yaml
# Underground/pipe entity: collision is automatically synchronized with anchoring.
components:
- type: Physics
  canCollide: false
- type: CollideOnAnchor
```

```yaml
# Massive small items: collision wake saves updates on the grid.
components:
- type: Physics
- type: CollisionWake
```

### 2) Sleep/wake and frequent speed updates

- For objects with frequent velocity updates, use `SetLinearVelocity(..., wakeBody: false)`, if you don’t need to force wake up every tick.
- Turn on `SetSleepingAllowed(true)` where the body can consistently “fall asleep”.
- Call `WakeBody` only before a real action (push, sharp maneuver, turning on a mechanism).

```csharp
// The controller updates velocity every tick, but an extra wake at every step is not needed.
PhysicsSystem.SetAngularVelocity(uid, angularVelocity);
PhysicsSystem.SetLinearVelocity(uid, velocity, wakeBody: false);
```

### 3) Cheap query/contact patterns

- For contact logic, use `GetContacts`/`ContactEnumerator` (minimum allocations).
- For intersection checks where perfect accuracy is not needed, use `approximate = true`.
- Ray/query filter through a mask in advance, rather than post-filtering a huge result.

```csharp
// Quick broadphase screening: where appropriate, leave approximate = true.
var touching = _physics.GetEntitiesIntersectingBody(uid, collisionMask, approximate: true);
```

### 4) Make changes to fixtures and collisions in batches

- Bad: change `layer/mask/hard` in a loop and rebuild the contacts after each change.
- Good: apply a bunch of changes and do one re-sync.

```csharp
// After a series of edits to the collision profile, we perform one recalculation of contacts.
_physics.SetCollisionMask(uid, id, fixture, newMask, manager: fixtures, body: body);
_physics.SetCollisionLayer(uid, id, fixture, newLayer, manager: fixtures, body: body);
_broadphase.RegenerateContacts((uid, body, fixtures, xform));
```

### 5) Use `hard: false` for trigger mechanics

- Puddles, traps, “auras”, sensors: leave contact events, but do not add the solver load of hard collisions.
- Meaning: the logic works, but the physical “emphasis” does not count.

```yaml
# Trigger fixture: contact is present, there is no hard collision.
fixtures:
  sensor:
    hard: false
    mask: [ItemMask]
    layer: [SlipLayer]
```

### Optimization patterns

- Enable `CollisionWake` for bulk dynamic items.
- Use `CollideOnAnchor` for subfloors/pipes/anchor systems.
- When updating speeds frequently, avoid unnecessary `WakeBody`.
- Keep masks narrow (no extra bits) to reduce the number of potential pairs in the broadphase.
- Use `hard: false` for pure trigger collision layers.

### Anti-optimization patterns

- Globally disable sleep or massively keep bodies forced awake for no reason.
- Give too wide `mask` (or `AllMask`) to regular entities.
- Constantly change the collision profile of the body in the controller update.
- Use only precise/expensive queries where approximate checks are sufficient.
- Do manually what is already covered by `CollisionWake`/`CollideOnAnchor`.

## Code examples

### Example 1: physical step (before solve -> contacts -> solve -> after solve)

```csharp
var updateBeforeSolve = new PhysicsUpdateBeforeSolveEvent(prediction, frameTime);
RaiseLocalEvent(ref updateBeforeSolve);

_broadphase.FindNewContacts();
CollideContacts();
Step(frameTime, prediction);

var updateAfterSolve = new PhysicsUpdateAfterSolveEvent(prediction, frameTime);
RaiseLocalEvent(ref updateAfterSolve);
```

### Example 2: correct transfer of an entity to container mode

```csharp
// When inserted into a container, we dampen the dynamics and turn off collisions.
_physics.SetLinearVelocity(entity, Vector2.Zero, false, body: physics);
_physics.SetAngularVelocity(entity, 0, false, body: physics);
_physics.SetCanCollide(entity, false, false, body: physics);
```

### Example 3: conveyor controller via contact iterator

```csharp
var contacts = PhysicsSystem.GetContacts(conveyorUid);
while (contacts.MoveNext(out var contact))
{
    var other = contact.OtherEnt(conveyorUid);
    if (_conveyedQuery.HasComp(other))
        PhysicsSystem.WakeBody(other);
}
```

### Example 4: predictable kinematics without unnecessary “waking up”

```csharp
// For conveyor movement, it is not always necessary to wake up the body at every update.
PhysicsSystem.SetAngularVelocity(uid, angularVelocity);
PhysicsSystem.SetLinearVelocity(uid, velocity, wakeBody: false);
```

### Example 5: re-sync contacts after changing physical mode

```csharp
_physics.SetBodyType(uid, BodyType.Dynamic, fixtures, body, xform);
_physics.SetCanCollide(uid, true, manager: fixtures, body: body);
_broadphase.RegenerateContacts((uid, body, fixtures, xform));
```

### Example 6: range-check on real hitboxes

```csharp
var xfA = new Transform(worldPosA, worldRotA);
var xfB = new Transform(targetPos.Position, targetRot);

if (_physics.TryGetNearest(origin, other, out _, out _, out var distance, xfA, xfB, fixtureA, fixtureB))
{
    if (distance <= range)
        return true;
}
```

### Example 7: runtime switching between "flying/not flying" via status+sleep

```csharp
_physics.SetBodyStatus(target, targetPhysics, BodyStatus.InAir, false);
_physics.SetSleepingAllowed(target, targetPhysics, false);
_physics.WakeBody(target, body: targetPhysics);
```

## Patterns 🙂

- Think in terms of `BodyType + CanCollide + Fixtures`, not just one flag.
- After a significant change in fixture/collision parameters, execute `RegenerateContacts`.
- For complex geometry, use `TryGetNearest*` rather than distance between centers.
- For contact logic, use `GetContacts`/`GetContactingEntities`, rather than manually traversing other people's structures.
- Treat parent/container/anchor as physical operations, not just transform operations.
- On the client we manage prediction through `UpdateIsPredicted` flow, not through manually tugging fields.

## Anti-patterns

- Directly mutate the `PhysicsComponent`/`Fixture` fields in the game code.
- Expect that `SetBodyStatus` itself will change the solver behavior like `SetBodyType`.
- Change `layer/mask/hard` and forget about resynchronizing contacts.
- Use "center-to-center" distance for objects with complex fixtures.
- Keep the object in a container with collision enabled and non-zero speed.

## Mini-checklist before changes

- Does the change really have to go through the `SharedPhysicsSystem` API?
- Is `WakeBody` needed after the change?
- Is it necessary to make `RegenerateContacts` after editing fixture/collision?
- Are `BodyType` and `BodyStatus` confused?
- Doesn’t the behavior break in containers/when reparent/when the anchor-state changes?
- Is prediction-flow taken into account for the client case?
