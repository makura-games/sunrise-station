using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Smell;

/// <summary>
/// Scent washing DoAfter: raised after a player applies an item with
/// ScentCleaningComponent (soap) to a target. The handler in ScentCleaningSystem
/// clears temporary scents and applies temporary masking of the target's base scent.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ScentCleaningDoAfterEvent : SimpleDoAfterEvent
{
}
