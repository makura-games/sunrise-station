---
name: ss14-ecs-entities
description: Working with entities in Space Station 14 — EntityUid, typed Entity wrappers, implicit conversion to EntityUid, component operations, containers, network identity, and entity lifecycle
---

# Entity - entities in ECS

## What is Entity

An Entity is a **unique identifier** (`EntityUid`) to which components are attached. The entity itself contains no data or logic - it is simply a numeric ID. Components define the properties of an entity, systems define its behavior.

## EntityUid

The main entity identifier is value type, compared by value:

```csharp
EntityUid uid = args.User;

// Validity check
if (!uid.IsValid())
    return;

// EntityUid.Invalid - invalid ID
if (uid == EntityUid.Invalid)
    return;
```

## Entity\<T\> - entity and component tuple

`Entity<T>` is the main way to simultaneously transfer `EntityUid` and a component:

```csharp
// Creation
Entity<MyComponent> ent = (uid, myComp);

// Access
EntityUid owner = ent.Owner;
MyComponent comp = ent.Comp;

// Deconstruction
var (entityUid, component) = ent;
```

### Entity\<T\> can be passed as EntityUid

`Entity<T>` has an implicit conversion to `EntityUid`. If an API accepts `EntityUid`, pass the
`Entity<T>` value directly instead of writing `.Owner`. Use `.Owner` only when you explicitly need
to store, compare, serialize, or pass around a standalone `EntityUid`.

```csharp
Entity<MyComponent> ent = (uid, comp);

// ✅ Preferred: no redundant Owner access.
Transform(ent);
Dirty(ent);
QueueDel(ent);
_audio.PlayPvs(sound, ent);

// ❌ Avoid this when the target API already accepts EntityUid.
Transform(ent.Owner);
Dirty(ent.Owner, ent.Comp);
QueueDel(ent.Owner);
_audio.PlayPvs(sound, ent.Owner);
```

When both entity and component are available, keep the tuple shape through the call chain. This
preserves component context and avoids noisy code that repeatedly unwraps and re-wraps the same
entity identifier.

###Entity\<T?\> - nullable option

Used when a component may be missing. Pattern `Resolve` - the system itself will receive the component:

```csharp
public void SetSpeed(Entity<MyComponent?> ent, float speed)
{
    // If ent.Comp == null, Resolve will try to get it
    if (!Resolve(ent, ref ent.Comp))
        return;

    ent.Comp.Speed = speed;
    Dirty(ent);
}

// Call - can be passed with or without a component:
SetSpeed((uid, myComp), 5f);    // with component
SetSpeed((uid, null), 5f);       // Resolve itself will receive
```

### AsNullable

Convert `Entity<T>` to `Entity<T?>`:

```csharp
Entity<MyComponent> ent = (uid, comp);
Entity<MyComponent?> nullable = ent.AsNullable();
```

## NetEntity - network identifier

`NetEntity` is the network version of `EntityUid`. Used when transmitting data over the network:

```csharp
// Convert EntityUid → NetEntity (for sending over the network)
NetEntity netEnt = GetNetEntity(uid);

// Convert NetEntity → EntityUid (when received from the network)
EntityUid uid = GetEntity(netEnt);

// Nullable options
EntityUid? uid = GetEntity(netEnt);
NetEntity? netEnt = GetNetEntity(uid);
```

## Creating and deleting entities

```csharp
// Prototyping
EntityUid newEntity = Spawn("PrototypeName", Transform(uid).Coordinates);

// Creation with position
EntityUid newEntity = Spawn("PrototypeName", new EntityCoordinates(mapUid, position));

// Removal (immediate)
Del(uid);

// Delete (deferred, safe in event handlers)
QueueDel(uid);
```

## Working with components on an entity

### Addition

```csharp
// Add a component (throws an exception if it already exists)
AddComp<MyComponent>(uid);

// Guaranteed to receive the component (will add if not)
var comp = EnsureComp<MyComponent>(uid);
```

### Receipt

```csharp
// Secure Receipt
if (TryComp<MyComponent>(uid, out var comp))
{
    // comp available
}

// Guaranteed receipt (throws exception if not)
var comp = Comp<MyComponent>(uid);
```

### Availability check

```csharp
if (HasComp<MyComponent>(uid))
{
    // There is a component
}
```

### Removal

```csharp
// Immediate removal
RemComp<MyComponent>(uid);

// Lazy deletion (safe in event handlers)
RemCompDeferred<MyComponent>(uid);
```

## EntityQueryEnumerator - iteration through entities

The most efficient way to find all entities with a specific set of components:

```csharp
// One component
var query = EntityQueryEnumerator<MyComponent>();
while (query.MoveNext(out var uid, out var comp))
{
    // Handling each entity with MyComponent
}

// Two components
var query = EntityQueryEnumerator<MyComponent, TransformComponent>();
while (query.MoveNext(out var uid, out var myComp, out var xform))
{
    // Only entities that have BOTH components
}

// Three components
var query = EntityQueryEnumerator<CompA, CompB, CompC>();
while (query.MoveNext(out var uid, out var a, out var b, out var c))
{
    // ...
}
```

## Built-in EntitySystem helpers

Each system inherits many convenience methods from `EntitySystem`:

```csharp
// Get TransformComponent
var xform = Transform(uid);

// Get MetaDataComponent
var meta = MetaData(uid);

// Get entity prototype
var prototypeName = Prototype(uid)?.ID;

// Debug line
var debugStr = ToPrettyString(uid);  // → "Scp096 (1234)"
```

## Container system

Entities can contain other entities through containers:

### Definition in component

```csharp
[RegisterComponent]
public sealed partial class MyContainerComponent : Component
{
    // Containers are created via ContainerContainer in a YAML prototype
}
```

### Definition in YAML

```yaml
- type: ContainerContainer
  containers:
    my_slot: !type:ContainerSlot
    storage: !type:Container
```

### Working in the system

```csharp
[Dependency] private SharedContainerSystem _container = default!;

// Get container
if (_container.TryGetContainer(uid, "my_slot", out var container))
{
    // Insert Entity
    _container.Insert(entityToInsert, container);

    // Extract
    _container.Remove(entityToRemove, container);
}
```

## MetaData - entity meta information

```csharp
var meta = MetaData(uid);

// Entity name
string name = meta.EntityName;

// Description
string desc = meta.EntityDescription;

// Flags
_meta.AddFlag(uid, MetaDataFlags.PvsPriority, meta);
_meta.RemoveFlag(uid, MetaDataFlags.PvsPriority, meta);
```

## Entity state checks

```csharp
// Does the entity exist?
if (Exists(uid))
{
    // ...
}

// Is the entity deleted?
if (Deleted(uid))
{
    return;
}

// Checking that the entity is still at the initialization stage
if (LifeStage(uid) < EntityLifeStage.MapInitialized)
{
    // ...
}

// Is it a client entity (does not have a server counterpart)
if (IsClientSide(uid))
{
    // ...
}
```

## Patterns for working with Entity

### Passing Entity\<T\> to system methods

```csharp
// Preferred Public API Format
public void DoSomething(Entity<MyComponent?> ent, float value)
{
    if (!Resolve(ent, ref ent.Comp))
        return;

    ent.Comp.Value = value;
    Dirty(ent);
}

// Internal method - when the component is guaranteed to exist
private void DoInternal(Entity<MyComponent> ent)
{
    ent.Comp.Value = 42;
    Dirty(ent);
}
```

### HashSet\<EntityUid\> to track

```csharp
// In component
[AutoNetworkedField]
public HashSet<EntityUid> TrackedEntities = new();

// In the system
comp.TrackedEntities.Add(targetUid);
comp.TrackedEntities.Remove(targetUid);
Dirty(uid, comp);
```

### Checking with Multiple Components - Early Return Pattern

**Do not pile conditions** into one `if`. Use early return when a component is missing:

```csharp
// ✅ That's right - early return
if (!TryComp<MyComponent>(uid, out var myComp))
    return;

if (!TryComp<TransformComponent>(uid, out var xform))
    return;

if (!HasComp<RequiredMarker>(uid))
    return;

// Working with an entity - all components are guaranteed to be present

// ❌ Incorrect - nested conditions
if (TryComp<MyComponent>(uid, out var myComp) &&
    TryComp<TransformComponent>(uid, out var xform) &&
    HasComp<RequiredMarker>(uid))
{
    // ...
}
```

## Optimizations for working with entities (addition)

### 1) Для частых проверок в системе использовать dependency `EntityQuery<T>`

Если API системы часто обращается к одному компоненту, получить query через системную
dependency-коллекцию:

```csharp
[Dependency] private EntityQuery<TagComponent> _tagQuery = default!;

public bool HasTagFast(EntityUid uid, ProtoId<TagPrototype> tag)
{
    return _tagQuery.TryComp(uid, out var comp) &&
           comp.Tags.Contains(tag);
}
```

Это правило относится к `EntitySystem` и его partial-файлам. В несистемных классах такая
dependency не разрешается: для единичных проверок использовать `IEntityManager`, а query для
вручную создаваемого объекта передавать через конструктор или получать от `IEntityManager`.

### 2) Remove unnecessary temporary components immediately after completing the role

```csharp
if (timer.NextTrigger <= curTime)
{
    Trigger(uid, timer.User, timer.KeyOut);
    RemComp<ActiveTimerTriggerComponent>(uid); // The entity is no longer active.
}
```

The idea is simple: the entity should only have components that are actually used.
Otherwise, it will continue to end up in the query and increase the cost of the search.
