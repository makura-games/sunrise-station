using Content.Shared._Sunrise.Objectives.Conditions;

namespace Content.Shared._Sunrise.Objectives.Components;

/// <summary>
/// Owner registrations for interaction-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveInteractionOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations for interaction-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveInteractionObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}

/// <summary>
/// Owner registrations for combat-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveCombatOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations for combat-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveCombatObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}

/// <summary>
/// Owner registrations for health-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveHealthOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations for health-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveHealthObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}

/// <summary>
/// Owner registrations for container-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveContainerOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations for container-oriented history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveContainerObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}

/// <summary>
/// Owner registrations for communication history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveCommunicationOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations reserved for communication history conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveCommunicationObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}

/// <summary>
/// Owner registrations for client-reported UI conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveUiOwnerComponent : Component, IObjectiveEventOwnerComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
    public Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; } = [];
}

/// <summary>
/// Target registrations reserved for UI conditions.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveUiObserverComponent : Component, IObjectiveEventObserverComponent
{
    public Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; } = [];
}
