using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Поднимается, когда укол многоразовым вакцинным инжектором завершён.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseVaccineInjectorDoAfterEvent : SimpleDoAfterEvent
{
}
