# Optimization matrix: signal → decision → risk

Use the table as a quick solution router in reviews and refactorings.

| Topic | Signal | Solution | Risk due to incorrect use |
|---|---|---|---|
| Caching invariants and aggregates | Inside nested loops, the same calculations or quantity recalculation are repeated | Precalculate invariants before the loop and keep the counter incrementally (`TargetCount`, `BurstShotsCount`, `count += / -=`) | Inconsistent counter when update is missed |
| Reduced allocations | GC spikes on mass shots | Reuse `ValueList/List`, apply `ArrayPool`, transfer collections/spans | Leaks in the pool when `Return` is forgotten, dirty data without `Clear` |
| Abandoning LINQ | LINQ in hot-path (`Where/Select/Any/Count`) | Rewrite to `for/foreach` with early exits | Loss of readability if rewritten without structure |
| `Component + ActiveComponent` | Many "inactive" entities in the overall iteration | Enter active marker and iterate only active ones | Inconsistent state transitions when remove/add is forgotten |
| `EntityQuery` for `TryComp/HasComp/Resolve` | Частые повторные проверки компонента | В `EntitySystem` получить `EntityQuery<T>` через `[Dependency]` и использовать его методы | Инъекция query в несистемный класс через глобальную IoC или ложная оптимизация редкого вызова |
| Order in `EntityQueryEnumerator` | Dear multi-component query | Put the rare component first, then the more widespread ones | Incorrect ordering worsens iteration time |
| `ByRef record struct` events | Frequent local events in the gameplay loop | `[ByRefEvent] public record struct ...` + transfer `ref` | Handlers break due to inconsistent signature |
| `DirtyField` vs `Dirty` | Large component with many network fields | For point changes, use `DirtyField` | Skip the required field and get out of sync |
| Early `return/continue` | Complex nested checks in a loop | Cheap filters ahead, early release | It's difficult to follow the logic if the conditions are chaotic |
| Removing unnecessary components | Temporary components hang after task completion | Remove the component immediately after the state is completed | Break visual/event tails if removed too early |

## Hot-path criterion by topic

1. Caching: in hot-loop there is a repeated calculation of identical parameters or a repeated complete recalculation of the quantity.
2. Allocations: the site creates temporary objects on each frame/packet.
3. LINQ: the method is called frequently and contains chains of enumerations.
4. ActiveComponent: the proportion of actually active entities is small relative to the total.
5. EntityQuery: The same component check is repeated in API/loops.
6. Query order: `EntityQueryEnumerator<T1, T2[, T3]>` is used in the frequent path.
7. ByRef events: the event is raised en masse (combat, physics, periodic updates).
8. DirtyField: a network component and contains several `AutoNetworkedField`.
9. Early exits: Expensive work is done before the base filters.
10. Cleaning components: temporary/active markers continue to live after completion.

## Unusual but useful techniques

1. Double packet queues (swap queues) in the network subsystem: prevents the “packet begets a packet” loop in one tick.
2. Passing `ReadOnlySpan<T>` to distribution methods: removes unnecessary copies of recipient lists.
3. Local list cache per frame in UI/effects: more GC-stable than creating a new list every pass.
4. `fieldDeltas + DirtyField` combination: especially useful for components with frequent minor updates.
