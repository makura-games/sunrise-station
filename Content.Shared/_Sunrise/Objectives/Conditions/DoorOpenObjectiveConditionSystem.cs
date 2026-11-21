using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Doors;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records successful door opening attempts made by objective owners.
/// </summary>
public sealed partial class DoorOpenObjectiveConditionSystem : ObjectiveEventConditionSystem<DoorOpenObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
    }

    private void OnBeforeDoorOpened(Entity<ObjectiveInteractionObserverComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (args.User is null)
            return;

        RecordObservedEvent(ent, DefaultKey, args.User.Value);
    }
}

/// <summary>
/// Checks if the player has opened a door/airlock (any door, or a specific prototype).
/// </summary>
public sealed partial class DoorOpenObjectiveCondition : ObjectiveEventConditionBase<DoorOpenObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
