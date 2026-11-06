using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Standing;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records lying down and standing up during a tutorial step.
/// </summary>
public sealed partial class LieDownStandUpListenedConditionSystem
    : EventListenedConditionSystemBase<LieDownStandUpListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<TutorialPlayerComponent, StoodEvent>(OnStood);
    }

    private void OnDowned(Entity<TutorialPlayerComponent> ent, ref DownedEvent args)
    {
        RecordEvent(ent, DefaultKey);
    }

    private void OnStood(Entity<TutorialPlayerComponent> ent, ref StoodEvent args)
    {
        RecordEvent(ent, DefaultKey);
    }
}

/// <summary>
/// Checks if the player has changed standing state enough times for the step.
/// </summary>
public sealed partial class LieDownStandUpListenedCondition : EventListenedConditionBase<LieDownStandUpListenedCondition>
{
}
