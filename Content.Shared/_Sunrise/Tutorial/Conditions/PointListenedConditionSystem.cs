using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Pointing;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records successful pointing actions from tutorial players.
/// </summary>
public sealed partial class PointListenedConditionSystem : EventListenedConditionSystemBase<PointListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, AfterPointedAtEvent>(OnAfterPointedAt);
    }

    private void OnAfterPointedAt(Entity<TutorialPlayerComponent> ent, ref AfterPointedAtEvent args)
    {
        RecordEvent(ent, DefaultKey, args.Pointed);
    }
}

/// <summary>
/// Checks if the player has pointed at a target entity.
/// </summary>
public sealed partial class PointListenedCondition : EventListenedConditionBase<PointListenedCondition>
{
}
