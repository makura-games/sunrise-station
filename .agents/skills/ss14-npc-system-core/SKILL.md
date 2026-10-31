---
name: ss14-npc-system-core
description: Deep dive into the NPC system in SS14/Sunrise: HTN planning, utility target selection, steering/pathfinding, blackboard and execution contracts. Use it when you need to understand the general scheme and logic of NPCs, create or rework behavior prototypes (`htnCompound`, `rootTask`, `blackboard`), or write your own AI code (operators, preconditions, components, systems) and safely integrate it into runtime.
---

# NPC System: Architecture, Prototypes and Custom AI

Use this skill as your main NPC playbook in SS14/Sunrise.
Keep your focus on fresh code and check its relevance through `git log`/`git blame` (cutoff: `2024-02-20`).

## What to download first

1. `references/architecture-runtime.md` - general scheme of work, layers and life cycle of NPCs.
2. `references/behavior-prototypes.md` - how to design and write behavior prototypes (`htnCompound`, `utilityQuery`, `HTN` component).
3. `references/custom-ai-code.md` - how and where to write your AI code (operators, preconditions, systems, components).
4. `references/debug-validation.md` - debugging, checking and protection against regressions.

## General scheme of NPC work

1. `NPCSystem` updates only active NPCs (`ActiveNPCComponent`) and limits the update budget (`npc.max_updates`).
2. `HTNSystem` schedules the behavior through the time-sliced ​​job queue (`HTNPlanJob`) and executes the current plan.
3. `HTNPlanJob` expands the tree `HTNCompoundTask` -> `HTNPrimitiveTask`, selecting branch by preconditions.
4. `HTNOperator.Plan()` evaluates the validity of the step and can return `effects` for blackboard.
5. `HTNOperator.Startup()/Update()/TaskShutdown()` performs runtime logic.
6. The operator usually delegates heavy/coordinated logic to external systems through components (steering/combat, etc.).
7. `NPCSteeringSystem` controls movement, pathfinding and obstacle avoidance.
8. `NPCUtilitySystem` selects targets via `utilityQuery` + considerations/curves.
9. `NpcFactionSystem` affects the selection of hostile/friendly targets.

## Design principles and logic

1. Keep `Plan()` as clean and predictable as possible: compute, not mutate, the world.
2. Perform side effects in `Startup()`/`Update()`, and cleanup in shutdown hooks.
3. Divide “choose a goal”, “get to the goal”, “take an action” into different primitives.
4. Use blackboard as a single contract between the prototype and the code.
5. Design behavior as a composition of small compounds, not a giant-tree.
6. Always add a fallback branch (idle/noop) so that the NPC does not get stuck without a plan.
7. For movement and combat, rely on existing systems (`NPCSteeringSystem`, `NPCCombatSystem`), and not on ad-hoc logic in the operator.

## Patterns

1. Start root-behavior from the priority branch (combat/goal), then fallback (idle/follow).
2. Add `UtilityOperator` before the action if the goal is dynamic.
3. Update the target with the service (`services`) inside combat primitives.
4. Make `MoveToOperator` a separate step between goal selection and action.
5. Store ranges/flags in blackboard (`VisionRadius`, `MeleeRange`, `NavInteract`) for fine-tuning without code.
6. Return `effects` from `Plan()` to reuse expensive calculations in execution.
7. Use `IHtnConditionalShutdown` when you need a controlled cleanup with `TaskFinished` or `PlanFinished`.
8. Write preconditions as simple Boolean gate objects without side effects.
9. Disable/enable HTN via `SetHTNEnabled`/replan patterns instead of direct mutation of internal fields.
10. Validate the tree for recursive traps using a separate test.

## Anti-patterns

1. Write a monolithic `htnCompound`, which is difficult to analyze and test.
2. Do side effects in `Plan()` (especially network/physical changes).
3. Do not clean the blackboard and runtime components when completing a task.
4. Rely on only one target key without fallback logic.
5. Ignore `services`, which causes the NPC to “stick” to an outdated target.
6. Mix pathfinding/steering/combat manually in one statement.
7. Write a precondition that depends on a hidden mutable-state outside the blackboard/components.
8. Use invalid types in `blackboard` YAML (absence of `!type` for complex/explicit types).
9. Build behavior without the idle/noop branch.
10. Ignore budget/cooldown (`PlanCooldown`, `npc.max_updates`) when diagnosing “stupid” NPCs.

## Mini-examples

### 1) Root prototype with fallback

```yaml
- type: htnCompound
  id: MyCustomHostileCompound
  branches:
    - tasks:
        - !type:HTNCompoundTask
          task: MeleeCombatCompound
    - tasks:
        - !type:HTNCompoundTask
          task: IdleCompound
```

### 2) Connecting HTN to an entity

```yaml
- type: entity
  id: MobMyNpc
  components:
  - type: HTN
    rootTask:
      task: MyCustomHostileCompound
    blackboard:
      VisionRadius: !type:Single
        18
      AggroVisionRadius: !type:Single
        24
      NavInteract: !type:Bool
        true
```

### 3) Custom operator

```csharp
public sealed partial class MyOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        // Transfer heavy/runtime logic to a separate system or component.
        return HTNOperatorStatus.Finished;
    }
}
```

## Extension rule

1. When adding a new goal/behavior, first expand `utilityQuery` and tasks prototypes.
2. Move to new C# code only when the prototypes and current operators are no longer enough.
3. For new mechanics, first define an extension point (precondition/operator/system/component), then write the code.
4. After any extension, run a domain debug check and a smoke test with a real NPC.

Think of the NPC system as a `planning -> execution -> state maintenance` pipeline rather than a random collection of operators.
