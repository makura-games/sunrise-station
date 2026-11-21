using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Standing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records lying down and standing up while an objective is active.
/// </summary>
public sealed partial class LieDownStandUpObjectiveConditionSystem
    : ObjectiveEventConditionSystem<LieDownStandUpObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, StoodEvent>(OnStood);
    }

    private void OnDowned(Entity<ObjectiveInteractionOwnerComponent> ent, ref DownedEvent args)
    {
        RecordEvent(ent, DefaultKey);
    }

    private void OnStood(Entity<ObjectiveInteractionOwnerComponent> ent, ref StoodEvent args)
    {
        RecordEvent(ent, DefaultKey);
    }
}

/// <summary>
/// Checks if the player has changed standing state enough times for the step.
/// </summary>
public sealed partial class LieDownStandUpObjectiveCondition : ObjectiveEventConditionBase<LieDownStandUpObjectiveCondition>
{
}
