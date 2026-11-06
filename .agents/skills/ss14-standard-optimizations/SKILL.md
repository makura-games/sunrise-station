---
name: ss14-standard-optimizations
description: "Practical skill in standard optimizations of Space Station 14: caching, reduction of allocations, abandonment of LINQ in hot-path, ActiveComponent approach, EntityQuery, order of components in EntityQueryEnumerator, ByRef events, DirtyField, early exits and cleaning of unnecessary components. Use it when designing, reviewing and optimizing ECS code in server/shared/client."
---

# SS14 Standard Optimizations

The goal of the skill: quickly select the correct optimization for ECS code without degradation of readability and without outdated techniques :)
The main reference: the current behavior of the code. Documentation is an auxiliary layer.

## When to use

1. Optimize `Update`, frequent event handlers, visual overlays, mass checks.
2. You do a code review and see frequent `TryComp/HasComp`, LINQ in the hot-path, and unnecessary state components.
3. Looking for the source of lags/GC spikes in server/shared/client.

## When not to use

1. The code is rarely executed and is not in the hot path.
2. Optimization complicates the code more than it provides benefits.
3. No measurable symptom (frame time, GC, network delta size).

## Reading order of resources

1. Open `references/optimization-patterns.md` - quick selection matrix.
2. Open `references/annotated-code-examples.md` - live examples with comments.
3. Open `references/docs-reconciliation.md` - comparison of docs and actual code.

## Source of truth and boundaries of trust

1. True - actual code of subsystems.
2. Use Docs as a context for intentions and terms.
3. If there is a conflict between docs and code, choose the code and explicitly record this in the reasoning.
4. Don't rely on old pieces without checking for freshness and without the context of the changes.

## Workflow optimization

1. Find the hot-path and record the symptom.
2. Choose the minimum technique from the cards below.
3. Compare before/after: number of iterations, allocations, network deltas, behavior.
4. Add a short explanation to your PR: “what was accelerated” and “why it’s safe.”

## Optimization cards

### 1) Caching invariants and aggregates before hot-loop ⏱️

**Pattern**
1. Compute expensive invariants before nested loops (shapes, set of angles, fast-path flags).
2. Maintain aggregates incrementally (for example, `TargetCount`, `BurstShotsCount`), rather than recalculating collections every tick.
3. Для частых проверок компонентов в `EntitySystem` дополнительно использовать dependency
   `EntityQuery<T>`.

**Anti-pattern**
1. Call an expensive computation inside the inner loop.
2. Each time, re-calculate “how much is already there/made”, going through the collections.
3. Receive query/accessor in every hot-path call.

**Code example**
```csharp
var itemShape = ItemSystem.GetItemShape(itemEnt); // One shape calculation before cycles.
var fastAngles = itemShape.Count == 1;
var angles = new ValueList<Angle>();

if (!fastAngles)
{
    for (var angle = startAngle; angle <= Angle.FromDegrees(360 - startAngle); angle += Math.PI / 2f)
        angles.Add(angle); // We precalculated the allowable angles once.
}
else
{
    angles.Add(startAngle);
    if (itemShape[0].Width != itemShape[0].Height)
        angles.Add(startAngle + Angle.FromDegrees(90));
}

// In the inner loop we use only prepared data.
foreach (var angle in angles)
{
    // Heavy placement/collision checking.
}

// Incremental aggregate instead of set recalculation:
if (gun.BurstActivated)
{
    gun.BurstShotsCount += shots;
    if (gun.BurstShotsCount >= gun.ShotsPerBurstModified)
    {
        gun.BurstActivated = false;
        gun.BurstShotsCount = 0;
    }
}
```

**Why is it faster**
1. The number of repeated calculations in a deep loop is greatly reduced.
2. The number of traversals of collections for the sake of one aggregate is reduced.
3. The hot path works on already prepared data.

**Limits of applicability**
1. The greatest effect is in algorithms with 2+ levels of cycles.
2. For rare code, choose simplicity over micro-optimization.

### 2) Reduced allocations ♻️

**Pattern**
1. Create working collections once as system/UI fields and clear them via `Clear()`.
2. Pass existing lists/spans to the method instead of creating new ones.
3. For temporary buffers in the hot-path, use a pool (`ArrayPool<T>`) and a guaranteed `Return`.

**Anti-pattern**
1. `new List<T>()` per frame/each call.
2. Copying large collections for one iteration.
3. Ignoring the pool for repeated short-lived buffers.

**Code example**
```csharp
private ValueList<EntityUid> _toRemove = new();

public override void Update(float frameTime)
{
    var query = AllEntityQuery<ColorFlashEffectComponent>();
    _toRemove.Clear(); // We will reuse without new allocation.

    while (query.MoveNext(out var uid, out _))
    {
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            continue;

        _toRemove.Add(uid);
    }

    foreach (var ent in _toRemove)
        RemComp<ColorFlashEffectComponent>(ent);
}
```

**Why is it faster**
1. The pressure on the GC is reduced.
2. Jitter disappears due to frequent short-lived objects.

**Limits of applicability**
1. Ensure collections are cleaned before reuse.
2. The pool should always have a symmetrical return.

### 3) Refusal of LINQ in hot-path 🚫

**Pattern**
1. In hot spots, use `for/foreach` and explicit early exits.
2. Leave LINQ for rare/offline operations where compactness is more important.

**Anti-pattern**
1. `Where/Select/Any/Count` in `Update` and frequent handlers.
2. LINQ chains for simple filters in loops.

**Code example**
```csharp
// ✅ Hot-path: a regular loop without unnecessary enumerators/closures.
for (var i = 0; i < entities.Count; i++)
{
    var uid = entities[i];
    if (!TryComp(uid, out MyComponent? comp))
        continue;

    Process(uid, comp);
}

// ❌ Don't do this in hot-path:
// entities.Where(HasComp<MyComponent>).Select(...).ToList();
```

**Why is it faster**
1. Fewer intermediate objects and delegates.
2. It is easier to control branches and early exits.

**Limits of applicability**
1. Don't turn your code into "micro-optimized noise."
2. If the area is not hot, readability may be more important.

### 4) Pattern Component + ActiveComponent 🔋

**Pattern**
1. The base component stores the configuration.
2. A separate `Active...Component` marks “currently active”.
3. `Update` passes only through active entities.

**Anti-pattern**
1. One huge list of all entities + `if (!IsActive) continue`.
2. Activity flags only inside the base component without a separate marker.

**Code example**
```csharp
// Activating the timer: added an active marker.
EnsureComp<ActiveTimerTriggerComponent>(uid);

// Deactivation: the marker was removed, the entity disappeared from the target query.
RemComp<ActiveTimerTriggerComponent>(uid);

// Iterate over active timers only.
var query = EntityQueryEnumerator<ActiveTimerTriggerComponent, TimerTriggerComponent>();
```

**Why is it faster**
1. The sample size is greatly reduced.
2. Fewer active/inactive checks within the loop.

**Limits of applicability**
1. We need discipline for state transitions (adding/removing a marker).
2. The logic must remain consistent during shutdown/remove.

### 5) EntityQuery для `TryComp/HasComp/Resolve` 🔎

**Pattern**
1. В `EntitySystem` получать часто используемый `EntityQuery<T>` через `[Dependency]`.
2. Использовать `_query.TryComp/_query.HasComp/_query.Comp` вместо частых общих вызовов.
3. Перед добавлением поля проверять partial-файлы системы на query того же типа.

**Anti-pattern**
1. Присваивать query через `GetEntityQuery<T>()` в `Initialize()` новой системы.
2. Добавлять `[Dependency] EntityQuery<T>` в команду, HTN-оператор, UI-контрол или оверлей.
3. Путать сохранённый `EntityQuery<T>` с перечисляющими API `EntityQueryEnumerator<T...>()`,
   `EntityQuery<T...>()` и `AllEntityQuery<T...>()`.

**Code example**
```csharp
[Dependency] private EntityQuery<TagComponent> _tagQuery = default!;

public bool HasTag(EntityUid uid, ProtoId<TagPrototype> tag)
{
    return _tagQuery.TryComp(uid, out var tagComp) &&
           tagComp.Tags.Contains(tag);
}
```

**Why is it faster**
1. A more direct path to component storage.
2. Fewer redundant operations during mass checks.

**Limits of applicability**
1. Выигрыш заметен при частых вызовах, а не в единичных местах.
2. Ленивая регистрация query существует только в системной dependency-коллекции. Вне
   `EntitySystem` использовать прямой доступ через `IEntityManager`, либо передавать query через
   конструктор вручную создаваемого объекта.

### 6) Component order in `EntityQueryEnumerator()` 📉

**Pattern**
1. Place the rarest component first.
2. The second is less rare, and so on.

**Anti-pattern**
1. Place the most massive component first and cross the huge set.
2. Choose the order “as it came to mind.”

**Code example**
```csharp
// ✅ Usually it’s better this way: Active* is less common.
var query = EntityQueryEnumerator<ActiveTimerTriggerComponent, TimerTriggerComponent>();

// ❌ Worse in most cases:
// var query = EntityQueryEnumerator<TimerTriggerComponent, ActiveTimerTriggerComponent>();
```

**Why is it faster**
1. Fewer candidates when constructing the intersection of components.
2. Lower cost of each `MoveNext`.

**Limits of applicability**
1. Assess the real cardinality in your subsystem.
2. Check after changes to gameplay data/prototypes.

### 7) `ByRef record struct` for events ⚡

**Pattern**
1. For frequent events, use `[ByRefEvent] public record struct ...`.
2. Pass such events through `ref`.

**Anti-pattern**
1. Use reference classes for high frequency local events unnecessarily.
2. Forget `ref` when processing/calling a by-ref event.

**Code example**
```csharp
[ByRefEvent] public record struct ChargedMachineActivatedEvent;

private void NotifyActivated(EntityUid uid)
{
    var ev = new ChargedMachineActivatedEvent();
    RaiseLocalEvent(uid, ref ev); // Transfer by reference.
}
```

**Why is it faster**
1. Less copying of event data.
2. Fewer unnecessary allocations in a frequent flow of events.

**Limits of applicability**
1. For rare events, the gain may be negligible.
2. Maintain a uniform signature style so as not to break the handler API.

### 8) `DirtyField` instead of `Dirty` for large network components 📡

**Pattern**
1. For components with the set `AutoNetworkedField` and field deltas enabled, only the changed fields are dirty.
2. Call `DirtyField(uid, comp, nameof(...))` for point changes.

**Anti-pattern**
1. Call `Dirty(uid, comp)` for every small update of a large component.
2. Transmit the full state when one field has changed.

**Code example**
```csharp
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ProximityDetectorComponent : Component
{
    [AutoNetworkedField] public TimeSpan NextUpdate;
    [AutoNetworkedField] public float Distance;
}

component.NextUpdate += component.UpdateCooldown;
DirtyField(uid, component, nameof(ProximityDetectorComponent.NextUpdate));
```

**Why is it faster**
1. Smaller size of network deltas.
2. Lower load on serialization and sending state.

**Limits of applicability**
1. If almost the entire state changes at once, the full `Dirty` may be normal.

### 9) Reducing busting and early exits 🚪

**Pattern**
1. Put cheap filters first.
2. Use early `return/continue`.
3. Stop working immediately if the context is invalid.

**Anti-pattern**
1. Calculate expensive things before simple checks.
2. Deep nesting instead of direct filters.

**Code example**
```csharp
while (query.MoveNext(out var uid, out var comp))
{
    if (comp.NextUpdate > curTime)
        continue; // Cheap filter first.

    if (!_toggle.IsActivated(uid))
        continue;

    UpdateTarget((uid, comp));
}
```

**Why is it faster**
1. Expensive is performed only for “reached” entities.
2. The average cost of one iteration is reduced.

**Limits of applicability**
1. Don't break readability: filters should be predictable.

### 10) Removing unnecessary components from an entity 🧹

**Pattern**
1. After the state is completed, remove temporary/active components.
2. Keep the entity minimal in terms of its current role.

**Anti-pattern**
1. Leave temporary components “just in case.”
2. Accumulate markers that are no longer involved in logic.

**Code example**
```csharp
if (timer.NextTrigger <= curTime)
{
    Trigger(uid, timer.User, timer.KeyOut);
    RemComp<ActiveTimerTriggerComponent>(uid); // We remove the excess component immediately.
}
```

**Why is it faster**
1. Fewer entities are included in future queries.
2. Less unnecessary checks and network load.

**Limits of applicability**
1. Removal should not break expected subscriptions/visual transitions.

## Checklist before PR

1. All changes are tied to a measurable hot-path.
2. No LINQ in hot loop without conscious rationale.
3. Query order is checked: from rare to massive.
4. For large network components, `DirtyField` is used where appropriate.
5. Temporary components are removed after the state is completed.
6. The text and comments are not tied to specific code paths.

## Rule for extending this skill

1. Add only architecture-wide optimizations that are repeated in several subsystems.
2. Narrow topics (for example, prediction-specific or atmos-specific) should be included in specialized skills.
3. Add any new pattern along with an anti-pattern and a code example.
