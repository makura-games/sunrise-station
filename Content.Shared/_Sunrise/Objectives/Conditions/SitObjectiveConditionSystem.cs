using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Buckle.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Handles <see cref="SitObjectiveCondition"/>.
/// Subscribes directly to <see cref="BuckledEvent"/> on the player entity,
/// so <c>ObjectiveInteractionObserverComponent</c> is not required.
/// </summary>
public sealed partial class SitObjectiveConditionSystem
    : ObjectiveEventConditionSystem<SitObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, BuckledEvent>(OnBuckled);
    }

    private void OnBuckled(Entity<ObjectiveInteractionOwnerComponent> ent, ref BuckledEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordEvent(ent, DefaultKey, args.Strap.Owner);
    }
}

/// <summary>
/// Checks if the player has buckled onto a strap entity (sat on a chair, strapped into a seat, etc.).
/// Supports any strap or a specific prototype via <see cref="ObjectiveEventConditionBase{SitObjectiveCondition}.Target"/>.
/// </summary>
public sealed partial class SitObjectiveCondition : ObjectiveEventConditionBase<SitObjectiveCondition>
{
    // BuckledEvent приходит прямо на ObjectiveInteractionOwnerComponent, поэтому
    // ObjectiveInteractionObserverComponent здесь не нужен и ObserveAnyWithoutTarget остаётся false.
}
