namespace Content.Server._Sunrise.Mech.Components;

/// <summary>
/// Marks a battery installed in a mech and stores the link used for energy synchronization.
/// </summary>
[RegisterComponent]
public sealed partial class InstalledMechBatteryComponent : Component
{
    /// <summary>
    /// The mech using this battery.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Mech;

    /// <summary>
    /// The time of the next derived charge synchronization.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextUpdate;
}
