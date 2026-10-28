using Content.Shared._Sunrise.Smell.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell;

/// <summary>
/// A temporary scent currently applied to a bearer: what smells, since when
/// and for how long. Stored in ScentComponent.TemporaryScents. Runtime-only data,
/// not serialized.
/// </summary>
public sealed partial class ActiveTemporaryScent
{
    /// <summary>
    /// The scent that was applied.
    /// </summary>
    public ProtoId<ScentPrototype> Scent;

    /// <summary>
    /// The game time when the scent appeared.
    /// </summary>

    public TimeSpan StartTime;

    /// <summary>
    /// How long the temporary scent lasts: each source has its own duration.
    /// Intensity comes from the scent prototype itself (shared by all sources).
    /// </summary>
    public TimeSpan Duration;
}
