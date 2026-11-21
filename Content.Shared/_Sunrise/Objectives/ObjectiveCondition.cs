namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Defines one behavior-only condition evaluated by the objective runtime.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class ObjectiveCondition
{
    /// <summary>
    /// Optional stable identifier. Named objective prototypes require it; inline definitions do not.
    /// </summary>
    [DataField]
    public string? Id;

    /// <summary>
    /// Inverts the current evaluated result without changing stored progress.
    /// </summary>
    [DataField]
    public bool Inverted;

    /// <summary>
    /// Calculates the non-inverted condition state from runtime progress and an explicitly reported state.
    /// </summary>
    public virtual bool GetRawSatisfied(int progress, bool reportedSatisfied)
    {
        return reportedSatisfied;
    }

    internal abstract bool RaiseValidation(
        EntityUid owner,
        ObjectiveStartOptions options,
        IObjectiveConditionEventRaiser raiser,
        out string? error);

    internal abstract void RaiseActivated(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser);

    internal abstract bool? RaiseEvaluate(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser);

    internal abstract void RaiseDeactivated(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser);
}

/// <summary>
/// Preserves the concrete condition type while dispatching objective lifecycle events.
/// </summary>
public abstract partial class ObjectiveConditionBase<TCondition> : ObjectiveCondition
    where TCondition : ObjectiveConditionBase<TCondition>
{
    internal override bool RaiseValidation(
        EntityUid owner,
        ObjectiveStartOptions options,
        IObjectiveConditionEventRaiser raiser,
        out string? error)
    {
        if (this is not TCondition condition)
        {
            error = $"Condition {GetType().Name} does not satisfy its CRTP type contract";
            return false;
        }

        return raiser.RaiseValidation(owner, options, condition, out error);
    }

    internal override void RaiseActivated(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser)
    {
        if (this is TCondition condition)
            raiser.RaiseActivated(context, condition);
    }

    internal override bool? RaiseEvaluate(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser)
    {
        return this is TCondition condition
            ? raiser.RaiseEvaluate(context, condition)
            : null;
    }

    internal override void RaiseDeactivated(
        ObjectiveConditionContext context,
        IObjectiveConditionEventRaiser raiser)
    {
        if (this is TCondition condition)
            raiser.RaiseDeactivated(context, condition);
    }
}

/// <summary>
/// Base for history conditions completed by a configurable number of matching events.
/// </summary>
public abstract partial class ObjectiveCountConditionBase<TCondition> : ObjectiveConditionBase<TCondition>
    where TCondition : ObjectiveCountConditionBase<TCondition>
{
    /// <summary>
    /// Number of matching events required to satisfy the condition.
    /// </summary>
    [DataField]
    public int Count = 1;

    /// <inheritdoc />
    public override bool GetRawSatisfied(int progress, bool reportedSatisfied)
    {
        return Count <= 0 || progress >= Count;
    }
}
