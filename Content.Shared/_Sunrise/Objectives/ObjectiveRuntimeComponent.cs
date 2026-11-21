using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Stores the definition, options, and runtime condition state of one objective instance.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveSystem))]
public sealed partial class ObjectiveRuntimeComponent : Component
{
    /// <summary>
    /// Entity whose actions and state are evaluated.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid TrackedOwner;

    /// <summary>
    /// Optional reusable prototype that supplied the definition.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<ObjectivePrototype>? Prototype;

    /// <summary>
    /// Behavior definition used by this runtime instance.
    /// </summary>
    public ObjectiveDefinition Definition = default!;

    /// <summary>
    /// Immutable-at-runtime launch options copied when the objective starts.
    /// </summary>
    public ObjectiveStartOptions Options = default!;

    /// <summary>
    /// Runtime state keyed by a stable or generated condition key.
    /// </summary>
    public Dictionary<string, ObjectiveConditionRuntimeState> Conditions = [];

    /// <summary>
    /// Whether the full all/any graph is currently satisfied.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Satisfied;

    /// <summary>
    /// Whether condition-specific bindings are currently active.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool ConditionsActive;
}

/// <summary>
/// Stores the mutable state of one condition without changing its shared prototype definition.
/// </summary>
public sealed class ObjectiveConditionRuntimeState
{
    /// <summary>
    /// Runtime key used by <see cref="ObjectiveConditionHandle"/>.
    /// </summary>
    public string Key = string.Empty;

    /// <summary>
    /// Shared behavior definition.
    /// </summary>
    public ObjectiveCondition Condition = default!;

    /// <summary>
    /// Whether this condition belongs to the definition's alternative any-of group.
    /// </summary>
    public bool IsAny;

    /// <summary>
    /// Accumulated history progress.
    /// </summary>
    public int Progress;

    /// <summary>
    /// Latest explicit state reported by a live-state condition system.
    /// </summary>
    public bool ReportedSatisfied;

    /// <summary>
    /// Current non-inverted result.
    /// </summary>
    public bool RawSatisfied;

    /// <summary>
    /// Current result after applying inversion.
    /// </summary>
    public bool Satisfied;
}

/// <summary>
/// Links an owner to its objective entities.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveSystem))]
public sealed partial class ObjectiveOwnerComponent : Component
{
    /// <summary>
    /// Objective entities owned by this entity.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> Objectives = [];
}

/// <summary>
/// Marks an objective that can still evaluate conditions and receive progress.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveObjectiveComponent : Component;

/// <summary>
/// Marks a completed one-shot objective retained for inspection.
/// </summary>
[RegisterComponent]
public sealed partial class CompletedObjectiveComponent : Component;

/// <summary>
/// Prevents duplicate stop processing while deletion is queued.
/// </summary>
[RegisterComponent]
public sealed partial class StoppingObjectiveComponent : Component;
