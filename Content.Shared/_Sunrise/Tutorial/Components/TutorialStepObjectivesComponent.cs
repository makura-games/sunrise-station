namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Correlates the objective instances owned by the currently active tutorial step.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialStepObjectivesComponent : Component
{
    /// <summary>
    /// One-shot completion objective.
    /// </summary>
    public EntityUid Completion;

    /// <summary>
    /// Optional reversible precondition monitor.
    /// </summary>
    public EntityUid? Preconditions;

    /// <summary>
    /// Reversible monitors in the same order as the step failure rules.
    /// </summary>
    public List<EntityUid> Failures = [];

    /// <summary>
    /// Cached completion graph state.
    /// </summary>
    public bool CompletionSatisfied;

    /// <summary>
    /// Cached precondition graph state, or true when the step has no preconditions.
    /// </summary>
    public bool PreconditionsSatisfied = true;

    /// <summary>
    /// Cached failure graph states in the same order as <see cref="Failures"/>.
    /// </summary>
    public List<bool> FailuresSatisfied = [];
}
