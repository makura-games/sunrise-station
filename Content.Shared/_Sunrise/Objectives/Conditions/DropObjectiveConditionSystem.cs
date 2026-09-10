using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records item drops from objective owners while avoiding state-application replays.
/// </summary>
public sealed partial class DropObjectiveConditionSystem : ObjectiveEventConditionSystem<DropObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, DroppedEvent>(OnDropped);
    }

    private void OnDropped(Entity<ObjectiveInteractionObserverComponent> ent, ref DroppedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordObservedEvent(ent, DefaultKey, args.User);
    }
}

/// <summary>
/// Checks if the player has dropped a target entity.
/// </summary>
public sealed partial class DropObjectiveCondition : ObjectiveEventConditionBase<DropObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
