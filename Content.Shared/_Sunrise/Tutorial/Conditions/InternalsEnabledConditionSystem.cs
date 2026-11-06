using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Body.Systems;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, что маска и баллон подключены, а internals действительно подают газ.
/// </summary>
public sealed partial class InternalsEnabledConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, InternalsEnabledCondition>
{
    [Dependency] private SharedInternalsSystem _internals = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<InternalsEnabledCondition> args)
    {
        args.Result = _internals.AreInternalsWorking(entity.Owner);
    }
}

/// <summary>
/// Условие успешного включения работающих internals.
/// </summary>
public sealed partial class InternalsEnabledCondition : TutorialConditionBase<InternalsEnabledCondition>
{
}
