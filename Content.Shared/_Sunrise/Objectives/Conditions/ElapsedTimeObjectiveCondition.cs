using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Completes after a fixed duration measured from condition activation.
/// </summary>
public sealed partial class ElapsedTimeObjectiveCondition : ObjectiveConditionBase<ElapsedTimeObjectiveCondition>
{
    /// <summary>
    /// Duration measured from objective activation.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}

/// <summary>
/// Stores deadlines for elapsed-time conditions on an objective entity.
/// Deadlines live as long as the objective entity, so re-activation after a transfer keeps the original timer.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveElapsedObjectiveComponent : Component
{
    /// <summary>
    /// Deadlines keyed by condition handle.
    /// </summary>
    public Dictionary<ObjectiveConditionHandle, TimeSpan> Deadlines = [];
}

/// <summary>
/// Tracks elapsed-time condition lifecycles without relying on a consumer-owned start timestamp.
/// </summary>
public sealed partial class ElapsedTimeObjectiveConditionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnValidation(ref ObjectiveConditionValidationEvent<ElapsedTimeObjectiveCondition> args)
    {
        args.Handled = true;
        args.Valid = args.Condition.Delay >= TimeSpan.Zero;
        args.Error = args.Valid ? null : "elapsed duration cannot be negative";
    }

    [SubscribeLocalEvent]
    private void OnActivated(ref ObjectiveConditionActivatedEvent<ElapsedTimeObjectiveCondition> args)
    {
        var active = EnsureComp<ActiveElapsedObjectiveComponent>(args.Context.Objective);
        active.Deadlines.TryAdd(args.Context.Handle, _timing.CurTime + args.Condition.Delay);
    }

    [SubscribeLocalEvent]
    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<ElapsedTimeObjectiveCondition> args)
    {
        args.Satisfied = TryComp(args.Context.Objective, out ActiveElapsedObjectiveComponent? active) &&
                         active.Deadlines.TryGetValue(args.Context.Handle, out var deadline) &&
                         _timing.CurTime >= deadline;
    }
}
