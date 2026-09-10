using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records activated actions
/// </summary>
public sealed partial class ActivateActionObjectiveConditionSystem : ObjectiveEventConditionSystem<ActivateActionObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, ActionAttemptEvent>(OnAttemptAction);
    }

    private void OnAttemptAction(Entity<ObjectiveInteractionObserverComponent> ent, ref ActionAttemptEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.User);
    }
}

/// <summary>
/// Checks if the player has activated target action entity.
/// </summary>
public sealed partial class ActivateActionObjectiveCondition : ObjectiveEventConditionBase<ActivateActionObjectiveCondition>
{
}
