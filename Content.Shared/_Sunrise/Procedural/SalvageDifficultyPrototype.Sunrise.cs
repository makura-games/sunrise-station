using System;
using Robust.Shared.Prototypes;

namespace Content.Shared.Procedural;

public sealed partial class SalvageDifficultyPrototype
{
    [DataField]
    public string LootPrototype = "SalvageLoot";

    /// <summary>
    /// Minimum round time after which this difficulty can appear.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    /// <summary>
    /// Weighted chance for this difficulty when generating offers.
    /// </summary>
    [DataField]
    public float Probability = 1f;
}
