using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, тянет ли игрок нужный предмет.
/// </summary>
public sealed partial class PullingConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, PullingCondition>
{
    protected override void Condition(
        Entity<TutorialPlayerComponent> ent,
        ref TutorialConditionEvent<PullingCondition> args)
    {
        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling is not { } pulling)
            return;

        if (args.Condition.Target is { } target)
        {
            if (!TryGetPrototypeId(pulling, out var prototype) || target != prototype)
                return;
        }

        args.Result = true;
    }
}

/// <summary>
/// Условие активного таскания предмета с необязательной проверкой его прототипа.
/// </summary>
public sealed partial class PullingCondition : TutorialConditionBase<PullingCondition>
{
    /// <summary>
    /// Прототип предмета, который должен тянуть игрок. Любой предмет, если не указан.
    /// </summary>
    [DataField]
    public EntProtoId? Target;
}
