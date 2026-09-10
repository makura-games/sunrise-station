using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Pointing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records successful pointing actions from objective owners.
/// </summary>
public sealed partial class PointObjectiveConditionSystem : ObjectiveEventConditionSystem<PointObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, AfterPointedAtEvent>(OnAfterPointedAt);
    }

    private void OnAfterPointedAt(Entity<ObjectiveInteractionOwnerComponent> ent, ref AfterPointedAtEvent args)
    {
        RecordEvent(ent, DefaultKey, args.Pointed);
    }
}

/// <summary>
/// Checks if the player has pointed at a target entity.
/// </summary>
public sealed partial class PointObjectiveCondition : ObjectiveEventConditionBase<PointObjectiveCondition>
{
}
