---
name: ss14-virtual-controller-api
description: Gives a practical catalog of APIs around the VirtualController in Space Station 14: selection of hooks and order, physics mutators, relay/movement/climb scripts, prediction constraints and safe application patterns.
---

# VirtualController: API Practice

Use this skill when you need to quickly choose the correct method and avoid getting physics/prediction regression 😎
Freshness reference: `git log`/`blame` with cutoff `2024-02-20`.

## Quick API selection

1. You need a controller for the physics step: inherit from `VirtualController` and implement `UpdateBeforeSolve`/`UpdateAfterSolve`.
2. You need order between controllers: add `UpdatesBefore`/`UpdatesAfter` to `base.Initialize()`.
3. You need control over velocity/impulse: use `SetLinearVelocity`, `ApplyLinearImpulse`, `SetBodyStatus`, damping setters.
4. You need an entity management relay: use `SetRelay`/`RemoveRelay`.
5. You need manual mobility mathematics: `Friction`, `Accelerate`, `GetWishDir`/`SetWishDir`.
6. You need a container/movement link through “climbing out”: `ForciblySetClimbing`.
7. We need a safe climb pipeline: `CanVault` -> `TryClimb`.
8. We need predicted control on the client: `UpdateIsPredictedEvent` handlers.

## API by group

### 1) VirtualController contract

1. `Initialize()` - registers before/after solve hooks and metrics.
2. `UpdateBeforeSolve(bool prediction, float frameTime)` - pre-solver logic.
3. `UpdateAfterSolve(bool prediction, float frameTime)` - post-solver logic.
4. `BeforeMonitor` / `AfterMonitor` - measurement of the cost of a specific controller.

### 2) Ordering API

1. `UpdatesBefore.Add(typeof(...))` - this controller goes before the target.
2. `UpdatesAfter.Add(typeof(...))` - this controller comes later than the target.
3. Both lists must be configured to `base.Initialize()`.

### 3) Physics mutators

1. `SetLinearVelocity(...)` - safe setting of linear speed.
2. `ApplyLinearImpulse(...)` - impulse for dynamics/pulling.
3. `SetBodyStatus(..., BodyStatus.InAir/OnGround)` — body mode for special mechanics.
4. `SetLinearDamping(...)` / `SetAngularDamping(...)` — runtime damping.
5. `SetBodyType(...)` - conscious change of body mode.

### 4) Relay API

1. `SetRelay(uid, relayEntity)` — create/update a relay link.
2. `RemoveRelay(uid)` — remove the relay and bring the prediction state to a consistent state.
3. Client prediction hooks (`UpdateIsPredictedEvent`) must take into account the relay chain.

### 5) Movement helper API

1. `GetWishDir(...)` / `SetWishDir(...)` - control of the desired direction.
2. `Friction(...)` (vector/scalar) - controlled speed decay.
3. `Accelerate(...)` - limited acceleration to the wish direction.
4. `ResetCamera(...)` / `GetParentGridAngle(...)` - correct operation of the angle/orientation.

### 6) Climb API

1. `CanVault(...)` — climb/vault pre-validation.
2. `TryClimb(...)` — launching the climb procedure with do-after and checks.
3. `ForciblySetClimbing(...)` - forced safe exit to the surface.

### 7) Conveyor and Pull practical points

1. Conveyor: `CanRun(...)`, `UpdateBeforeSolve(...)`, combination of conveyor vector with `wishDir`.
2. Pull: impulse towards the target + settle shutdown + reverse impulse in special conditions.

## Unusual use cases

1. Relay piloting: mechs, station-eye, manned clothing, proxy-control objects in special modes.
2. Container-eject pipeline: after extracting the entity, use `ForciblySetClimbing` to avoid sticking.
3. Client prediction gating: for relay target and pullable via `UpdateIsPredictedEvent`.

## Patterns

1. Set up the controller order (`UpdatesBefore/After`) to `base.Initialize()`.
2. In `UpdateBeforeSolve` filter unsuitable bodies (`prediction`, `body.Predict`, static/sleeping cases).
3. For `KinematicController` use helper friction + explicit setting of speeds.
4. For relay scripts, use only `SetRelay`/`RemoveRelay`.
5. For gravity/space pull scenarios, take into account the pair impulse if required by the mechanics.
6. For climb-flow, always do `CanVault` before `TryClimb`.
7. For ejection from a container/cryo/scanner, use `ForciblySetClimbing` as a post-action step.
8. For conveyor logic, calculate the direction separately, apply the result after the parallel part.
9. For client prediction, adjust `IsPredicted/BlockPrediction` through the appropriate events.
10. Use `SetBodyStatus` only in clearly justified special mechanics.
11. For APIs that are called frequently, cache queries and avoid unnecessary allocations.
12. Check the actual freshness of the example before reusing it.

## Anti-patterns

1. Add order dependencies after `base.Initialize()`.
2. Pull `SetBodyStatus(InAir)` as a universal “patch”.
3. Change relay manually through direct `RemComp/EnsureComp` without lifecycle methods.
4. In the predicted controller, use nondeterministic logic without protections.
5. Copy TODO-heavy pieces from pull/contact/solver as “best practice”.
6. Rely on the empty client conveyor class as the source of behavior.
7. Use pre-cutoff legacy fragments as a reference.
8. Mix transform shifts and impulses without an explicit order model.
9. Skip the `CanVault` checks and immediately force the climb-flow.
10. Ignore update prediction events in relay/pullable scenarios.
11. Try to “treat” mispredict only with visual guards.
12. Do not check the consequences of an API change on server/shared/client at the same time.

## Code examples

### 1) Basic controller template

```csharp
public sealed class ExampleController : VirtualController
{
    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SomeOtherController));
        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        // There is only deterministic pre-solver logic here.
    }
}
```

### 2) Explicit ordering API

```csharp
public override void Initialize()
{
    UpdatesBefore.Add(typeof(TileFrictionController));
    UpdatesAfter.Add(typeof(SharedMoverController));
    // Important: we set the order before base.Initialize().
    base.Initialize();
}
```

### 3) Relay lifecycle via API, not manually

```csharp
// We connect the pilot control to the proxy entity.
_mover.SetRelay(pilotUid, proxyUid);

// We remove the relay correctly, updating the prediction state.
_mover.RemoveRelay(pilotUid);
```

### 4) Movement math helper: friction + accelerate

```csharp
var velocity = body.LinearVelocity;

// First attenuation, then acceleration towards the target direction.
_mover.Friction(minimumFrictionSpeed, frameTime, friction, ref velocity);
SharedMoverController.Accelerate(ref velocity, targetDir, accel, frameTime);

PhysicsSystem.SetLinearVelocity(uid, velocity, body: body);
```

### 5) Physics mutators in pull mechanics

```csharp
var impulse = accel * physics.Mass * frameTime;
PhysicsSystem.WakeBody(targetUid, body: physics);
PhysicsSystem.ApplyLinearImpulse(targetUid, impulse, body: physics);
```

### 6) Safe climb pipeline

```csharp
if (_climb.CanVault(climbable, user, target, out _) &&
    _climb.TryClimb(user, target, climbableUid, out var doAfter, climbable))
{
    // Climb started normally, doAfter is stored for continuation.
    component.DoAfterId = doAfter;
}
```

### 7) Container eject -> ForciblySetClimbing

```csharp
_container.Remove(containedUid, bodyContainer);

// After extraction, we transfer the entity to the correct “outside” state.
_climb.ForciblySetClimbing(containedUid, containerOwnerUid);
```

### 8) Prediction hook for pullable on the client

```csharp
private void OnUpdatePullablePredicted(Entity<PullableComponent> ent, ref UpdateIsPredictedEvent args)
{
    if (ent.Comp.Puller == _playerManager.LocalEntity)
        args.IsPredicted = true;
    else if (ent.Comp.Puller != null)
        args.BlockPrediction = true;
}
```

### 9) Conveyor + wish direction

```csharp
var targetDir = conveyorDir;
var wishDir = _mover.GetWishDir(entityUid);

if (Vector2.Dot(wishDir, targetDir) > 0f)
    targetDir += wishDir;

SharedMoverController.Accelerate(ref velocity, targetDir, 20f, frameTime);
```

## Rule of application

1. First select methods from `fresh-pattern-catalog.md`.
2. First check any legacy/TODO fragment with `rejected-snippets.md`.
3. If the script involves prediction, check server/shared/client behavior at the same time.
4. If there is a discrepancy between the docs and the code, always fix the decision in favor of the code.

Keep the API layer predictable: the right method and the right lifecycle are more important than a “short” call :)
