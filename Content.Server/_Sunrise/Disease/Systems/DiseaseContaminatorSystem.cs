using Content.Server.Administration.Logs;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Database;
using Content.Shared.Labels.EntitySystems;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть подмешивания: заводит штамм пробирки и превращает обработанную еду в очаг.
/// </summary>
public sealed partial class DiseaseContaminatorSystem : SharedDiseaseContaminatorSystem
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    /// <summary>
    /// Каждая пробирка несёт свой штамм: вакцина от одной покупки не закрывает другую.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseContaminatorComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DiseaseStrainSampleComponent>(ent, out var sample) || sample.Disease != null ||
            _registry.CreateStrain(ent.Comp.Disease) is not { } strain)
            return;

        sample.Disease = strain;
        sample.HasStrain = true;
        sample.IsBlank = false;
        sample.IsDesigned = false;
        sample.Routes = DiseaseTransmissionRoute.Ingestion;
        sample.StrainCode = _analysis.GetStrainCode(strain);
        Dirty(ent.Owner, sample);
        _label.Label(ent.Owner, sample.StrainCode);
    }

    protected override bool ValidateSample(Entity<DiseaseContaminatorComponent> ent)
    {
        return base.ValidateSample(ent) && TryComp<DiseaseStrainSampleComponent>(ent, out var sample) &&
               sample.Disease is { } strain && _registry.TryGetData(strain, out var definition) &&
               definition.Routes == DiseaseTransmissionRoute.Ingestion;
    }

    protected override bool OnContaminated(Entity<DiseaseContaminatorComponent> ent, EntityUid user, EntityUid target)
    {
        if (!TryComp<DiseaseStrainSampleComponent>(ent, out var sample) || sample.Disease is not { } strain)
            return false;

        var source = EnsureComp<DiseaseSourceComponent>(target);
        source.Routes |= DiseaseTransmissionRoute.Ingestion;
        source.IngestionChance = 1f;
        if (!source.Diseases.Contains(strain))
            source.Diseases.Add(strain);

        _adminLog.Add(LogType.Disease, LogImpact.High,
            $"{ToPrettyString(user):user} contaminated {ToPrettyString(target):target} with {ToPrettyString(strain)} ({sample.StrainCode}) using {ToPrettyString(ent.Owner)}");
        return true;
    }
}
