using Content.Shared._Sunrise.Objectives;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет, что маска и баллон подключены, а internals действительно подают газ.
/// </summary>
public sealed partial class InternalsEnabledObjectiveConditionSystem
    : ObjectiveConditionSystem<InternalsComponent, InternalsEnabledObjectiveCondition>
{
    [Dependency] private readonly SharedInternalsSystem _internals = default!;

    protected override void Condition(Entity<InternalsComponent> entity, ref ObjectiveConditionEvaluateEvent<InternalsEnabledObjectiveCondition> args)
    {
        args.Satisfied = _internals.AreInternalsWorking(entity.Comp);
    }
}

/// <summary>
/// Условие успешного включения работающих internals.
/// </summary>
public sealed partial class InternalsEnabledObjectiveCondition : ObjectiveConditionBase<InternalsEnabledObjectiveCondition>
{
}
