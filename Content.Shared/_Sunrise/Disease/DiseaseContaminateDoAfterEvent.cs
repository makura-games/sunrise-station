using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Завершение подмешивания культуры в еду или питьё.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseContaminateDoAfterEvent : SimpleDoAfterEvent
{
}
