using Content.Shared.Alert;
using Content.Shared._Sunrise.Grab.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Grab.Components;

/// <summary>
/// Stores the active grab state for the entity being held by another entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedGrabSystem))]
public sealed partial class GrabbedComponent : Component
{
    /// <summary>
    /// Entity currently grabbing this entity.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid Grabber = EntityUid.Invalid;

    /// <summary>
    /// Current strength of the grab.
    /// </summary>
    [AutoNetworkedField]
    public GrabStage Stage = GrabStage.No;

    /// <summary>
    /// Chance that movement breaks the grab instead of being resisted by the grabber.
    /// </summary>
    [AutoNetworkedField]
    public float EscapeChance = 1f;

    /// <summary>
    /// Bonus escape chance accumulated from failed escape attempts at the current grab stage.
    /// </summary>
    [AutoNetworkedField]
    public float EscapeChanceBonus;

    /// <summary>
    /// Next time a movement escape attempt can roll.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextEscapeAttempt;

    /// <summary>
    /// Cooldown between movement escape attempts.
    /// </summary>
    [DataField]
    public TimeSpan EscapeAttemptCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Extra escape chance added after each failed escape attempt.
    /// </summary>
    [DataField]
    public float EscapeChanceBonusPerFail = 0.15f;

    /// <summary>
    /// Alert shown to the grabbed entity.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> GrabbedAlert = "Grabbed";
}
