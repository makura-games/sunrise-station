---
name: SS14 Prediction
description: Architecture guide for client-side prediction in Space Station 14 — prediction loop, timing properties, predicted entities, state reconciliation, randomness, and common pitfalls
---

# Prediction (Client-Side Prediction) in SS14

## Why do we need prediction?

In an online game, there is time (RTT) between pressing a button and the server responding. Without prediction, the player would press “move” and see the result 50–200 ms later. Prediction solves this: the client **immediately** applies the action locally, and then adjusts the result when the server state arrives.

### Price of prediction

- The code must be in a **Shared** project (common for client and server)
- Need special processing of side effects (sounds, pop-ups)
- Randomness requires a deterministic generator
- Reference types in components must implement `IRobustCloneable`
- Possible “mispredicts” - moments when the client predicted incorrectly

## Main prediction loop

Each frame the client executes the following sequence:

```
┌──────────────────────────────────────────────────────┐
│           ClientGameStateManager.ApplyGameState()     │
│                                                      │
│  1. ResetPredictedEntities()                         │
│ └─ Roll back all predicted changes │
│ to the last confirmed server │
│ condition │
│                                                      │
│  2. ApplyGameState(curState, nextState)               │
│ └─ Apply new server state │
│ └─ Create new entities │
│ └─ Process PVS inputs/outputs │
│ └─ Delete marked entities │
│                                                      │
│  3. MergeImplicitData()                              │
│ └─ Create “fake” initial states │
│ for new entities (from prototypes) │
│                                                      │
│  4. PredictTicks(predictionTarget)                   │
│ └─ Play all candidate ticks again │
│ └─ Apply pending input and events │
│ └─ Run EntitySystemManager.TickUpdate() │
│                                                      │
│ 5. TickUpdate (main tick of the current frame) │
│ └─ Final tick = predictionTarget │
└──────────────────────────────────────────────────────┘
```

### Calculation of predictionTarget

```
predictionTarget = LastProcessedTick + TargetBufferSize + lag_ticks + PredictTickBias
```

Where is `lag_ticks = ceil(TickRate × ping / TimeScale)`. The client predicts so many ticks ahead so that its input has time to reach the server and return.

## ResetPredictedEntities: rollback predictions

Before the new server state is applied, **all predicted changes are rolled back**. This happens via `ClientDirtySystem`:

### How changes are tracked

- `ClientDirtySystem` subscribes to the `EntityDirtied` event
- When a component of a **server** entity (not a client) changes during prediction (`InPrediction = true`), it is added to `DirtyEntities`
- Deleted components are written to `RemovedComponents`

### Rollback includes

1. **Deleting predicted entities** - all entities with `PredictedSpawnComponent` are deleted and recreated when running again
2. **Restoring component states** - for each “dirty” entity, components are reset to the last server state via `ComponentHandleState`
3. **Removing added components** - components added during prediction (with `CreationTick > LastRealTick`) are deleted
4. **Recovery of deleted components** - components deleted during prediction are recreated with the server state
5. **Reset physics contacts** - `PhysicsSystem.ResetContacts()`

## IGameTiming properties for prediction

### IsFirstTimePredicted

Returns `true` if the current tick is predicted **for the first time**. For repeated runs (re-prediction), it returns `false`.

**Critical for side effects:**

```csharp
// ✅ Correct - the sound is played once
if (_timing.IsFirstTimePredicted)
    _audio.PlayPvs(sound, uid);

// ✅ It’s more correct to use Predicted methods
_audio.PlayPredicted(sound, source, receiver);
// source - sound source
// receiver - the “client” entity that made the sound.
// By default, the player's client that triggered the sound will duplicate the sound (locally and from the server). To avoid this, we pass the player’s sound that was projected to the method
// Because of this feature, sometimes the Predicted method may not work correctly!

// ❌ Incorrect - the sound will be played with every re-prediction!
_audio.PlayPvs(sound, uid);
```

### InPrediction

`true` when `CurTick > LastRealTick` **and** `ApplyingState = false`. Indicates that the client is currently making a prediction.

### ApplyingState

`true` when the client applies the server state (between `ResetPredictedEntities` and the beginning of `PredictTicks`). At this point, you cannot create side effects.

### CurTick vs LastRealTick vs LastProcessedTick

- **CurTick** — current simulation tick. Changes with prediction
- **LastRealTick** — last tick confirmed by the server
- **LastProcessedTick** — the last tick for which the server state was applied

## Foretold entities

### Spawn

Use special methods to create entities on the client:

```csharp
// Creating an Entity with Prediction
var entity = PredictedSpawn(prototype, coordinates);
```

When applying server state:
1. All entities with `PredictedSpawnComponent` are deleted
2. If the server has confirmed the creation, the entity will appear from the server state
3. The next time the prediction is run, the entity will be created anew

### Predicted removal

Deleting server-side entities on the client is **not directly supported**. `ClientDirtySystem` will throw an error if it detects that a server entity has been deleted during prediction:

```
// This will throw an error:
// "Predicting the deletion of a networked entity: ..."
QueueDel(serverEntity); // ❌ cannot be predicted!
```

Instead, use the state as component pattern - add/remove components to change the state of the entity.

## Predicted sounds and popups

### Sounds

```csharp
// ✅ Plays once: on the client for the first predictor, on the server for the rest
_audio.PlayPredicted(sound, uid, user);

// ✅ Alternative with manual control
if (_timing.IsFirstTimePredicted)
    _audio.PlayPvs(sound, uid);
```

### Popups

```csharp
// ✅ Popup API самостоятельно обрабатывает предикт и подавление дублей.
_popup.PopupEntity(message, uid, PopupType.Medium);

// ✅ Вариант только для локального игрока.
_popup.PopupEntity(message, uid, user);
```

Use `PopupEntity`, `PopupCoordinates`, or `PopupCursor` directly. The old
`PopupPredicted*` and `PopupClient` wrappers are obsolete. When replacing them,
preserve their filtering semantics: a `PopupClient` recipient is a real audience
filter, while the recipient argument of the old `PopupPredicted` overloads was
only a prediction key and should not become a recipient filter.

## Predicted randomness

Regular `IRobustRandom` is **not deterministic** between prediction runs. Each time it re-predicts, it will give different numbers, which will cause a misprediction.

### Solutions

#### Project Sunrise: RandomPredictedSystem

The Sunrise project uses its own predictable random system `RandomPredictedSystem` linked to `EntityUid`:

```csharp
// ✅ Seed is tied to EntityUid + current tick
var random = _randomPredicted.NextForEntity(uid, 0, 100);

// ✅ Random float
var value = _randomPredicted.NextFloatForEntity(uid, 0f, 1f);

// ✅ Probability check
if (_randomPredicted.ProbForEntity(uid, 0.3f))
    // 30% chance

// ✅ Random element from the list
var item = _randomPredicted.PickForEntity(uid, myList);
```

The system creates `System.Random` with a seed based on `EntityUid` and the current `CurTick`, so repeated prediction runs for the same tick will produce the same result.

#### Vanilla approach: manual creation of Random

```csharp
// ✅ Deterministic seed
var random = new System.Random((int)(uid.Id + _timing.CurTick.Value));
var result = random.Next(0, 100);
```

#### What NOT to do

```csharp
// ❌ IRobustRandom - different results for each prediction run
var result = _random.Next(0, 100);

// ❌ RobustRandom.NextFloat() - also non-deterministic
```

## Shared code for prediction

### Why Shared

Predicted systems and components **must** be in `Content.Shared` (or `Robust.Shared`). This is because:

1. The prediction code is executed **on both the client and the server**
2. The client and server should get the **same result** given the same input
3. If the code is only on the server, the client will not be able to predict it

### Partial class decomposition

When server or client specialization is needed:

```csharp
// Content.Shared/MySystem.cs
public abstract partial class SharedMySystem : EntitySystem
{
    // General prediction logic
    protected void HandleAction(EntityUid uid, MyComponent comp)
    {
        comp.Value += 1;
        Dirty(uid, comp);
    }
}

// Content.Server/MySystem.cs
public sealed partial class MySystem : SharedMySystem
{
    // Server validation, logging, authoritative actions
}

// Content.Client/MySystem.cs
public sealed partial class MySystem : SharedMySystem
{
    // Client effects, UI
}
```

## Processing states during prediction

### AutoGenerateComponentState

The `[AutoGenerateComponentState]` attribute generates code for:
- Serialization of the component in `IComponentState` (for sending over the network)
- Deserialization from `ComponentHandleState` (to apply server state)
- Automatic rollback at `ResetPredictedEntities`

### How rollback works

1. Component is marked as "dirty" during prediction
2. At `ResetPredictedEntities` the engine finds the last server state for this component
3. Generates `ComponentHandleState` and raises it as an event
4. The auto-generated handler restores all `[AutoNetworkedField]` fields
5. `LastModifiedTick` is reset to `LastRealTick`

### NetSync

Fields with `[AutoNetworkedField]` can be further configured via `[NetSync]` to control the direction of synchronization.

## Prediction checklist

When adding prediction to the system:

1. **Component in Shared** - with `[NetworkedComponent]`, `[AutoGenerateComponentState]`, `[AutoNetworkedField]` on the required fields
2. **System in Shared** - base class `SharedMySystem` with prediction logic
3. **`Dirty()` after changes** - each change to a component must be accompanied by `Dirty(uid, comp)`
4. **Side effects for `IsFirstTimePredicted`** - sounds, popups, visual effects
5. **Deterministic random** - `RandomPredictedSystem` or seed-based `System.Random`
6. **`IRobustCloneable` for reference types** - collections, classes in network fields
7. **No deleting server-side entities** - use state components instead of deleting
8. **Testing with delay** - `net.fakelagmin 0.2` + `net.fakelagrand 0.1`
9. **Testing with prediction disabled** - `net.predict false`

## Frequent errors

### 1. Forgot `Dirty()`

```csharp
// ❌ The client will not receive the update
comp.Health -= damage;

// ✅ Correct
comp.Health -= damage;
Dirty(uid, comp);
```

### 2. IRobustRandom in prediction

```csharp
// ❌ Different results with each re-prediction
if (_random.Prob(0.5f))
    DoAction();

// ✅ Deterministic random
if (_randomPredicted.ProbForEntity(uid, 0.5f))
    DoAction();
```

### 3. Obsolete popup prediction wrappers

```csharp
// ❌ Устаревшие обёртки
_popup.PopupPredicted("Hit!", uid, user);
_popup.PopupClient("Hit!", uid, user);

// ✅ Текущий popup API самостоятельно предсказывает и удаляет дубли
_popup.PopupEntity("Hit!", uid);
_popup.PopupEntity("Hit!", uid, user);
```

### 4. [NetworkedComponent] on a non-Shared component

```csharp
// ❌ In Content.Client - silently does not work
[RegisterComponent, NetworkedComponent]
public sealed partial class MyClientComponent : Component { }

// ✅ In Content.Shared - works correctly
[RegisterComponent, NetworkedComponent]
public sealed partial class MyComponent : Component { }
```

### 5. Deleting a server entity in prediction

```csharp
// ❌ Will cause an error during reconciliation
if (_timing.InPrediction)
    QueueDel(targetUid);

// ✅ Use a state component
RemComp<AliveComponent>(targetUid);
```

### 6. Changing non-network data in prediction

```csharp
// ❌ A field without [AutoNetworkedField] will not be rolled back
comp.LocalCounter += 1; // Not network, will not roll back!

// ✅ All predicted fields must be network
[AutoNetworkedField]
public int Counter;
```

## Prediction testing

### Basic CVars

```
// In the client console
net.predict false          // Disable prediction - the real delay is visible
net.predict true           // Turn back on

// Latency simulation
net.fakelagmin 0.2         // 200ms minimum latency
net.fakelagrand 0.05       // +0-50ms random delay

// Packet Loss Simulation
net.fakeloss 0.05          // 5% packet loss
```

### What to check

1. With **prediction turned off** (`net.predict false`) - is the server behavior correct?
2. With **delay** - are there any visual jumps (snap-backs) during a misprediction?
3. With **packet loss** - does synchronization break?
4. Several **quick actions in a row** - is the prediction queue processed correctly?

## Connection with other skills

- **SS14 Netcode Architecture** - how the network stack that provides prediction works
- **SS14 ECS Components** - attributes `[AutoGenerateComponentState]`, `[AutoNetworkedField]`, `Dirty()`
- **SS14 ECS Systems** — partial class decomposition for Shared/Client/Server systems
- **SS14 ECS Entities** — `EntityUid` vs `NetEntity`, entity life cycle
