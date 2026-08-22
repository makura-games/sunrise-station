using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет предмет, который находится именно в активной руке игрока.
/// </summary>
public sealed partial class ActiveHandContainsConditionSystem
    : TutorialConditionSystem<HandsComponent, ActiveHandContainsCondition>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> entity, ref TutorialConditionEvent<ActiveHandContainsCondition> args)
    {
        if (!_hands.TryGetActiveItem(entity.AsNullable(), out var activeItem))
            return;

        if (!TryGetPrototypeId(activeItem, out var protoId))
            return;

        args.Result = protoId == args.Condition.Item;
    }
}

/// <summary>
/// Условие, требующее конкретный предмет в активной руке.
/// </summary>
public sealed partial class ActiveHandContainsCondition : TutorialConditionBase<ActiveHandContainsCondition>
{
    /// <summary>
    /// Предмет, который должен находиться в активной руке.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;
}
