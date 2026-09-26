using Content.Shared._Sunrise.Smell.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Components;

/// <summary>
/// Emitter marker: an item that grants its bearer a temporary scent when held
/// or worn in an inventory slot. Settings (which contact triggers it, the scent,
/// the duration) are configured directly on the item via this component's fields.
/// </summary>
[RegisterComponent]
public sealed partial class ScentEmitterComponent : Component
{
    /// <summary>
    /// Where the item must end up to emit its scent.
    /// </summary>
    [DataField]
    public ScentEmitSpot Spot = ScentEmitSpot.SpecificSlot;

    /// <summary>
    /// The specific slot if Spot is SpecificSlot (e.g. "mask").
    /// </summary>
    [DataField]
    public string Slot = "mask";

    /// <summary>
    /// Which scent is applied (an id from scents.yml).
    /// </summary>
    [DataField]
    public ProtoId<ScentPrototype> Scent = default!;

    /// <summary>
    /// How long the scent stays on the bearer.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(300);
}

/// <summary>
/// Determines at which contact with a player a scent-emitting item emits its smell.
/// </summary>
public enum ScentEmitSpot
{
    /// <summary>
    /// Only in the specific clothing slot (the Slot field). E.g. a cigarette in the mouth.
    /// </summary>
    SpecificSlot,

    /// <summary>
    /// In any clothing slot. E.g. an explosive whose smell is noticeable everywhere.
    /// </summary>
    AnySlot,

    /// <summary>
    /// In hands only.
    /// </summary>
    Hands,
}
