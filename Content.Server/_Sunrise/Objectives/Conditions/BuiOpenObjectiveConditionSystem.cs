using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;

namespace Content.Server._Sunrise.Objectives.Conditions;

/// <summary>
/// Records bound UI openings on entities observed by an objective owner.
/// </summary>
public sealed partial class BuiOpenObjectiveConditionSystem
    : ObjectiveEventConditionSystem<BuiOpenObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
    }

    private void OnBoundUIOpened(Entity<ObjectiveInteractionObserverComponent> ent, ref BoundUIOpenedEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.Actor);
    }
}
