using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Accumulates player movement distance for travel-distance tutorial conditions.
/// </summary>
public sealed partial class TravelDistanceConditionSystem : TutorialConditionSystem<TutorialPlayerComponent, TravelDistanceCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<TravelDistanceCondition> args)
    {
        if (args.Condition.Distance <= 0f)
        {
            args.Result = true;
            return;
        }

        var tracker = EnsureComp<TutorialDistanceTrackerComponent>(entity);
        var pos = _transform.GetWorldPosition(entity);

        if (tracker.LastPosition is { } last)
        {
            var delta = (pos - last).Length();
            if (delta > 0f)
            {
                tracker.Distance += delta;
                tracker.LastPosition = pos;
                Dirty(entity, tracker);
            }

            args.Result = tracker.Distance >= args.Condition.Distance;
            return;
        }

        tracker.LastPosition = pos;
        Dirty(entity, tracker);
        args.Result = tracker.Distance >= args.Condition.Distance;
    }
}

/// <summary>
/// Checks if the player has traveled the specified distance.
/// </summary>
public sealed partial class TravelDistanceCondition : TutorialConditionBase<TravelDistanceCondition>
{
    /// <summary>
    /// Distance in world units the player must travel.
    /// </summary>
    [DataField]
    public float Distance = 1f;
}
