using Robust.Shared.Map;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Completes after the owner travels a configured distance while the condition is active.
/// </summary>
public sealed partial class TravelDistanceObjectiveCondition : ObjectiveConditionBase<TravelDistanceObjectiveCondition>
{
    /// <summary>
    /// Required distance in world units.
    /// </summary>
    [DataField]
    public float Distance = 1f;
}

/// <summary>
/// Stores accumulated distance independently for every active condition handle.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveTravelDistanceObjectiveComponent : Component
{
    /// <summary>
    /// Travel states keyed by condition handle.
    /// </summary>
    public Dictionary<ObjectiveConditionHandle, ObjectiveTravelState> States = [];
}

/// <summary>
/// Mutable travel accumulator for one condition instance.
/// </summary>
public sealed class ObjectiveTravelState
{
    /// <summary>
    /// Last owner position used to calculate the next segment.
    /// </summary>
    public MapCoordinates? LastPosition;

    /// <summary>
    /// Distance accumulated on matching maps.
    /// </summary>
    public float Distance;
}

/// <summary>
/// Tracks travel-distance condition lifecycles per objective instance.
/// </summary>
public sealed class TravelDistanceObjectiveConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveConditionValidationEvent<TravelDistanceObjectiveCondition>>(OnValidation);
        SubscribeLocalEvent<ObjectiveConditionActivatedEvent<TravelDistanceObjectiveCondition>>(OnActivated);
        SubscribeLocalEvent<ObjectiveConditionEvaluateEvent<TravelDistanceObjectiveCondition>>(OnEvaluate);
        SubscribeLocalEvent<ObjectiveConditionDeactivatedEvent<TravelDistanceObjectiveCondition>>(OnDeactivated);
    }

    private void OnValidation(ref ObjectiveConditionValidationEvent<TravelDistanceObjectiveCondition> args)
    {
        args.Handled = true;
        args.Valid = float.IsFinite(args.Condition.Distance) && args.Condition.Distance >= 0f;
        args.Error = args.Valid ? null : "travel distance cannot be negative";
    }

    private void OnActivated(ref ObjectiveConditionActivatedEvent<TravelDistanceObjectiveCondition> args)
    {
        var active = EnsureComp<ActiveTravelDistanceObjectiveComponent>(args.Context.Objective);
        active.States[args.Context.Handle] = new ObjectiveTravelState
        {
            LastPosition = GetPosition(args.Context.Owner),
        };
    }

    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<TravelDistanceObjectiveCondition> args)
    {
        if (!TryComp(args.Context.Objective, out ActiveTravelDistanceObjectiveComponent? active) ||
            !active.States.TryGetValue(args.Context.Handle, out var state))
        {
            args.Satisfied = false;
            return;
        }

        var position = GetPosition(args.Context.Owner);
        if (position != null && state.LastPosition is { } previous && previous.MapId == position.Value.MapId)
            state.Distance += (position.Value.Position - previous.Position).Length();

        state.LastPosition = position;
        args.Satisfied = state.Distance >= args.Condition.Distance;
    }

    private void OnDeactivated(ref ObjectiveConditionDeactivatedEvent<TravelDistanceObjectiveCondition> args)
    {
        if (!TryComp(args.Context.Objective, out ActiveTravelDistanceObjectiveComponent? active))
            return;

        active.States.Remove(args.Context.Handle);
        if (active.States.Count == 0 && !TerminatingOrDeleted(args.Context.Objective))
            RemComp<ActiveTravelDistanceObjectiveComponent>(args.Context.Objective);
    }

    private MapCoordinates? GetPosition(EntityUid owner)
    {
        if (TerminatingOrDeleted(owner))
            return null;

        var position = _transform.GetMapCoordinates(owner);
        return position.MapId == MapId.Nullspace ? null : position;
    }
}
