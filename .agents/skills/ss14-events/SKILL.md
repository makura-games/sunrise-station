---
name: ss14-events
description: A guide to using events in Space Station 14 - strict taxonomy, subscriptions, by-ref event prioritization, and networking patterns.
---

# 📨 SS14 Events Guide

Events are the primary way of communication between systems and entities in Space Station 14. 🚀 This guide covers how to properly define, raise, and handle events while following engine standards.

## 📝 Event Definition

### Local Events 🏠
For local events (within a single client or server), use a simple `struct` or `class` structure.
* **Structs**: Preferred for high frequency events (e.g. `MoveEvent`, `DamageEvent`) to avoid GC load. 🏎️
* **Classes**: Use for complex data or events that require inheritance (e.g. `ExamineEvent`). 📚
* **Naming**: The `Event` suffix is required (for example, `DoorOpenedEvent`).

```csharp
// Simple event structure
public readonly record struct DoorOpenedEvent(EntityUid User);

// Event class with output data
public sealed class ExamineEvent : EntityEventArgs {
    public readonly EntityUid Examined;
    public FormattedMessage Message = new();
}
```

### Network Events 🌐
Events transmitted over the network **MUST** inherit `EntityEventArgs` and be marked with `[Serializable, NetSerializable]` attributes.

```csharp
[Serializable, NetSerializable]
public sealed class RequestStationNameEvent : EntityEventArgs {
    public string NewName;
}
```

## 🔗 Subscribe to Events

Subscriptions are always processed in `EntitySystem.Initialize()`.

### 1. Directed Subscription (`SubscribeLocalEvent`) 🎯
Use when you want to listen to an event *on a specific entity* that has a specific component.

**Modern format:** Use the `Entity<T>` wrapper to access the component and UID at the same time.

```csharp
public override void Initialize() {
    base.Initialize();
    SubscribeLocalEvent<DoorComponent, DoorOpenedEvent>(OnDoorOpened);
}

private void OnDoorOpened(Entity<DoorComponent> ent, ref DoorOpenedEvent args) {
    // ent.Owner is the EntityUid
    // ent.Comp is a DoorComponent
    if (ent.Comp.IsOpen) ...
}
```

### 2. Broadcast Subscription (`SubscribeEvent`) 📢
Use for global events that are not tied to a specific entity.

```csharp
SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
```

### 3. Network Subscription (`SubscribeNetworkEvent`) 📡
Use to process events sent from the other side (Client -> Server or Server -> Client).

```csharp
SubscribeNetworkEvent<RequestStationNameEvent>(OnNameRequest);
```

## 🧩 Specific Patterns

### 1. Cancellable Events 🚫
Used to check whether an action can be performed ("Attempt" events). Any subscriber can cancel the action.

* **Classes**: Inherit from `CancellableEntityEventArgs`.
* **Structures**: Add the `public bool Cancelled;` field.
* **Important**: Always pass such events through `ref` so that changes to `Cancelled` are visible to the calling code.

**Usage:**
```csharp
// Definition
public sealed class DisarmAttemptEvent : CancellableEntityEventArgs { }
```

```csharp
// Subscription (Lock action)
private void OnDisarmAttempt(Entity<ScpRestrictionComponent> ent, ref DisarmAttemptEvent args) {
    if (!ent.Comp.CanBeDisarmed)
        args.Cancel(); // Or args.Cancelled = true;
}
```

```csharp
// Call (Permission check)
var attempt = new DisarmAttemptEvent();
RaiseLocalEvent(target, attempt);

if (attempt.Cancelled)
    return; // Action interrupted
```

### 2. Handled Events ✅
Used when an event must be processed by only one system (for example, interaction with an object). If one system has "handled" an event, the others do not need to execute their logic.

* **Implementation**: Add field `public bool Handled;` (or inherit `HandledEntityEventArgs` for classes).

**Usage:**
```csharp
// Definition
[ByRefEvent]
public struct InteractEvent {
    public bool Handled;
}
```

```csharp
// Subscription
private void OnInteract(Entity<MyComponent> ent, ref InteractEvent args) {
    if (args.Handled) return; // Already processed by someone

    // Executing the logic
    args.Handled = true; // Mark as processed
}
```
**Important**: The pattern of `Handled` is different from `Cancelled`. `Cancelled` asks for permission (“Is it possible?”), and `Handled` speaks of the fact of accomplishment (“I did it!”).

## ⚡ Performance: By-Ref Events

For high-load code, especially frequently triggered events (physics, motion), use **By-Ref** (reference) events. This avoids copying large structures.

### Definition of By-Ref Events
Mark the structure with the `[ByRefEvent]` attribute.

```csharp
[ByRefEvent]
public struct MoveEvent {
    public EntityCoordinates OldPosition;
    public EntityCoordinates NewPosition;
}
```

### Subscription By-Ref
You **MUST** use the `ref` keyword in the handler signature. ⚠️

```csharp
SubscribeLocalEvent<PhysicsComponent, MoveEvent>(OnMove);
```

```csharp
private void OnMove(Entity<PhysicsComponent> ent, ref MoveEvent args) {
    // args is passed by reference, changes are visible everywhere
}
```

## 📤 Calling Events

### Calling Local Events
Use `RaiseLocalEvent` from `EntitySystem`.

```csharp
// By Value
RaiseLocalEvent(uid, new DoorOpenedEvent(user));
```

```csharp
// By reference (By Ref) - automatically for those marked [ByRefEvent]
var moveEv = new MoveEvent(oldPos, newPos);
RaiseLocalEvent(uid, ref moveEv);
```

## ❌ Antipatterns and Frequent Errors

### 1. 🚫 Deprecated handler signature
**Error**: Use expanded signature `(EntityUid uid, Component comp, args)`.
**Why**: This is an outdated style. The new style with `Entity<T>` is cleaner and more convenient.
**Right**:
```csharp
// ✅ GOOD
private void OnEvent(Entity<MyComponent> ent, ref MyEvent args) { ... }
```

```csharp
// ❌ BAD
private void OnEvent(EntityUid uid, MyComponent component, MyEvent args) { ... }
```

### 2. 🚫 Subscribe to `OnMapInit` or `Startup`
**Error**: Subscribe to events inside component lifecycle methods.
**Why**: This causes memory leaks and duplicate subscriptions.
**Correct**: Always subscribe only to `Initialize()` of your `EntitySystem`.

### 3. 🚫 Using `CancellableEntityEventArgs` for Structs
**Error**: Trying to inherit structures from classes or using `CancellableEntityEventArgs` unnecessarily.
**Why**: This creates unnecessary allocations (boxing).
**Correct**: Add the `bool Handled` or `bool Cancelled` field directly to the structure and pass it through `ref`.

### 4. 🚫 Heavy logic in event constructors
**Bug**: Perform complex calculations in event constructor.
**Why**: Events are created frequently.
**Correct**: Transfer only ready data.

### 5. 🚫 Forgotten `sealed` for event classes
**Error**: Creating an event class without `sealed`.
**Why**: Prevents the JIT compiler from devirtualizing calls, reducing performance.
**Correct**: Always write `public sealed class MyEvent`.

### 6. 🚫 Changing `ref` arguments unnecessarily
**Bug**: Change fields in `ref` event if you are not the "responsible" system.
**Why**: This may break the logic of other systems that receive the modified event.
**Correct**: Change the data only if your system needs to intercept or modify the result (for example, armor reduces damage).

## Performance addition: `ByRef record struct`

For frequent local events, prefer this format:

```csharp
[ByRefEvent] public record struct ChargedMachineActivatedEvent;

private void RaiseActivated(EntityUid uid)
{
    var ev = new ChargedMachineActivatedEvent();
    RaiseLocalEvent(uid, ref ev); // Important: ref is required.
}
```

### Why is this useful?

1. Less copying of events in mass flows.
2. More stable behavior in hot-path compared to heavy event classes.

### Anti-pattern

```csharp
// ❌ Frequent event as a class + call without by-ref:
public sealed class FrequentEvent : EntityEventArgs { }
RaiseLocalEvent(uid, new FrequentEvent());
```

Use classes where it is really needed due to semantics, and not out of habit.
