using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records use-in-hand and use-on-target interactions for objective conditions.
/// </summary>
public sealed partial class UseObjectiveConditionSystem : ObjectiveEventConditionSystem<UseObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, UserInteractUsingEvent>(OnUserInteractUsing);
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUserInteractUsing(Entity<ObjectiveInteractionOwnerComponent> ent, ref UserInteractUsingEvent args)
    {
        RecordEvent(ent, DefaultKey, args.Target, args.Used);
    }

    private void OnUseInHand(Entity<ObjectiveInteractionObserverComponent> ent, ref UseInHandEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.User);
    }
}

/// <summary>
/// Checks if the player has used a target entity.
/// </summary>
public sealed partial class UseObjectiveCondition : ObjectiveEventConditionBase<UseObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}

