---
name: ss14-ecs-systems
description: Architecture guide for EntitySystem in Space Station 14 — lifecycle, events, queries, networking, prediction, and partial class decomposition patterns
---

# EntitySystem - systems in ECS

## Limit of responsibility

This skill covers systems design, lifecycle, events, query and prediction.
Strict naming standards (suffix `System`, name pairing `Component/System`, style of dependency aliases, file naming conventions) are maintained in `ss14-naming-conventions`.
If the example here conflicts with `ss14-naming-conventions`, use `ss14-naming-conventions`.

## What is EntitySystem

EntitySystem is a singleton class that contains **all the logic and behavior** for entities. In the ECS architecture, components store only data, and systems operate on this data. Systems are automatically created and managed by the engine - no need to manually register them.

## Basic lifecycle

```csharp
public sealed class MySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // Event subscriptions and EntityQuery dependencies
    }

    public override void Shutdown()
    {
        base.Shutdown();
        // Cleaning up resources (especially important on the client)
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // Logic executed every tick
    }
}
```

- `Initialize()` - called once when creating the system. Here we subscribe to events and cache requests.
- `Shutdown()` - called when the system is destroyed. On the server - when the program ends. On the client - when disconnecting from the server, so it is extremely important to clean up resources correctly on the client.
- `Update(float frameTime)` — called every tick. Used for periodic logic (timers, iteration over entities).

## Dependency Injection

Systems receive dependencies through the `[Dependency]` attribute. This works for both other systems and IoC managers:

```csharp
public sealed class MySystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
}
```

Dependencies are resolved automatically before `Initialize()` is called. Always use `= default!` to suppress compiler warnings.

## Order of system members (mandatory)

Keep a stable order of blocks in each `*System` so that the code is read quickly and equally in all subsystems :)

1. Dependencies (`[Dependency]` fields).
2. Constants + `static readonly` fields.
3. Runtime-cached fields and long-lived status fields.
4. `Initialize()`, `Shutdown()`.
5. Event handlers (`On...`, `Handle...`, network and component events).
6. Main logic (public/protected API of the system).
7. Other code (override/specialized methods, not helper block).
8. Helpers (small private methods for servicing logic).
9. Private nested classes / records / enums / other.

Don’t mix blocks with each other: don’t raise helpers above event handlers, don’t spread the runtime cache across the file, keep private nested types at the bottom.

Example:

```csharp
public sealed class ExampleSystem : EntitySystem
{
    // 1) Dependencies
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    // 2) Constants + static readonly
    private const float TimeoutSeconds = 1.0f;
    private static readonly ProtoId<TagPrototype> SpecialTag = "Special";

    // 3) Runtime cache/state
    private readonly Dictionary<EntityUid, TimeSpan> _cooldowns = new();

    // 4) Init/Shutdown
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyComponent, ComponentInit>(OnInit);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cooldowns.Clear();
    }

    // 5) Event handlers
    private void OnInit(Entity<MyComponent> ent, ref ComponentInit args)
    {
        _cooldowns[ent] = _timing.CurTime;
    }

    // 6) Main logic
    public bool TryActivate(EntityUid uid) => true;

    // 7) Other code
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    // 8) Helpers
    private bool IsCoolingDown(EntityUid uid) => _cooldowns.ContainsKey(uid);

    // 9) Private nested types
    private sealed record DebugEntry(EntityUid Uid, TimeSpan Time);
}
```

## Subscribe to events

### Directed Events

Tied to a specific entity. Called only if the entity has the specified component:

```csharp
public override void Initialize()
{
    SubscribeLocalEvent<MyComponent, InteractUsingEvent>(OnInteractUsing);
    SubscribeLocalEvent<MyComponent, ComponentStartup>(OnStartup);
    SubscribeLocalEvent<MyComponent, ComponentShutdown>(OnShutdown);
}

private void OnInteractUsing(Entity<MyComponent> ent, ref InteractUsingEvent args)
{
    // ent.Owner — EntityUid of the entity
    // ent.Comp — MyComponent
    // args — event data
}
```

### Broadcast Events

Not tied to a specific entity. Called for all subscribers:

```csharp
SubscribeLocalEvent<MyBroadcastEvent>(OnMyBroadcast);

private void OnMyBroadcast(MyBroadcastEvent args)
{
    // Event Handling
}
```

### Sorted Subscriptions

You can specify the order in which an event is processed between systems:

```csharp
SubscribeLocalEvent<MyComponent, SomeEvent>(OnEvent, before: [typeof(OtherSystem)], after: [typeof(AnotherSystem)]);
```

### Lifestage Events

The most common subscriptions are for creating and deleting components:

- `ComponentInit` — the component is initialized
- `ComponentStartup` — component is running
- `ComponentShutdown` — component is disabled
- `ComponentRemove` — the component is removed

```csharp
SubscribeLocalEvent<MyComponent, ComponentStartup>(OnStartup);
SubscribeLocalEvent<MyComponent, ComponentShutdown>(OnShutdown);
```

## Creating and calling events

### Directed Event

```csharp
var ev = new MyEvent(someData);
RaiseLocalEvent(uid, ref ev);
```

### Broadcast Event

```csharp
var ev = new MyBroadcastEvent();
RaiseLocalEvent(ev);
```

### Directed + Broadcast

```csharp
RaiseLocalEvent(uid, ref ev, broadcast: true);
```

## EntityQuery — эффективный доступ к компонентам

### Dependency в EntitySystem

`EntityQuery<T>` регистрируется лениво в `EntitySystemManager.SystemDependencyCollection`.
Внутри `EntitySystem`, включая `GameRuleSystem` и partial-файлы системы, объявлять его как
dependency и не инициализировать вручную в `Initialize()`:

```csharp
[Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
[Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
```

Перед добавлением query проверить другие partial-файлы: для одного типа компонента нужно
переиспользовать одно поле системы.

### Граница системной dependency-коллекции

Не добавлять `[Dependency] EntityQuery<T>` в `IConsoleCommand`, HTN-операторы, UI-контролы,
оверлеи и helper-классы. Глобальная IoC-коллекция не содержит эту регистрацию.

Для единичных проверок использовать методы `IEntityManager`. Если вручную создаваемому объекту
нужен часто используемый query, получить его через `IEntityManager.GetEntityQuery<T>()` в месте
создания или передать готовый query из системы через конструктор.

```csharp
public sealed class MyCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    private bool IsGrid(EntityUid uid)
    {
        return _entityManager.HasComponent<MapGridComponent>(uid);
    }
}
```

Здесь `[Dependency] EntityQuery<MapGridComponent>` был бы ошибкой: команда инъектируется не из
`SystemDependencyCollection`.

### Usage

```csharp
// Secure Receipt
if (_xformQuery.TryComp(uid, out var xform))
{
    // working with xform
}

// Guaranteed receipt (will throw an exception if not)
var xform = _xformQuery.Comp(uid);

// Availability check
if (_xformQuery.HasComp(uid))
{
    // ...
}
```

### EntityQueryEnumerator - iteration in Update

When you need to go through all entities with a certain set of components:

```csharp
public override void Update(float frameTime)
{
    var query = EntityQueryEnumerator<MyComponent, TransformComponent>();
    while (query.MoveNext(out var uid, out var myComp, out var xform))
    {
        // Logic for each entity
    }
}
```

You can iterate over one, two, or three components at a time.

## Prediction

Many systems run simultaneously on the client and server for smooth display. Important checks:

```csharp
// Execute the code only on the first prediction (not on repeated ones)
if (!_timing.IsFirstTimePredicted)
    return;

// Do not execute when applying server state
if (_timing.ApplyingState)
    return;

// Checking if an entity is a client entity
if (IsClientSide(uid))
    return;
```

## Server vs Client

```csharp
// Side check
if (_net.IsServer)
{
    // Server logic
}

if (_net.IsClient)
{
    // Client logic
}
```

## Dirty mechanism - network synchronization

When a component with `[AutoNetworkedField]` changes, you need to inform the engine about the need for synchronization:

```csharp
// Mark a component as "dirty" for sending over the network
Dirty(uid, component);

// Or via Entity<T>
Dirty(ent);
```

## Shared/Server/Client pattern

### Abstract Shared Class

The general logic is placed in `Content.Shared`:

```csharp
// Content.Shared
public abstract partial class SharedMySystem : EntitySystem
{
    // Common logic running on both server and client
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyComponent, SomeEvent>(OnSomeEvent);
    }

    // Virtual method to override
    protected virtual void OnSpecificAction(EntityUid uid, MyComponent comp)
    {
        // Basic implementation
    }
}
```

### Server implementation

```csharp
// Content.Server
public sealed partial class MySystem : SharedMySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // Server subscriptions: DB, PVS, spawn
        SubscribeLocalEvent<MyComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    protected override void OnSpecificAction(EntityUid uid, MyComponent comp)
    {
        base.OnSpecificAction(uid, comp);
        // Server logic: PVS overrides, spawn entities
    }
}
```

### Client implementation

```csharp
// Content.Client
public sealed partial class MySystem : SharedMySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // Client subscriptions: UI, visuals, sounds
    }
}
```

## Partial class decomposition

For complex systems, the logic is divided into several files via `partial class`. Each file is responsible for its own subsystem:

```text
SharedMySystem.cs            — Initialize, base subscriptions, DI
SharedMySystem.Actions.cs    — abilities and actions
SharedMySystem.Target.cs     — target management
SharedMySystem.State.cs      — state transitions
SharedMySystem.Appearance.cs — visuals and animations
```

Example structure:

```csharp
// SharedMySystem.cs
public abstract partial class SharedMySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    [Dependency] private EntityQuery<MyComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeActions();     // from Actions.cs
        InitializeTargets();     // from Target.cs
    }

    public override void Update(float frameTime)
    {
        UpdateState(frameTime);  // from State.cs
    }
}

// SharedMySystem.Actions.cs
public abstract partial class SharedMySystem
{
    private void InitializeActions()
    {
        SubscribeLocalEvent<MyComponent, MyActionEvent>(OnAction);
    }
}
```

## Pattern of generic systems

For generalized systems, a generic approach is used:

```csharp
// Basic handler for all effects of a certain type
public abstract class EntityEffectSystem<T, TEffect> : EntitySystem
    where T : unmanaged
    where TEffect : EntityEffectBase<TEffect>
{
    public override void Initialize()
    {
        SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(OnEffect);
    }

    protected abstract void OnEffect(Entity<T> ent, ref EntityEffectEvent<TEffect> args);
}
```

## Interfaces for systems

Systems can implement interfaces to standardize behavior:

```csharp
public interface IEntityEffectRaiser
{
    void RaiseEvent(EntityUid target, EntityEffect effect, float scale, EntityUid? user);
}

public sealed partial class MyEffectsSystem : EntitySystem, IEntityEffectRaiser
{
    public void RaiseEvent(EntityUid target, EntityEffect effect, float scale, EntityUid? user)
    {
        effect.RaiseEvent(target, this, scale, user);
    }
}
```

## Public methods of the system (API) and Interaction Flow (`OnEvent -> TryDo -> CanDo -> Do`)

Systems provide public methods for interaction between other systems. The `Entity<T?>` pattern with `Resolve` allows calling code to optionally pass a component — the system will resolve it automatically.

For actions and interactions, always separate responsibilities into three levels (`OnEvent -> TryDo -> CanDo -> Do`):

1. **Event Handler (`On[EventName]`)**: Entry point subscribed via `SubscribeLocalEvent`. Checks only event validity (`args.Handled`) and delegates to `Try...`.
2. **Action Attempt (`public bool TryAction(Entity<TComp?> ent, ...)`)**: Public API. Calls `CanAction` first; if `false`, returns `false`. Otherwise mutates state, plays predicted audio/popups, raises events, and returns `true`.
3. **Feasibility Check (`public bool CanAction(Entity<TComp?> ent, ..., bool quiet = false)`)**: Pure validation. **Never** mutates component state; may show failure popups only when `!quiet`. Use `out string? reason` if callers need the failure text, while keeping the return type `bool`.

```csharp
private void OnUseInHand(Entity<WieldableComponent> ent, ref UseInHandEvent args)
{
    if (args.Handled)
        return;

    if (TryWield(ent.AsNullable(), args.User))
        args.Handled = true;
}

public bool TryWield(Entity<WieldableComponent?> wieldable, EntityUid user)
{
    if (!Resolve(wieldable, ref wieldable.Comp, false))
        return false;

    if (!CanWield(wieldable, user))
        return false;

    SetWielded(wieldable, true);

    if (wieldable.Comp.WieldSound != null)
        _audio.PlayPredicted(wieldable.Comp.WieldSound, wieldable, user);

    var ev = new ItemWieldedEvent(user);
    RaiseLocalEvent(wieldable, ref ev);

    _popup.PopupPredicted(Loc.GetString("wieldable-component-successful-wield", ("item", wieldable.Owner)), user, user);
    return true;
}

public bool CanWield(Entity<WieldableComponent?> wieldable, EntityUid user, bool quiet = false)
{
    if (!Resolve(wieldable, ref wieldable.Comp, false))
        return false;

    if (!TryComp<HandsComponent>(user, out var hands))
    {
        if (!quiet)
            _popup.PopupEntity(Loc.GetString("wieldable-component-no-hands"), user, user);
        return false;
    }

    if (!_hands.IsHolding((user, hands), wieldable, out _))
    {
        if (!quiet)
            _popup.PopupEntity(Loc.GetString("wieldable-component-not-in-hands", ("item", wieldable.Owner)), user, user);
        return false;
    }

    return true;
}
```

**Anti-patterns to avoid:**
- **Fat event handler**: putting validation and state changes directly inside `OnEvent` (cannot be reused by verbs, commands, or other systems).
- **Side-effects in `CanDo`**: mutating component data (e.g., decrementing ammo) inside `Can...`.
- **Blind `TryDo`**: skipping `CanDo` inside `TryDo` and assuming the caller already validated preconditions.

## Logging

Logs should be **English only** and contain enough information to be parsed **after the round ends** (EntityUids will no longer be available, so be sure to include the prototype, name and other identifiers):

```csharp
// ✅ Correct - English, prototype, name, context
Log.Warning("Failed to apply effect on {Entity} (proto: {Proto})",
    ToPrettyString(uid), Prototype(uid)?.ID ?? "unknown");

Log.Error("Target {Target} (proto: {Proto}) is out of range for {Source} (proto: {SourceProto}), distance: {Distance}",
    ToPrettyString(targetUid), Prototype(targetUid)?.ID ?? "unknown",
    ToPrettyString(sourceUid), Prototype(sourceUid)?.ID ?? "unknown",
    distance);

Log.Debug("State transition: {Entity} (proto: {Proto}) entered rage, targets: {Count}",
    ToPrettyString(uid), Prototype(uid)?.ID ?? "unknown", targets.Count);

// ❌ Incorrect - Russian language, not enough context
Log.Debug("Debug message");
Log.Warning("Error: {Entity}", ToPrettyString(uid));  // no prototype, no context
```

## Event types

See `ss14-events` for full event patterns. Quick summary:

- **Cancellable**: inherit `CancellableEntityEventArgs` (or struct with `Cancelled`) and pass by `ref`.
- **Handled**: inherit `HandledEntityEventArgs` (`args.Handled = true`).
- **By-ref struct**: mark with `[ByRefEvent]` (`public record struct MyEvent(...)`) and subscribe with `ref`.
- **Naming**: always end with `Event` (`InteractUsingEvent`, `PickupAttemptEvent`, `MobStateChangedEvent`).

## Hot-path optimizations

For `Update()` loops, frequent event handlers, allocation reduction, `EntityQueryEnumerator` component ordering, and caching invariants, activate the `ss14-standard-optimizations` skill.
