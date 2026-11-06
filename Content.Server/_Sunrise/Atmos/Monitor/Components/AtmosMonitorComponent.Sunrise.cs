// Sunrise-Edit
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor;

namespace Content.Server.Atmos.Monitor.Components;

public sealed partial class AtmosMonitorComponent
{
    /// <summary>
    /// Specifies whether this sensor should be completely ignored in storyteller stress calculations.
    /// </summary>
    [DataField("ignoreStress")]
    public bool IgnoreStress = false;
}
