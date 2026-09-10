using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Nutrition.Components;

public sealed partial class ThirstComponent
{
    // Sunrise-Start
    [DataField]
    public DamageSpecifier? DehydrationDamage;

    /// <summary>
    /// Mangeliness healing amount when thirst level is Okay or higher. Negative values indicate healing (damage recovery signal).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessHealingOkay = -0.01f;

    /// <summary>
    /// Mangeliness healing amount when thirst level is Thirsty. Negative values indicate healing (damage recovery signal).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessHealingThirsty = -0.005f;

    /// <summary>
    /// Mangleness decay rate multiplier applied to BaseDecayRate when entity has Mangleness damage and thirst level is OverHydrated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultOverhydrated = 4.0f;

    /// <summary>
    /// Mangleness decay rate multiplier applied to BaseDecayRate when entity has Mangleness damage and thirst level is Okay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultOkay = 4.0f;

    /// <summary>
    /// Mangleness decay rate multiplier applied to BaseDecayRate when entity has Mangleness damage and thirst level is Thirsty.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultThirsty = 2.0f;

    /// <summary>
    /// Tracks whether the entity previously had active Mangleness damage to detect transitions for threshold effects.
    /// </summary>
    [ViewVariables, AutoNetworkedField, Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool HadMangleness;
    // Sunrise-End
}
