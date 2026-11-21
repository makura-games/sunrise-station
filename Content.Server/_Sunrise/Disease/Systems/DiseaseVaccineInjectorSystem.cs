using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Systems;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть многоразового вакцинного инжектора: действие вакцины из слота на цель.
/// </summary>
public sealed partial class DiseaseVaccineInjectorSystem : SharedDiseaseVaccineInjectorSystem
{
    [Dependency] private DiseaseVaccineSystem _vaccine = default!;

    protected override void OnInjected(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
        _vaccine.ApplyVaccine(vaccine, user, target);
    }
}
