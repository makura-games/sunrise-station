---
name: ss14-virtual-controller-core
description: Parses the VirtualController architecture in Space Station 14: the UpdateBeforeSolve/UpdateAfterSolve cycle, order through UpdatesBefore/UpdatesAfter, prediction semantics and connection with low-level physics Box2D (substeps, contacts, solver). Use when you need to deeply understand the system before expanding or debugging.
---

# VirtualController: Architecture and Cycle

Use this skill as an architectural playbook for VirtualController :)
Keep your focus on fresh code and check its relevance through `git log`/`blame` (cutoff: `2024-02-20`).

## Source of truth

1. The codebase is the primary source of truth.
2. Documentation - secondary layer (terms, intent, diagnostics).
3. Any site older than two years or with a TODO/problematic comment on the topic should not be included in the reference rules.

## VirtualController mental model

1. `VirtualController` is `EntitySystem` which subscribes to `PhysicsUpdateBeforeSolveEvent` and `PhysicsUpdateAfterSolveEvent`.
2. The order of calling the controller is specified via `UpdatesBefore` / `UpdatesAfter`.
3. Hooks `UpdateBeforeSolve` and `UpdateAfterSolve` are called on each physics substep.
4. The `prediction` parameter tells whether predict simulation is running on the client.
5. `frameTime` — time of one substep, not the entire tick.
6. The performance of controllers is monitored separately through built-in histograms (`BeforeMonitor` / `AfterMonitor`).

## Layer scheme

1. Engine layer:
`VirtualController`, `SharedPhysicsSystem`, before/after solve events, solver/contact pipeline, special-casing `KinematicController`.
2. Shared layer:
`SharedMoverController`, `TileFrictionController`, `SharedConveyorController`, `ClimbSystem`.
3. Server layer:
`PullController`, `RandomWalkController`, `ChasingWalkSystem`, `ChaoticJumpSystem`, `MoverController`, `ConveyorController`.
4. Client layer:
`MoverController`, `ConveyorController`-stub for prediction/network presence, prediction hooks via `UpdateIsPredictedEvent`.

## Patterns

1. Add `UpdatesBefore` / `UpdatesAfter` before `base.Initialize()` - otherwise the order hook will be fixed incorrectly.
2. In `UpdateBeforeSolve` do early `continue` by `prediction`/`body.Predict` so as not to break the predict.
3. Use `AwakeBodies` as the main set for per-step physical logic.
4. For `KinematicController`, use manual damping via helper movement methods and then `SetLinearVelocity`/`SetAngularVelocity`.
5. For heavy calculations inside the controller, separate the calculation phase (parallel job) from the phase of applying the results (main thread).
6. For relay management, use the `SetRelay`/`RemoveRelay` API methods, rather than manually adding/removing components.
7. For container-eject scenarios, where the entity must immediately appear “outside”, use `ForciblySetClimbing` as a safe post-eject transition.
8. Keep `UpdateBeforeSolve` deterministic: minimum hidden state, minimum nondeterministic sources.
9. To control regressions, check the physics monitors of the controllers before/after the change.
10. For movement systems, maintain an invariant: mob movement and tile friction must occur in a consistent order.

## Anti-patterns

1. Configure `UpdatesBefore/UpdatesAfter` after `base.Initialize()`.
2. Copy TODO-heavy sections as an architectural standard.
3. Write a controller that depends on “one call per tick” and ignores substeps.
4. Perform expensive lookup/alloc operations without cache-query in a hot physics loop.
5. Bypass the relay API using direct `RemComp/EnsureComp` in the gameplay code.
6. Consider the empty client `ConveyorController` “superfluous” and remove it from the predictor scheme.
7. Try to use old pre-cutoff fragments as a basis for new solutions.
8. Treat mispredict with “magic guards” instead of correcting the source of desync.
9. Interfere with transform movement and physical impulses without a clear cause-and-effect model.
10. Ignore warning comments about fragile logic (`slop`, `hack`, `temporary`).

## Low-level Box2D/Physics Internals

1. The simulation goes through `SimulateWorld`: each substep raises before/after events, then broadphase/contact/solve.
2. Controllers are executed before the solver (`UpdateBeforeSolve`) and after the solver (`UpdateAfterSolve`) on each substep.
3. `AwakeBodies` - a key runtime set of bodies participating in the active simulation and controllers.
4. Generation and recalculation of contacts are based on broadphase overlap + narrowphase manifold update.
5. For `KinematicController` there are separate branches in solver/contact logic, so it cannot be treated as a regular `Dynamic`.
6. Any logic that changes speeds/pulses in controllers must take into account prediction and body mode.

## Code examples

### 1) Subscribe VirtualController to before/after solve

```csharp
public override void Initialize()
{
    base.Initialize();

    var updatesBefore = UpdatesBefore.ToArray();
    var updatesAfter = UpdatesAfter.ToArray();

    // The controller receives both physics hooks in one order graph.
    SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve, updatesBefore, updatesAfter);
    SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve, updatesBefore, updatesAfter);
}
```

### 2) Physics substeps with calling controllers

```csharp
for (int i = 0; i < _substeps; i++)
{
    var before = new PhysicsUpdateBeforeSolveEvent(prediction, frameTime);
    RaiseLocalEvent(ref before);

    _broadphase.FindNewContacts();
    CollideContacts();
    Step(frameTime, prediction);

    var after = new PhysicsUpdateAfterSolveEvent(prediction, frameTime);
    RaiseLocalEvent(ref after);
}
```

### 3) Explicit order between mover/friction/conveyor

```csharp
public override void Initialize()
{
    UpdatesBefore.Add(typeof(TileFrictionController));
    base.Initialize();
}

public override void Initialize()
{
    UpdatesAfter.Add(typeof(SharedMoverController));
    base.Initialize();
}
```

### 4) Manual damping for KinematicController

```csharp
if (body.BodyType == BodyType.KinematicController)
{
    var velocity = body.LinearVelocity;
    var angVelocity = body.AngularVelocity;

    // For this type of body, the friction/solver path is different, so we dampen it manually.
    _mover.Friction(0f, frameTime, friction, ref velocity);
    _mover.Friction(0f, frameTime, friction, ref angVelocity);

    PhysicsSystem.SetLinearVelocity(uid, velocity, body: body);
    PhysicsSystem.SetAngularVelocity(uid, angVelocity, body: body);
}
```

### 5) Pull controller with reverse pulse in special mode

```csharp
var impulse = accel * physics.Mass * frameTime;
PhysicsSystem.ApplyLinearImpulse(pullableEnt, impulse, body: physics);

// In weightless/blocked scenarios we add a pair impulse so as not to lose physical balance.
if (_gravity.IsWeightless(puller) && pullerXform.Comp.GridUid == null || !_actionBlockerSystem.CanMove(puller))
{
    PhysicsSystem.WakeBody(puller);
    PhysicsSystem.ApplyLinearImpulse(puller, -impulse);
}
```

### 6) Chasing controller forcibly keeps the body “in the air”

```csharp
var delta = targetPos - selfPos;
var speed = delta.Length() > 0 ? delta.Normalized() * component.Speed : Vector2.Zero;

_physics.SetLinearVelocity(uid, speed);
// Special mode for entity behavior (for example, tesla-like objects).
_physics.SetBodyStatus(uid, physics, BodyStatus.InAir);
```

### 7) Chaotic jump with raycast-safe offset

```csharp
var ray = new CollisionRay(startPos, direction.ToVec(), component.CollisionMask);
var hit = _physics.IntersectRay(mapId, ray, range, uid, returnOnFirstHit: false).FirstOrNull();

if (hit != null)
{
    // Shift away from the collision point to avoid teleporting directly into the collision.
    targetPos = hit.Value.HitPos - new Vector2((float)Math.Cos(direction), (float)Math.Sin(direction));
}

_transform.SetWorldPosition(uid, targetPos);
```

## Extension rule

1. Design new controllers as deterministic substep passes with explicit prediction-gating.
2. Fix the order of controllers in `Initialize()` to `base.Initialize()`.
3. First check any borrowed fragment with `rejected-snippets`.
4. Confirm any performance change by profiling controller histograms.

Think of VirtualController as a physics orchestration layer, not as “another Update()” 😅
