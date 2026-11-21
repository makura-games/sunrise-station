using Content.Shared._Sunrise.Objectives;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет, тянет ли игрок нужный предмет.
/// </summary>
public sealed partial class PullingObjectiveConditionSystem
    : ObjectiveConditionSystem<PullerComponent, PullingObjectiveCondition>
{
    protected override void Condition(
        Entity<PullerComponent> ent,
        ref ObjectiveConditionEvaluateEvent<PullingObjectiveCondition> args)
    {
        if (ent.Comp.Pulling is not { } pulling)
            return;

        if (args.Condition.Target is { } target)
        {
            if (!TryGetPrototypeId(pulling, out var prototype) || target != prototype)
                return;
        }

        args.Satisfied = true;
    }
}

/// <summary>
/// Условие активного таскания предмета с необязательной проверкой его прототипа.
/// </summary>
public sealed partial class PullingObjectiveCondition : ObjectiveConditionBase<PullingObjectiveCondition>
{
    /// <summary>
    /// Прототип предмета, который должен тянуть игрок. Любой предмет, если не указан.
    /// </summary>
    [DataField]
    public EntProtoId? Target;
}
