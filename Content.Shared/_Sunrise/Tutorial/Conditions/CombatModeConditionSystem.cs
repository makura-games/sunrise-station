using Content.Shared.CombatMode;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет текущее состояние боевого режима игрока.
/// </summary>
public sealed partial class CombatModeConditionSystem : TutorialConditionSystem<CombatModeComponent, CombatModeCondition>
{
    protected override void Condition(Entity<CombatModeComponent> entity, ref TutorialConditionEvent<CombatModeCondition> args)
    {
        args.Result = entity.Comp.IsInCombatMode == args.Condition.Enabled;
    }
}

/// <summary>
/// Требует включённый или выключенный боевой режим.
/// </summary>
public sealed partial class CombatModeCondition : TutorialConditionBase<CombatModeCondition>
{
    /// <summary>
    /// Ожидаемое состояние боевого режима.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
