using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Raised after an objective instance is linked to its owner and activated.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveStartedEvent(
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    string? SourceIdentifier);

/// <summary>
/// Raised whenever stored progress or evaluated state for one condition changes.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveConditionChangedEvent(
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    ObjectiveConditionHandle Handle,
    int Progress,
    bool RawSatisfied,
    bool Satisfied,
    string? SourceIdentifier);

/// <summary>
/// Raised on both false-to-true and true-to-false graph transitions.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveStateChangedEvent(
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    bool PreviousSatisfied,
    bool Satisfied,
    string? SourceIdentifier);

/// <summary>
/// Raised when an objective graph transitions to satisfied.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveCompletedEvent(
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    string? SourceIdentifier);

/// <summary>
/// Raised after an active objective is transferred to another owner.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveOwnerChangedEvent(
    EntityUid PreviousOwner,
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    string? SourceIdentifier);

/// <summary>
/// Raised after condition bindings are deactivated and an objective is stopped.
/// </summary>
[ByRefEvent]
public readonly record struct ObjectiveStoppedEvent(
    EntityUid Owner,
    EntityUid Objective,
    ProtoId<ObjectivePrototype>? Prototype,
    ObjectiveStopReason Reason,
    string? SourceIdentifier);
