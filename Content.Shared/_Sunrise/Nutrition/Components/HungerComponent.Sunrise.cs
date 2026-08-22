using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Nutrition.Components;

public sealed partial class HungerComponent
{
    // Sunrise-Start
    /// <summary>
    /// Mangleness healing amount when hunger level is Okay or higher. Negative values indicate healing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessHealingOkay = -0.01f;

    /// <summary>
    /// Mangleness healing amount when hunger level is Peckish. Negative values indicate healing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessHealingPeckish = -0.005f;

    /// <summary>
    /// Mangleness decay rate multiplier applied when entity has Mangleness damage and hunger level is Okay or higher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultOkay = 4.0f;

    /// <summary>
    /// Mangleness decay rate multiplier applied when entity has Mangleness damage and hunger level is Overfed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultOverfed = 4.0f;

    /// <summary>
    /// Mangleness decay rate multiplier applied when entity has Mangleness damage and hunger level is Peckish.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManglenessDecayMultPeckish = 2.0f;

    /// <summary>
    /// Tracks whether the entity previously had active Mangleness damage. Used to trigger threshold updates when state changes.
    /// </summary>
    [ViewVariables, AutoNetworkedField, Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool HadMangleness;
    // Sunrise-End
}
