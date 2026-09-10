using System;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Stable runtime reference to one condition in an objective instance.
/// </summary>
public readonly record struct ObjectiveConditionHandle(EntityUid Objective, string Key);

/// <summary>
/// Immutable context supplied to a condition lifecycle event.
/// </summary>
public readonly record struct ObjectiveConditionContext(
    EntityUid Owner,
    EntityUid Objective,
    ObjectiveConditionHandle Handle,
    ObjectiveStartOptions Options,
    ProtoId<ObjectivePrototype>? Prototype);

/// <summary>
/// Controls the lifetime and observation policy of a new objective instance.
/// </summary>
public sealed class ObjectiveStartOptions
{
    /// <summary>
    /// Determines whether satisfaction is terminal or continuously monitored.
    /// </summary>
    public ObjectiveRunMode Mode = ObjectiveRunMode.OneShot;

    /// <summary>
    /// Determines whether a completed one-shot entity is retained for inspection.
    /// </summary>
    public ObjectiveCompletionRetention CompletionRetention = ObjectiveCompletionRetention.Remove;

    /// <summary>
    /// Entity locations inspected by target-oriented conditions.
    /// </summary>
    public ObjectiveObservationScope ObservationScope = ObjectiveObservationScope.Default;

    /// <summary>
    /// Maximum range for nearby-entity observation.
    /// </summary>
    public float ObservationRange = 10f;

    /// <summary>
    /// Optional consumer-owned identifier used to correlate runtime events.
    /// </summary>
    public string? SourceIdentifier;
}

/// <summary>
/// Determines whether an objective is terminal or reversible.
/// </summary>
public enum ObjectiveRunMode : byte
{
    OneShot,
    Monitor,
}

/// <summary>
/// Determines what happens to a completed one-shot objective entity.
/// </summary>
public enum ObjectiveCompletionRetention : byte
{
    Remove,
    Retain,
}

/// <summary>
/// Locations searched by target-oriented objective conditions.
/// </summary>
[Flags]
public enum ObjectiveObservationScope : byte
{
    None = 0,
    Nearby = 1 << 0,
    Hands = 1 << 1,
    Equipment = 1 << 2,
    Actions = 1 << 3,
    Default = Nearby | Hands | Equipment | Actions,
}

/// <summary>
/// Public snapshot of one objective instance.
/// </summary>
public readonly record struct ObjectiveStatus(
    bool Active,
    bool Satisfied,
    bool Completed,
    ObjectiveRunMode Mode,
    string? SourceIdentifier);

/// <summary>
/// Public snapshot of one objective condition.
/// </summary>
public readonly record struct ObjectiveConditionStatus(
    int Progress,
    bool RawSatisfied,
    bool Satisfied);

/// <summary>
/// Describes why an objective instance stopped.
/// </summary>
public enum ObjectiveStopReason : byte
{
    Cancelled,
    Completed,
    OwnerTerminating,
    ObjectiveTerminating,
}
