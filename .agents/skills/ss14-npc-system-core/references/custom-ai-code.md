# Custom AI Code Guide

## Purpose

Use this file when prototypes are no longer enough and you need to write your own AI code.

## Content

1. Quick selection of extension point
2. Where to write the code
3. HTNOperator contract
4. Statement template
5. Precondition pattern
6. Connecting new C# to YAML
7. Practical pattern “operator + system”
8. Anti-patterns when writing your AI code

## Quick selection of extension point

1. A new condition check is needed:
add `HTNPrecondition`.
2. A new atomic behavior step is needed:
add `HTNOperator`.
3. We need a new way to select targets:
add `UtilityQuery`/`UtilityConsideration`/`UtilityCurve`.
4. We need heavy runtime logic that ticks separately:
add/expand the system + runtime component, and use the operator as a “gateway”.
5. Fork specifics are needed:
add code to the fork segment (`Content.Server/_Sunrise/...`) and connect via YAML.

## Where to write code

1. Operators:
`Content.Server/NPC/HTN/PrimitiveTasks/Operators/**`
2. Preconditions:
`Content.Server/NPC/HTN/Preconditions/**`
3. Utility API:
`Content.Server/NPC/Queries/**`
4. NPC runtime systems:
`Content.Server/NPC/Systems/**`
5. Fork extensions (example):
`Content.Server/_Sunrise/NPC/HTN/**`

## HTNOperator contract

1. `Initialize(...)`:
initialize dependencies/systems.
2. `Plan(...)`:
check the validity of the step and optionally return `effects`.
3. `Startup(...)`:
run the runtime part of the step.
4. `Update(...)`:
return `Continuing`, `Finished` or `Failed`.
5. `TaskShutdown(...)`/`PlanShutdown(...)`:
clean components/blackboard/state.

## Operator template

```csharp
public sealed partial class MyOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey")]
    public string TargetKey = "Target";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) || target == EntityUid.Invalid)
            return (false, null);

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        // Run runtime logic or expose a component.
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        // Set the runtime logic status to HTNOperatorStatus.
        return HTNOperatorStatus.Finished;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        // Clear runtime components/keys.
    }
}
```

## Precondition template

```csharp
public sealed partial class MyPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("key", required: true)]
    public string Key = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(Key, out var ent, _entManager) && ent != EntityUid.Invalid;
    }
}
```

## Connecting new C# to YAML

1. Create an operator/precondition class in the desired namespace.
2. Use it in YAML via `!type:MyOperator` or `!type:MyPrecondition`.
3. Insert a new primitive/precondition into the desired `htnCompound`.
4. If necessary, expand `utility_queries.yml`.

## Practical pattern “operator + system”

1. In the operator:
minimum logic, only orchestration and contract with blackboard.
2. In the system:
all the heavy runtime, updating components, working with physics/combat/actions.
3. In shutdown:
guaranteed cleanup so that NPCs do not leave “dangling” runtime states.

## Anti-patterns when writing your own AI code

1. Keep a complex mutable-state inside a singleton operator.
2. Do entity side effects in `Plan()` and break the determinism of the scheduler.
3. Do not process the `Failed` state and receive an endless replan-thrash.
4. Delete/change blackboard keys in unexpected places without a contract.
5. Replace existing combat/steering systems with ad-hoc code within the operator.
