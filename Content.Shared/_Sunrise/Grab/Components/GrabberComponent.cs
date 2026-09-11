using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared._Sunrise.Grab.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Grab.Components;

/// <summary>
/// Stores the active grab state for the entity holding another entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedGrabSystem))]
public sealed partial class GrabberComponent : Component
{
    /// <summary>
    /// Entity currently being grabbed.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? Grabbed;

    /// <summary>
    /// Current strength of the grab.
    /// </summary>
    [AutoNetworkedField]
    public GrabStage Stage = GrabStage.No;

    /// <summary>
    /// Next time this grab can be tightened or used for suffocation stamina damage.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextStageChange;

    /// <summary>
    /// Cooldown between stage changes.
    /// </summary>
    [DataField]
    public TimeSpan StageChangeCooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Alert shown to the grabber while they are grabbing an entity.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> GrabbingAlert = "Grabbing";

    /// <summary>
    /// Escape chance used when the grabbed entity tries to break the pull by moving.
    /// </summary>
    [DataField]
    public Dictionary<GrabStage, float> EscapeChances = new()
    {
        { GrabStage.No, 1f },
        { GrabStage.Soft, 0.7f },
        { GrabStage.Hard, 0.4f },
        { GrabStage.Suffocate, 0.1f },
    };

    /// <summary>
    /// Stamina damage dealt when tightening an already suffocating grab.
    /// </summary>
    [DataField]
    public float SuffocateStaminaDamage = 10f;

    /// <summary>
    /// Damage multiplier for entities thrown out of hard or suffocating grabs.
    /// </summary>
    [DataField]
    public float ThrowDamageModifier = 1f;

    /// <summary>
    /// Damage applied to the thrown entity, and optionally the struck entity, after grab throws.
    /// </summary>
    [DataField]
    public DamageSpecifier ThrowDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 5 },
        },
    };

    /// <summary>
    /// Base throw speed used when throwing the grabbed entity from a hard grab.
    /// </summary>
    [DataField]
    public float ThrowSpeed = 65f;

    /// <summary>
    /// Stamina damage applied to the thrown grabbed entity when it collides.
    /// </summary>
    [DataField]
    public float ThrowStaminaDamage = 65f;

    /// <summary>
    /// Extra virtual hand items required for each grab stage.
    /// </summary>
    [DataField]
    public Dictionary<GrabStage, int> VirtualItemStageCount = new()
    {
        { GrabStage.Suffocate, 1 },
    };

    /// <summary>
    /// Extra virtual items created by grab stages. The base pulling virtual item is owned by pulling.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> VirtualItems = new();

    /// <summary>
    /// Virtual items currently being deleted by the grab system itself.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> DeletingVirtualItems = new();
}
