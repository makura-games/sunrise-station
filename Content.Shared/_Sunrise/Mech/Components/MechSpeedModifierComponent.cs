using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Mech.Components;

/// <summary>
/// Modifies a mech's speed while this item is installed in its power slot.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechSpeedModifierComponent : Component
{
    /// <summary>
    /// The walking speed multiplier.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float WalkModifier = 1f;

    /// <summary>
    /// The sprinting speed multiplier.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SprintModifier = 1f;
}
