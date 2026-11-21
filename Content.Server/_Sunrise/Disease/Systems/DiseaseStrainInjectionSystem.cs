using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть укола выведенным штаммом: попытка заразить цель через обычные проверки.
/// </summary>
public sealed partial class DiseaseStrainInjectionSystem : SharedDiseaseStrainInjectionSystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    protected override void OnInjected(Entity<DiseaseStrainInjectorComponent> ent, EntityUid user, EntityUid target)
    {
        if (!TryComp<DiseaseStrainSampleComponent>(ent, out var sample) || sample.Disease is not { } disease)
            return;

        // Попытка расходует дозу даже при иммунитете или сработавшей защите цели.
        _disease.TryInfect(target, disease, DiseaseTransmissionRoute.Injection, ent.Owner, 1f);
        _adminLog.Add(LogType.Disease, LogImpact.High,
            $"{ToPrettyString(user):user} injected {ToPrettyString(target):target} with strain {ToPrettyString(disease)}");
    }
}
