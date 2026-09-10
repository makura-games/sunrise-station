using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Interaction;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records direct hand interactions performed by an objective owner.
/// </summary>
public sealed partial class InteractObjectiveConditionSystem : ObjectiveEventConditionSystem<InteractObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, UserInteractHandEvent>(OnUserInteractHand);
    }

    private void OnUserInteractHand(Entity<ObjectiveInteractionOwnerComponent> ent, ref UserInteractHandEvent args)
    {
        RecordEvent(ent, DefaultKey, args.Target);
    }
}

/// <summary>
/// Checks if the player has interacted with a target entity.
/// </summary>
public sealed partial class InteractObjectiveCondition : ObjectiveEventConditionBase<InteractObjectiveCondition>
{
}
