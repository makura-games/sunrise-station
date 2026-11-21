using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Поднимается, когда инъекция вакцины или сыворотки завершена.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseVaccineDoAfterEvent : SimpleDoAfterEvent
{
}
