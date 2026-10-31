using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Stores the current tutorial session state for a player entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class TutorialPlayerComponent : Component
{
    /// <summary>
    /// Tutorial sequence currently assigned to this player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<TutorialSequencePrototype> SequenceId = "IntroductionTutorial";

    /// <summary>
    /// Index of the active step inside <see cref="SequenceId"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int StepIndex;

    /// <summary>
    /// Temporary step shown instead of the sequence step until a recoverable failure is fixed.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ProtoId<TutorialStepPrototype>? ActiveStepOverride;

    /// <summary>
    /// Entity currently hosting the tutorial bubble, if any.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? CurrentBubbleTarget;

    /// <summary>
    /// Navigation target selected for the active tutorial step.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Whether the tutorial session has been explicitly initialized by the server.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool TutorialInitialized;

    /// <summary>
    /// Game time when the current tutorial step became active.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan StepStartedAt;

    /// <summary>
    /// Absolute game time when this tutorial session expires.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan? EndTime;

    /// <summary>
    /// Grid loaded for this player's tutorial session.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Grid;
}
