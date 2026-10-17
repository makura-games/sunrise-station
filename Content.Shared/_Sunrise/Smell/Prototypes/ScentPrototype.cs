using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Prototypes;

/// <summary>
/// Scent prototype: localized description, tooltip color, intensity used for sorting
/// within strength groups, and a bold-output flag.
/// </summary>
[Prototype]
public sealed partial class ScentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// LocId of the scent description shown when smelling.
    /// </summary>
    [DataField(required: true)]
    public LocId Description { get; private set; } = default!;

    /// <summary>
    /// Text color of the scent in the tooltip; null — no highlighting.
    /// </summary>
    [DataField]
    public Color? Color { get; private set; }

    /// <summary>
    /// Scent intensity 0..1 — shared by all sources of this scent.
    /// </summary>
    [DataField]
    public float Intensity { get; private set; } = 1f;

    /// <summary>
    /// Render the scent description in bold (an accenting/pungent smell).
    /// </summary>
    [DataField]
    public bool Fat { get; private set; }
}
