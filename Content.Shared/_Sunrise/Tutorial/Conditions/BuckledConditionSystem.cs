using Content.Shared.Buckle.Components;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, пристёгнут ли игрок к стулу или другому сиденью.
/// </summary>
public sealed partial class BuckledConditionSystem
    : TutorialConditionSystem<BuckleComponent, BuckledCondition>
{
    protected override void Condition(Entity<BuckleComponent> entity, ref TutorialConditionEvent<BuckledCondition> args)
    {
        args.Result = entity.Comp.Buckled == args.Condition.Buckled;
    }
}

/// <summary>
/// Требует указанное состояние пристёгивания игрока.
/// </summary>
public sealed partial class BuckledCondition : TutorialConditionBase<BuckledCondition>
{
    /// <summary>
    /// Должен ли игрок быть пристёгнут.
    /// </summary>
    [DataField]
    public bool Buckled = true;
}
