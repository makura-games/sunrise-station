using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Поднимается, когда ручной анализатор закончил осмотр цели.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseScanDoAfterEvent : SimpleDoAfterEvent
{
}
