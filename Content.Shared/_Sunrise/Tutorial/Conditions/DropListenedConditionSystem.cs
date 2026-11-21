using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records item drops from tutorial players while avoiding state-application replays.
/// </summary>
public sealed partial class DropListenedConditionSystem : EventListenedConditionSystemBase<DropListenedCondition>
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, DroppedEvent>(OnDropped);
    }

    private void OnDropped(Entity<TutorialObservableComponent> ent, ref DroppedEvent args)
    {
        if (_timing.ApplyingState || !ent.Comp.Observers.Contains(args.User))
            return;

        RecordEvent(args.User, DefaultKey, ent);
    }
}

/// <summary>
/// Checks if the player has dropped a target entity.
/// </summary>
public sealed partial class DropListenedCondition : EventListenedConditionBase<DropListenedCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
