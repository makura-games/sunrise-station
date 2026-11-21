namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Type-safe lifecycle dispatch used internally by the objective runtime.
/// </summary>
public interface IObjectiveConditionEventRaiser
{
    bool RaiseValidation<TCondition>(
        EntityUid owner,
        ObjectiveStartOptions options,
        TCondition condition,
        out string? error)
        where TCondition : ObjectiveConditionBase<TCondition>;

    void RaiseActivated<TCondition>(ObjectiveConditionContext context, TCondition condition)
        where TCondition : ObjectiveConditionBase<TCondition>;

    bool? RaiseEvaluate<TCondition>(ObjectiveConditionContext context, TCondition condition)
        where TCondition : ObjectiveConditionBase<TCondition>;

    void RaiseDeactivated<TCondition>(ObjectiveConditionContext context, TCondition condition)
        where TCondition : ObjectiveConditionBase<TCondition>;
}

/// <summary>
/// Requests condition-specific configuration validation before an objective starts.
/// </summary>
[ByRefEvent]
public record struct ObjectiveConditionValidationEvent<TCondition>(
    EntityUid Owner,
    ObjectiveStartOptions Options,
    TCondition Condition)
    where TCondition : ObjectiveConditionBase<TCondition>
{
    public bool Handled;
    public bool Valid = true;
    public string? Error;
}

/// <summary>
/// Activates condition-specific registrations and observers.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveConditionActivatedEvent<TCondition>(
    ObjectiveConditionContext Context,
    TCondition Condition)
    where TCondition : ObjectiveConditionBase<TCondition>;

/// <summary>
/// Requests a live condition evaluation. History conditions may leave the result unset.
/// </summary>
[ByRefEvent]
public record struct ObjectiveConditionEvaluateEvent<TCondition>(
    ObjectiveConditionContext Context,
    TCondition Condition)
    where TCondition : ObjectiveConditionBase<TCondition>
{
    public bool? Satisfied;
}

/// <summary>
/// Removes condition-specific registrations and observers.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveConditionDeactivatedEvent<TCondition>(
    ObjectiveConditionContext Context,
    TCondition Condition)
    where TCondition : ObjectiveConditionBase<TCondition>;
