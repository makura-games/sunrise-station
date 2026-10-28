using Content.Shared._Sunrise.Smell.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Components;

/// <summary>
/// Scent bearer: static base and personal scent, temporary scents from sources,
/// and the masking state of the base scent.
/// </summary>
[RegisterComponent]
public sealed partial class ScentComponent : Component
{
    /// <summary>
    /// Static (base) scents of the bearer, always present when smelled:
    /// species notes and other permanent aromas.
    /// </summary>
    [DataField]
    public List<ProtoId<ScentPrototype>> BaseScents = new();

    /// <summary>
    /// Profile used to generate the personal scent (color + notes) from character
    /// traits (name, age, gender, voice). If unset — no personal scent.
    /// </summary>
    [DataField]
    public ProtoId<PersonalScentProfilePrototype>? PersonalScentProfile;

    /// <summary>
    /// Temporary scents list. Runtime data: filled only by code during the round,
    /// not serialized into YAML or map saves.
    /// </summary>
    [NonSerialized]
    public List<ActiveTemporaryScent> TemporaryScents = new();

    /// <summary>
    /// Whether temporary masking of the base scent is active (e.g. after washing with soap).
    /// While active the base (static + personal) scent is hidden from smellers,
    /// temporary scents are still shown. Runtime data.
    /// </summary>
    [NonSerialized]
    public bool Masked;

    /// <summary>
    /// Game time until which the masking lasts. After expiry the mask is removed
    /// lazily on the next smelling. Runtime data.
    /// </summary>
    [NonSerialized]
    public TimeSpan MaskUntil;
}
