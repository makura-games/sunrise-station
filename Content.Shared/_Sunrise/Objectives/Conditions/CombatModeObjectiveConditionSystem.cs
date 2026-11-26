using Content.Shared._Sunrise.Objectives;
using Content.Shared.CombatMode;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет текущее состояние боевого режима игрока.
/// </summary>
public sealed partial class CombatModeObjectiveConditionSystem : ObjectiveConditionSystem<CombatModeComponent, CombatModeObjectiveCondition>
{
    protected override void Condition(Entity<CombatModeComponent> entity, ref ObjectiveConditionEvaluateEvent<CombatModeObjectiveCondition> args)
    {
        args.Satisfied = entity.Comp.IsInCombatMode == args.Condition.Enabled;
    }
}

/// <summary>
/// Требует включённый или выключенный боевой режим.
/// </summary>
public sealed partial class CombatModeObjectiveCondition : ObjectiveConditionBase<CombatModeObjectiveCondition>
{
    /// <summary>
    /// Ожидаемое состояние боевого режима.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
