using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Base system for a live condition evaluated against one component on the objective owner.
/// </summary>
public abstract class ObjectiveConditionSystem<TComponent, TCondition> : EntitySystem
    where TComponent : Component
    where TCondition : ObjectiveConditionBase<TCondition>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveConditionValidationEvent<TCondition>>(OnValidation);
        SubscribeLocalEvent<ObjectiveConditionEvaluateEvent<TCondition>>(OnEvaluate);
    }

    private void OnValidation(ref ObjectiveConditionValidationEvent<TCondition> args)
    {
        args.Handled = true;
        args.Valid = Validate(args.Condition, out var error);
        args.Error = error;
    }

    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<TCondition> args)
    {
        args.Satisfied = false;

        if (!TryComp(args.Context.Owner, out TComponent? component))
            return;

        Condition((args.Context.Owner, component), ref args);
    }

    /// <summary>
    /// Validates condition-specific configuration before an objective starts.
    /// </summary>
    protected virtual bool Validate(TCondition condition, out string? error)
    {
        error = null;
        return true;
    }

    /// <summary>
    /// Reports the current raw state of a live condition.
    /// </summary>
    protected abstract void Condition(
        Entity<TComponent> entity,
        ref ObjectiveConditionEvaluateEvent<TCondition> args);

    /// <summary>
    /// Resolves an entity prototype ID for condition-specific matching.
    /// </summary>
    protected bool TryGetPrototypeId(EntityUid? uid, out EntProtoId prototype)
    {
        prototype = default;
        if (uid is not { } target || Prototype(target) is not { } entityPrototype)
            return false;

        prototype = entityPrototype.ID;
        return true;
    }
}
