---
name: ss14-ecs-components
description: Architecture guide for Component in Space Station 14 — data containers, attributes, networking, state-as-component pattern, and marker components
---

# Component - components in ECS

## Limit of responsibility

This skill covers component architecture, attributes, and data patterns.
Strict naming standards (suffix `Component`, linking with `System`, dependency aliases, file naming/prototype/localization rules) are maintained in `ss14-naming-conventions`.
If the local naming example differs from `ss14-naming-conventions`, use `ss14-naming-conventions`.

## What is Component

A component is a **pure data container** with no logic. Components are attached to Entities and define their properties. All logic for working with component data is located in the corresponding system (EntitySystem).

**The main rule: components do not contain methods with logic.** They only store data and configuration.

## Basic structure

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Speed = 5f;

    [DataField]
    public SoundSpecifier? ActivationSound;
}
```

## Required class attributes

### `[RegisterComponent]`

Registers a component in the engine. Required for all components. Without it, the component will not be available for use.

### `[NetworkedComponent]`

Indicates that the component is synchronized over the network between the server and client. Used in conjunction with `[AutoGenerateComponentState]`.

### `[AutoGenerateComponentState]` and `[AutoGenerateComponentState(true)]`

Automatically generates code to serialize/deserialize component state during network synchronization.

The `(true)` option additionally generates a `AfterAutoHandleState` method that is called after the state is applied - useful for performing side effects after a network update.

### `[AutoGenerateComponentPause]`

Generates code to automatically pause timer fields (`TimeSpan`) when the card is paused. Works in conjunction with `[AutoPausedField]`.

## Field attributes

### `[DataField]`

Marks a field for deserialization from YAML prototypes. The field name in YAML is the camelCase version of the field name in C#:

```csharp
[DataField]
public float BaseSpeed = 5f;  // → baseSpeed ​​in YAML

[DataField(required: true)]
public EntProtoId EntityId;   // Required, error if not specified
```

> **⚠️ Anti-pattern: string name in DataField (legacy)**
>
> Do not specify a string field name in `DataField`. This is an outdated approach. The name in YAML is **always** equal to the name in C# with a lower case letter:
>
> ```csharp
> // ❌ Legacy - DO NOT do this
> [DataField("counter")]
> public int Counter;
>
> [DataField("baseSpeed")]
> public float BaseSpeed;
>
> // ✅ Correct - the name is displayed automatically
> [DataField]
> public int Counter;        // → counter in YAML
>
> [DataField]
> public float BaseSpeed;    // → baseSpeed ​​in YAML
> ```

> **⚠️ Anti-pattern: DataField on runtime fields**
>
> `[DataField]` is needed **only** for fields that are specified via YAML prototypes. Fields that are generated in code during the game **should not** have `[DataField]`:
>
> ```csharp
> // ❌ Wrong - EntityUid is generated in code, not in YAML
> [DataField]
> public EntityUid? CurrentTarget;
>
> [DataField]
> public HashSet<EntityUid> ActiveTargets = [];
>
> [DataField]
> public TimeSpan? RageStartTime;  // Set by the system, not by prototype
>
> // ✅ Correct - without DataField, only AutoNetworkedField if synchronization is needed
> [AutoNetworkedField]
> public EntityUid? CurrentTarget;
>
> [AutoNetworkedField]
> public HashSet<EntityUid> ActiveTargets = [];
>
> [AutoNetworkedField]
> public TimeSpan? RageStartTime;
>
> // ✅ Correct - DataField is only for the configuration from the prototype
> [DataField]
> public float MaxSpeed ​​= 8f;  // Configured in YAML
>
> [DataField]
> public TimeSpan RageDuration = TimeSpan.FromMinutes(4);  // Configured in YAML
> ```

### `[AutoNetworkedField]`

Marks a field for automatic network synchronization. Only used in conjunction with `[AutoGenerateComponentState]` on the class:

```csharp
[DataField, AutoNetworkedField]
public int Counter;

[AutoNetworkedField]
public TimeSpan? StartTime;
```

### `[AutoPausedField]`

Marks a field of type `TimeSpan` to automatically pause when the card is paused. Used with `[AutoGenerateComponentPause]`:

```csharp
[AutoNetworkedField, AutoPausedField]
public TimeSpan? ActivationTime;
```

### `[ViewVariables]`

Makes the field visible in the debug View Variables (VV) panel:

```csharp
[ViewVariables] // By default, access = (VVAccess.ReadWrite), you do NOT need to register it again!
public float DebugValue;

[ViewVariables(VVAccess.ReadOnly)]
public int ReadOnlyValue;
```

### `[Access]`

Restricts access to the fields/properties of the component. Only the specified types can write to fields:

```csharp
[RegisterComponent, NetworkedComponent, Access(typeof(SharedMySystem))]
public sealed partial class MyComponent : Component
{
    // Only SharedMySystem and descendants can change fields
}
```

### `[NonSerialized]`

Excludes a field from serialization. Used for runtime data that does not need to be saved and transmitted:

```csharp
[NonSerialized]
public IPlayingAudioStream? SoundStream;

[NonSerialized]
public EntityUid? CurrentTarget;
```

## Component data types

### Basic types

```csharp
// Entity prototype ID
[DataField]
public EntProtoId? SpawnPrototype;

// Prototype ID of a specific type
[DataField]
public ProtoId<DamageModifierSetPrototype> DamageModifier;

// Sound
[DataField]
public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/path.ogg");

[DataField]
public SoundSpecifier Sound = new SoundCollectionSpecifier("CollectionName");

// Damage
[DataField]
public DamageSpecifier Damage = new();

// Entity filtering
[DataField]
public EntityWhitelist? Whitelist;
[DataField]
public EntityWhitelist? Blacklist;

// Time periods
[DataField]
public TimeSpan Duration = TimeSpan.FromSeconds(10);
```

### Collections

```csharp
[DataField]
public List<EntProtoId> Prototypes = [];

[DataField]
public HashSet<EntityUid> Targets = [];

[DataField]
public Dictionary<string, float> Values = new();
```

## State as component pattern

Instead of storing enum states inside a single component, each state is modeled as a **separate component**. Transition between states = adding/removing components.

### Main component (stores configuration):

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CreatureComponent : Component
{
    [DataField]
    public float BaseSpeed = 3f;

    [DataField]
    public TimeSpan RageDuration = TimeSpan.FromMinutes(2);
}
```

### State "Quiet" - no additional components

### "Heating" state:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveCreatureHeatingUpComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan HeatingDuration = TimeSpan.FromSeconds(30);

    [AutoNetworkedField]
    public TimeSpan? StartTime;
}
```

### Rage Condition:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveCreatureRageComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan RageDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public float Speed = 8f;

    [AutoNetworkedField]
    public TimeSpan? RageStartTime;
}
```

### Transitions between states in the system:

```csharp
// Going into rage
private void EnterRage(EntityUid uid, CreatureComponent comp)
{
    RemComp<ActiveCreatureHeatingUpComponent>(uid);  // exit from previous
    var rage = EnsureComp<ActiveCreatureRageComponent>(uid);  // entrance to the new
    rage.RageStartTime = _timing.CurTime;
    Dirty(uid, rage);
}

// Coming out of rage
private void ExitRage(EntityUid uid, CreatureComponent comp)
{
    RemComp<ActiveCreatureRageComponent>(uid);
}
```

**Advantages of the pattern:**
- Systems can subscribe to `ComponentStartup`/`ComponentShutdown` states
- `EntityQueryEnumerator` iterates only over entities in the desired state
- Network state synchronization occurs automatically
- Easily add new states without modifying the main component

## Marker components

Components without data fields used as "tags" for filtering:

```csharp
[RegisterComponent]
public sealed partial class ProtectedComponent : Component
{
    // No fields - just a marker
}
```

Systems check for the presence of a token:
```csharp
if (HasComp<ProtectedComponent>(uid))
    return; // Entity is protected, let's pass
```

## Component as target label

Components can be added to **other** entities to establish relationships:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TargetMarkerComponent : Component
{
    /// <summary>
    /// Link to haunting entity
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? Source;

    [DataField]
    public float RequiredDamage = 200f;

    [AutoNetworkedField]
    public float DamageApplied;
}
```

## Rules for writing components

1. **The class is always `sealed partial`** — `sealed` prevents inheritance, `partial` is needed for source generators
2. **Inherit from `Component`** - not from other components
3. **No logic** - only data fields. No methods, properties just for easy access
4. **XML documentation** - each public field must have a `/// <summary>` comment
5. **Reasonable defaults** - fields should have default values ​​so that the prototype can omit optional fields
6. **`[NonSerialized]` for runtime data** - audio streams, cached links to entities, temporary data
7. **Organizing Fields** - Use `#region` blocks to group related fields in large components
8. **`[DataField]` only for YAML configuration** - do not put on runtime fields (`EntityUid`, timestamps, caches)
9. **Do not specify a string name in `[DataField]`** - the name is derived automatically from the field name

## Optimization through Active components (add-on)

### Pattern: `BaseComponent + ActiveComponent`

Use the base component for configuration and a separate `Active...Component` for the current activity:

```csharp
[RegisterComponent]
public sealed partial class TimerTriggerComponent : Component
{
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(5);
    public TimeSpan NextTrigger = TimeSpan.Zero;
}

[RegisterComponent]
public sealed partial class ActiveTimerTriggerComponent : Component;
```

In the system:

```csharp
// Activation.
EnsureComp<ActiveTimerTriggerComponent>(uid);

// Shutdown—the component is removed.
RemComp<ActiveTimerTriggerComponent>(uid);
```

### Why is this important?

1. Only active entities are included in the query.
2. The number of empty iterations is reduced.
3. The state logic is simplified: the activity is read by the presence of a component.

### Anti-pattern

1. Keep the `IsActive` flag only in the base component and iterate through all of them.
2. Do not delete the active component after the state is complete.
