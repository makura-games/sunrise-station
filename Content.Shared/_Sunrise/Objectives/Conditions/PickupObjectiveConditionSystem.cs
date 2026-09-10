using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Hands;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records items picked up into an objective owner's hands and starts observing them if needed.
/// </summary>
public sealed partial class PickupObjectiveConditionSystem : ObjectiveEventConditionSystem<PickupObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, DidEquipHandEvent>(OnDidEquipHand);
    }

    private void OnDidEquipHand(Entity<ObjectiveInteractionOwnerComponent> ent, ref DidEquipHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordEvent(ent, DefaultKey, args.Equipped);
    }
}

/// <summary>
/// Checks if the player has picked up a target entity into hands.
/// </summary>
public sealed partial class PickupObjectiveCondition : ObjectiveEventConditionBase<PickupObjectiveCondition>
{
}
