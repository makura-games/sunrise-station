using Content.Server.Administration.Logs;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Database;
using Content.Shared.Forensics.Components;
using Content.Shared.Popups;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть вакцин и сывороток: запускает выздоровление у больного и выдаёт иммунитет здоровому.
/// </summary>
public sealed partial class DiseaseVaccineSystem : SharedDiseaseVaccineSystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseDonorRegistrySystem _donors = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void OnInjected(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
        ApplyVaccine(vaccine, user, target);
    }

    /// <summary>
    /// Применяет препарат к цели: запускает выздоровление и выдаёт иммунитет. Сам препарат не расходует.
    /// </summary>
    /// <remarks>
    /// Одноразовую вакцину удаляет общий код, а многоразовый инжектор оставляет её в слоте.
    /// </remarks>
    /// <returns>True, если препарат был заряжен и подействовал.</returns>
    public bool ApplyVaccine(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
        if (vaccine.Comp.Disease is not { } disease)
            return false;

        var cured = vaccine.Comp.Cures && _disease.TryBeginRecovery(target, disease, DiseaseCureReason.Treatment);

        if (vaccine.Comp.Immunizes && _registry.TryGetData(disease, out var proto))
        {
            _disease.GrantImmunity(target, disease, proto.Immunity);

            // Привитый годится как донор сыворотки: у него есть иммунитет к этому штамму.
            if (HasComp<DnaComponent>(target))
                _donors.RegisterDonor(target, disease);
        }

        var ev = new DiseaseVaccinatedEvent(disease);
        RaiseLocalEvent(target, ref ev);

        _adminLog.Add(LogType.Disease, LogImpact.Medium,
            $"{ToPrettyString(user):user} injected {ToPrettyString(target):target} with a vaccine for {ToPrettyString(disease)}");

        _popup.PopupEntity(
            cured
                ? Loc.GetString("disease-vaccine-popup-cured", ("target", target))
                : Loc.GetString("disease-vaccine-popup-immunized", ("target", target)),
            target,
            user);

        return true;
    }
}
