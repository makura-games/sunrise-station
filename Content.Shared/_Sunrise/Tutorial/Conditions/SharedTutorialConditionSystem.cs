using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Manages tutorial condition evaluation.
/// Provides methods to check whether tutorial conditions are satisfied for a given entity.
/// </summary>
public sealed partial class SharedTutorialConditionsSystem : EntitySystem, ITutorialConditionRaiser
{
    /// <summary>
    /// Checks a list of conditions to verify that they all return true.
    /// </summary>
    /// <param name="target">Entity we're checking conditions on.</param>
    /// <param name="conditions">Conditions to check.</param>
    /// <returns><c>true</c> if all conditions pass or the list is null; <c>false</c> if any condition fails.</returns>
    public bool TryConditions(EntityUid target, IReadOnlyList<TutorialCondition>? conditions)
    {
        if (conditions == null)
            return true;

        for (var i = 0; i < conditions.Count; i++)
        {
            if (!TryCondition(target, conditions[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks a list of conditions to see if any of them returns true.
    /// </summary>
    /// <param name="target">Entity we're checking conditions on.</param>
    /// <param name="conditions">Conditions to check.</param>
    /// <returns><c>true</c> if at least one condition passes; <c>false</c> if none pass or the list is null.</returns>
    public bool TryAnyCondition(EntityUid target, IReadOnlyList<TutorialCondition>? conditions)
    {
        if (conditions == null)
            return false;

        for (var i = 0; i < conditions.Count; i++)
        {
            if (TryCondition(target, conditions[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks a single <see cref="TutorialCondition"/> on an entity, respecting <see cref="TutorialCondition.Inverted"/>.
    /// </summary>
    /// <param name="target">Entity we're checking the condition on.</param>
    /// <param name="condition">Condition to check.</param>
    /// <returns><c>true</c> if the condition passes (after applying inversion).</returns>
    public bool TryCondition(EntityUid target, TutorialCondition condition)
    {
        return condition.Inverted != condition.RaiseEvent(target, this);
    }

    /// <summary>
    /// Raises a <see cref="TutorialConditionEvent{T}"/> on the target entity and returns the result.
    /// Called internally via <see cref="ITutorialConditionRaiser"/>; prefer <see cref="TryCondition"/> for external use.
    /// </summary>
    public bool RaiseConditionEvent<T>(EntityUid target, T effect) where T : TutorialConditionBase<T>
    {
        var effectEv = new TutorialConditionEvent<T>(effect);
        RaiseLocalEvent(target, ref effectEv);
        return effectEv.Result;
    }
}

/// <summary>
/// Base system for handling a specific <see cref="TutorialConditionBase{TCon}"/> on entities with component <typeparamref name="T"/>.
/// Subscribe to <see cref="TutorialConditionEvent{TCon}"/> and set <see cref="TutorialConditionEvent{TCon}.Result"/> in the handler.
/// </summary>
/// <typeparam name="T">Component required on the entity for this condition to apply.</typeparam>
/// <typeparam name="TCon">The condition type being evaluated.</typeparam>
public abstract partial class TutorialConditionSystem<T, TCon> : EntitySystem where T : Component where TCon : TutorialConditionBase<TCon>
{
    public override void Initialize()
    {
        SubscribeLocalEvent<T, TutorialConditionEvent<TCon>>(Condition);
    }
    protected abstract void Condition(Entity<T> entity, ref TutorialConditionEvent<TCon> args);
    public bool TryGetPrototypeId(EntityUid? uid, out EntProtoId protoId)
    {
        protoId = default;

        if (uid is not { } target)
            return false;

        var proto = Prototype(target);
        if (proto == null)
            return false;

        protoId = proto.ID;
        return true;
    }
}

/// <summary>
/// Provides a type-safe way to raise <see cref="TutorialConditionEvent{T}"/> without losing the concrete condition type.
/// </summary>
public interface ITutorialConditionRaiser
{
    bool RaiseConditionEvent<T>(EntityUid target, T effect) where T : TutorialConditionBase<T>;
}

/// <summary>
/// Generic base for <see cref="TutorialCondition"/> implementations.
/// Preserves the concrete type <typeparamref name="T"/> when raising the condition event,
/// so the correct <see cref="TutorialConditionSystem{T,TCon}"/> handler receives it.
/// </summary>
/// <typeparam name="T">The concrete condition type (CRTP pattern).</typeparam>
public abstract partial class TutorialConditionBase<T> : TutorialCondition where T : TutorialConditionBase<T>
{
    public override bool RaiseEvent(EntityUid target, ITutorialConditionRaiser raiser)
    {
        if (this is not T type)
            return false;

        return raiser.RaiseConditionEvent(target, type);
    }
}

/// <summary>
/// A tutorial condition that is evaluated by raising a <see cref="TutorialConditionEvent{T}"/> on an entity.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TutorialCondition
{
    public abstract bool RaiseEvent(EntityUid target, ITutorialConditionRaiser raiser);

    /// <summary>
    /// If true, inverts the result of the condition check.
    /// </summary>
    [DataField]
    public bool Inverted;
}

/// <summary>
/// By-ref event raised on an entity to evaluate a <see cref="TutorialConditionBase{T}"/>.
/// The handling system should set <see cref="Result"/> to indicate whether the condition is met.
/// </summary>
/// <param name="Condition">The condition being evaluated.</param>
[ByRefEvent]
public record struct TutorialConditionEvent<T>(T Condition) where T : TutorialConditionBase<T>
{
    /// <summary>
    /// Whether the condition is satisfied. Defaults to <c>false</c> if no system handles the event.
    /// </summary>
    public bool Result;

    /// <summary>
    /// The condition being evaluated in this event.
    /// </summary>
    public readonly T Condition = Condition;
}
