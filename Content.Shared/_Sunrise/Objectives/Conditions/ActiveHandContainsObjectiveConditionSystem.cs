using Content.Shared._Sunrise.Objectives;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет предмет, который находится именно в активной руке игрока.
/// </summary>
public sealed partial class ActiveHandContainsObjectiveConditionSystem
    : ObjectiveConditionSystem<HandsComponent, ActiveHandContainsObjectiveCondition>
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> entity, ref ObjectiveConditionEvaluateEvent<ActiveHandContainsObjectiveCondition> args)
    {
        if (!_hands.TryGetActiveItem(entity.AsNullable(), out var activeItem))
            return;

        if (!TryGetPrototypeId(activeItem, out var protoId))
            return;

        args.Satisfied = protoId == args.Condition.Item;
    }
}

/// <summary>
/// Условие, требующее конкретный предмет в активной руке.
/// </summary>
public sealed partial class ActiveHandContainsObjectiveCondition : ObjectiveConditionBase<ActiveHandContainsObjectiveCondition>
{
    /// <summary>
    /// Предмет, который должен находиться в активной руке.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;
}
