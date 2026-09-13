using System;
using System.Collections.Generic;

namespace Content.Server._Sunrise.Humanoid;

[RegisterComponent, Access(typeof(HumanoidPhysicalStatsSystem))]
public sealed partial class HumanoidPhysicalStatsComponent : Component
{
    /// <summary>
    /// Original fixture densities captured before applying profile mass modifiers.
    /// </summary>
    [NonSerialized]
    public readonly Dictionary<string, float> BaseDensities = new();
}
