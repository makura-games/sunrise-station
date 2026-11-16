using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared.Hands;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records items picked up into tutorial player hands and starts observing them if needed.
/// </summary>
public sealed partial class PickupListenedConditionSystem : EventListenedConditionSystemBase<PickupListenedCondition>
{
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, DidEquipHandEvent>(OnDidEquipHand);
    }

    private void OnDidEquipHand(Entity<TutorialPlayerComponent> ent, ref DidEquipHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordEvent(ent, DefaultKey, args.Equipped);

        if (!TryComp<TutorialTrackerComponent>(ent, out var tracker))
            return;

        Tutorial.TryObserveEntity(ent, args.Equipped, tracker);
    }
}

/// <summary>
/// Checks if the player has picked up a target entity into hands.
/// </summary>
public sealed partial class PickupListenedCondition : EventListenedConditionBase<PickupListenedCondition>
{
}
