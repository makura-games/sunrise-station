using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Checks if the player is holding a specific item in their hands.
/// </summary>
public sealed partial class HoldInHandsConditionSystem : TutorialConditionSystem<HandsComponent, HoldInHandsCondition>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> entity, ref TutorialConditionEvent<HoldInHandsCondition> args)
    {
        foreach (var held in _hands.EnumerateHeld(entity.Owner))
        {
            if (args.Condition.Item == null)
            {
                args.Result = true;
                return;
            }

            var proto = Prototype(held);

            if (proto?.ID == null)
                continue;

            if (proto.ID != args.Condition.Item)
                continue;

            args.Result = true;
            return;
        }
    }
}

/// <summary>
/// Checks whether the player is holding any item or a specific item prototype.
/// </summary>
public sealed partial class HoldInHandsCondition : TutorialConditionBase<HoldInHandsCondition>
{
    /// <summary>
    /// Optional item prototype that must be held. If unset, any held item passes.
    /// </summary>
    [DataField]
    public EntProtoId? Item;
}
