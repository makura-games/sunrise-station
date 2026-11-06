using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records tutorial player item throws.
/// </summary>
public sealed partial class ThrowListenedConditionSystem : EventListenedConditionSystemBase<ThrowListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(Entity<TutorialObservableComponent> ent, ref ThrownEvent args)
    {
        if (args.User == null)
            return;

        if (!ent.Comp.Observers.Contains(args.User.Value))
            return;

        RecordEvent(args.User.Value, DefaultKey, ent);
    }
}

/// <summary>
/// Checks if the player has thrown a target entity.
/// </summary>
public sealed partial class ThrowListenedCondition : EventListenedConditionBase<ThrowListenedCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
