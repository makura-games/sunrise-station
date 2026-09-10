using Content.Shared._Sunrise.Objectives;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Проверяет, удерживает ли игрок указанный предмет в нескольких руках.
/// </summary>
public sealed partial class WieldedObjectiveConditionSystem
    : ObjectiveConditionSystem<HandsComponent, WieldedObjectiveCondition>
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> entity, ref ObjectiveConditionEvaluateEvent<WieldedObjectiveCondition> args)
    {
        foreach (var held in _hands.EnumerateHeld(entity.Owner))
        {
            if (!TryGetPrototypeId(held, out var proto) || proto != args.Condition.Item)
                continue;

            if (!TryComp<WieldableComponent>(held, out var wieldable))
                return;

            args.Satisfied = wieldable.Wielded == args.Condition.Wielded;
            return;
        }
    }
}

/// <summary>
/// Требует указанное состояние удержания предмета в нескольких руках.
/// </summary>
public sealed partial class WieldedObjectiveCondition : ObjectiveConditionBase<WieldedObjectiveCondition>
{
    /// <summary>
    /// Прототип проверяемого предмета.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    /// <summary>
    /// Должен ли предмет удерживаться в нескольких руках.
    /// </summary>
    [DataField]
    public bool Wielded = true;
}
