using Content.Shared._Sunrise.Objectives;
using Content.Shared.Buckle.Components;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет, пристёгнут ли игрок к стулу или другому сиденью.
/// </summary>
public sealed partial class BuckledObjectiveConditionSystem
    : ObjectiveConditionSystem<BuckleComponent, BuckledObjectiveCondition>
{
    protected override void Condition(Entity<BuckleComponent> entity, ref ObjectiveConditionEvaluateEvent<BuckledObjectiveCondition> args)
    {
        args.Satisfied = entity.Comp.Buckled == args.Condition.Buckled;
    }
}

/// <summary>
/// Требует указанное состояние пристёгивания игрока.
/// </summary>
public sealed partial class BuckledObjectiveCondition : ObjectiveConditionBase<BuckledObjectiveCondition>
{
    /// <summary>
    /// Должен ли игрок быть пристёгнут.
    /// </summary>
    [DataField]
    public bool Buckled = true;
}
