using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Nutrition;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records food and drink consumption events for objective conditions.
/// </summary>
public sealed partial class ConsumeObjectiveConditionSystem : ObjectiveEventConditionSystem<ConsumeObjectiveCondition, ObjectiveHealthOwnerComponent, ObjectiveHealthObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveHealthObserverComponent, IngestedEvent>(OnIngested);
    }

    private void OnIngested(Entity<ObjectiveHealthObserverComponent> ent, ref IngestedEvent args)
    {
        // Target — сущность, которая ест; при принудительном кормлении User может отличаться.
        RecordObservedEvent(ent, DefaultKey, args.Target);
    }
}

/// <summary>
/// Checks if the player has consumed a food or drink item (any item, or a specific prototype).
/// </summary>
public sealed partial class ConsumeObjectiveCondition : ObjectiveEventConditionBase<ConsumeObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
