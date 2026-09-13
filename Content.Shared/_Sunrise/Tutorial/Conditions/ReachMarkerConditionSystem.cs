using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Checks if the player has reached a <see cref="TutorialGoalMarkerComponent"/> entity
/// within the specified distance.
/// </summary>
public sealed partial class ReachMarkerConditionSystem : TutorialConditionSystem<TutorialPlayerComponent, ReachMarkerCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<ReachMarkerCondition> args)
    {
        var playerPos = _transform.GetMapCoordinates(entity.Owner);
        if (playerPos.MapId == MapId.Nullspace)
            return;

        var query = EntityQueryEnumerator<TutorialGoalMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (args.Condition.Marker is { } marker && !marker.Equals(Prototype(uid)?.ID))
                continue;

            if (xform.GridUid != entity.Comp.Grid)
                continue;

            var markerPos = _transform.GetMapCoordinates(uid, xform: xform);
            if (markerPos.MapId != playerPos.MapId)
                continue;

            if ((markerPos.Position - playerPos.Position).Length() >= args.Condition.Distance)
                continue;

            args.Result = true;
            return;
        }
    }
}

/// <summary>
/// Checks if the player has reached a tutorial goal marker within a given distance.
/// If <see cref="Marker"/> is not set, any <see cref="TutorialGoalMarkerComponent"/> entity qualifies.
/// </summary>
public sealed partial class ReachMarkerCondition : TutorialConditionBase<ReachMarkerCondition>
{
    /// <summary>
    /// Prototype ID of the marker to reach.
    /// Leave empty to match any <see cref="TutorialGoalMarkerComponent"/>.
    /// </summary>
    [DataField]
    public EntProtoId? Marker;

    /// <summary>
    /// Maximum distance in world units at which the condition is satisfied.
    /// </summary>
    [DataField]
    public float Distance = 1.5f;
}
