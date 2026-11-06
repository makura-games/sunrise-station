# Reconciliation of docs and actual code

The docs are useful for intent and terms, but some of the language in them is broader and older than current practices.  
The rule of this skill is: **if there is a conflict, the current code wins**.

## Summary table for 10 topics

| Topic | What docs usually capture | What does the code confirm | Rule for skill |
|---|---|---|---|
| Caching in hot-path | Tips for eliminating duplicate data access | In real code, invariants are precomputed before nested loops and aggregates are maintained using counters; query is also cached | First, remove repeated calculations/recalculations, then cache access to components |
| Reduced allocations | Recommendations for reusing memory | Uses `ValueList` as fields and `ArrayPool` for temporary buffers | Reuse collections, don't create them for every frame |
| Abandoning LINQ in hot-path | Indirectly through perf guides and examples | Critical loops are mostly written via `for/foreach` | In hot LINQ code, replace with explicit loops |
| `Component + ActiveComponent` | ECS State through Components Architecture | Active markers are actually used to narrow query | Use Active Components for Limited Sampling |
| `EntityQuery` for `TryComp/HasComp/Resolve` | Query API Recommendations | `EntitySystem` получает `EntityQuery<T>` из системной dependency-коллекции; глобальная IoC его не регистрирует | Для частых проверок в системе использовать dependency-query, вне системы выбирать `IEntityManager` или передачу через конструктор |
| Component order in `EntityQueryEnumerator` | Not always clearly stated in docs | In runtime there is a direct comment about the rare first component | Put the rarest component first |
| `ByRef record struct` events | There is guidance about by-ref events | In server/shared there are `[ByRefEvent] record struct` + `RaiseLocalEvent(..., ref ...)` | For frequent local events, use the by-ref structure |
| `DirtyField` vs `Dirty` | There are recommendations about field deltas | In network components with multiple fields, use `DirtyField` | When changing a field selectively, select `DirtyField` |
| Early `return/continue` | General tips about cheap filters | In hot-loop there are often early filters and `continue` | Sort checks from cheap to expensive |
| Removing unnecessary components | ECS-idea “data only on the fact of state” | After the activity is completed, temporary components are deleted | Remove temporary components immediately after state completion |

## Where docs can fall behind

1. Docs may describe the general approach, but not reflect the current “de facto” optimizations of specific subsystems.
2. The docs rarely indicate the actual cardinality of components for choosing the query order.
3. The documentation does not always show the latest local optimizations of the UI/client layers.

## How to deal with discrepancies

1. Check the freshness of the code using the blame/history.
2. If the code is fresh and stable, use it as a reference.
3. If the code is old, but it is a canonical engine pattern, mark it as a soft-exception and be sure to explain why it is still relevant.
4. If the code contains TODO/FIXME/HACK on the topic of optimization, do not take such a fragment into the standard.

## Short template for committing a solution

```text
Topic: <optimization>
Docs: <what is claimed>
Code: <what is actually done>
Decision: <which rule goes into the skill>
Basis: <freshness/canonicity>
```
