using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Interaction;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records activation events performed by an objective owner on observed world entities.
/// </summary>
public sealed partial class ActivateInWorldObjectiveConditionSystem : ObjectiveEventConditionSystem<ActivateInWorldObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnActivateInWorld(Entity<ObjectiveInteractionObserverComponent> ent, ref ActivateInWorldEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.User);
    }
}

/// <summary>
/// Checks if the player has activated a target entity in the world.
/// </summary>
public sealed partial class ActivateInWorldObjectiveCondition : ObjectiveEventConditionBase<ActivateInWorldObjectiveCondition>
{
}
