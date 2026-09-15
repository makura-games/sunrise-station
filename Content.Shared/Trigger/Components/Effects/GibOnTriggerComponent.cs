using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Will gib the entity when triggered.
/// If TargetUser is true the user will be gibbed instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GibOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Should gibbing also delete the owners items?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DeleteItems = false;

    // Sunrise added start - support gear acidifier without deleting the body
    /// <summary>
    /// Whether the triggered entity itself should be gibbed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GibBody = true;

    /// <summary>
    /// Whether giblets should be spawned when gibbing the body.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GibOrgans = true;
    // Sunrise added end
}
