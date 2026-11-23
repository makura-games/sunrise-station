using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared.Buckle.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Handles <see cref="SitListenedCondition"/>.
/// Subscribes directly to <see cref="BuckledEvent"/> on the player entity,
/// so <c>TutorialObservableComponent</c> is not required.
/// </summary>
public sealed partial class SitListenedConditionSystem
    : EventListenedConditionSystemBase<SitListenedCondition>
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, BuckledEvent>(OnBuckled);
    }

    private void OnBuckled(Entity<TutorialPlayerComponent> ent, ref BuckledEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // Record against the strap (chair/seat) prototype for Target matching, plus AnyTarget.
        RecordEvent(ent, DefaultKey, args.Strap.Owner);
    }
}

/// <summary>
/// Checks if the player has buckled onto a strap entity (sat on a chair, strapped into a seat, etc.).
/// Supports any strap or a specific prototype via <see cref="EventListenedConditionBase{T}.Target"/>.
/// </summary>
public sealed partial class SitListenedCondition : EventListenedConditionBase<SitListenedCondition>
{
    // BuckledEvent fires directly on TutorialPlayerComponent — TutorialObservableComponent
    // is not used for this condition, so ObserveAnyWithoutTarget is intentionally left false.
}
