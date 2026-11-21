using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Поднимается, когда засев чашки Петри завершён.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseCultureDoAfterEvent : SimpleDoAfterEvent
{
}
