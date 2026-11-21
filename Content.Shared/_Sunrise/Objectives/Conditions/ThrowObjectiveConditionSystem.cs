using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Throwing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records item throws performed by an objective owner.
/// </summary>
public sealed partial class ThrowObjectiveConditionSystem : ObjectiveEventConditionSystem<ThrowObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(Entity<ObjectiveInteractionObserverComponent> ent, ref ThrownEvent args)
    {
        if (args.User == null)
            return;

        RecordObservedEvent(ent, DefaultKey, args.User.Value);
    }
}

/// <summary>
/// Checks if the player has thrown a target entity.
/// </summary>
public sealed partial class ThrowObjectiveCondition : ObjectiveEventConditionBase<ThrowObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
