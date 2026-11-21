using System.Text;
using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease;
using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Ведёт образцы: записывает, что попало на мазок, и превращает образцы в результаты анализа.
/// Раскрывает только то, что разрешает запрошенный уровень, поэтому результат можно отправить
/// клиентскому интерфейсу без утечки скрытых данных болезни.
/// </summary>
/// <remarks>
/// Анализ никогда не раскрывает название болезни. Болезнь опознаётся по коду штамма, который
/// генерируется заново каждый раунд: он связывает носителей с разными симптомами, но его нельзя выучить между раундами.
/// </remarks>
public sealed partial class DiseaseAnalysisSystem : EntitySystem
{
    [Dependency] private DiseaseSourceSystem _source = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DiseaseSystem _disease = default!;

    [Dependency] private EntityQuery<DiseaseSourceComponent> _sourceQuery = default!;

    /// <summary>
    /// Записывает на мазок снимок болезней цели на конец do-after.
    /// </summary>
    /// <remarks>
    /// Образец заводится и с чистой цели: пустой мазок честно покажет в анализе, что патогенов нет.
    /// Снимок, а не ссылка на носителя: болезнь у цели может уйти или шагнуть по стадиям,
    /// а образец должен показывать то, что было в пробирке на момент взятия.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnSwabTaken(Entity<DiseaseSwabComponent> ent, ref DiseaseSwabTakenEvent args)
    {
        var sample = EnsureComp<DiseaseSampleComponent>(ent);
        sample.SubjectName = Name(args.Target);
        sample.Entries.Clear();

        foreach (var instance in _disease.GetDiseases(args.Target))
        {
            var symptoms = new List<ProtoId<DiseaseSymptomPrototype>>(instance.Symptoms.Count);
            foreach (var active in instance.Symptoms)
            {
                symptoms.Add(active.Symptom);
            }

            sample.Entries.Add(new DiseaseSampleEntry(instance.Disease, instance.Stage, symptoms));
        }

        if (_sourceQuery.TryComp(args.Target, out var source))
        {
            foreach (var disease in _source.GetDiseases((args.Target, source)))
            {
                sample.Entries.Add(new DiseaseSampleEntry(disease, null, new List<ProtoId<DiseaseSymptomPrototype>>()));
            }
        }
    }

    /// <summary>
    /// Анализирует взятый образец.
    /// </summary>
    /// <param name="sample">Использованный мазок с образцом.</param>
    /// <param name="level">Сколько раскрывает анализ; обычно определяется анализирующей машиной.</param>
    public DiseaseAnalysisResult Analyze(Entity<DiseaseSampleComponent> sample, DiseaseAnalysisLevel level)
    {
        var entries = new List<DiseaseAnalysisEntry>(sample.Comp.Entries.Count);

        foreach (var sampleEntry in sample.Comp.Entries)
        {
            if (!_registry.TryGetData(sampleEntry.Disease, out var disease))
                continue;

            entries.Add(CreateEntry(sampleEntry, disease, level));
        }

        return new DiseaseAnalysisResult(level, sample.Comp.SubjectName, entries);
    }

    /// <summary>
    /// Код штамма: его выдаёт реестр при заведении болезни, поэтому он одинаков во всех отчётах.
    /// </summary>
    public string GetStrainCode(EntityUid disease)
    {
        return _registry.TryGetData(disease, out var data) ? data.StrainCode : string.Empty;
    }

    /// <summary>
    /// Разбирает сущность напрямую, без мазка: что на ней есть — болезни носителя или зараза предмета.
    /// </summary>
    /// <remarks>
    /// Так работает ручной анализатор: он смотрит на объект в поле, а не на снятый с него образец.
    /// </remarks>
    public DiseaseAnalysisResult AnalyzeTarget(EntityUid target, DiseaseAnalysisLevel level)
    {
        var entries = new List<DiseaseAnalysisEntry>();

        foreach (var instance in _disease.GetDiseases(target))
        {
            if (!_registry.TryGetData(instance.Disease, out var disease))
                continue;

            var symptoms = new List<ProtoId<DiseaseSymptomPrototype>>(instance.Symptoms.Count);
            foreach (var active in instance.Symptoms)
            {
                symptoms.Add(active.Symptom);
            }

            entries.Add(CreateEntry(new DiseaseSampleEntry(instance.Disease, instance.Stage, symptoms), disease, level));
        }

        if (TryComp<DiseaseSourceComponent>(target, out var source))
        {
            foreach (var diseaseId in _source.GetDiseases((target, source)))
            {
                if (!_registry.TryGetData(diseaseId, out var disease))
                    continue;

                entries.Add(CreateEntry(
                    new DiseaseSampleEntry(diseaseId, null, new List<ProtoId<DiseaseSymptomPrototype>>()),
                    disease,
                    level));
            }
        }

        return new DiseaseAnalysisResult(level, Name(target), entries);
    }

    private DiseaseAnalysisEntry CreateEntry(
        DiseaseSampleEntry sampleEntry,
        DiseaseDefinitionComponent disease,
        DiseaseAnalysisLevel level)
    {
        var symptoms = new List<LocId>(sampleEntry.Symptoms.Count);
        foreach (var symptomId in sampleEntry.Symptoms)
        {
            if (_proto.TryIndex(symptomId, out var symptom))
                symptoms.Add(symptom.Name);
        }

        DiseaseClassification? classification = level >= DiseaseAnalysisLevel.Classification
            ? disease.Classification
            : null;

        if (level < DiseaseAnalysisLevel.Exact)
            return new DiseaseAnalysisEntry(disease.Syndrome, symptoms, classification, null, null, new List<LocId>());

        return new DiseaseAnalysisEntry(
            disease.Syndrome,
            symptoms,
            classification,
            GetStrainCode(sampleEntry.Disease),
            sampleEntry.Stage,
            new List<LocId>(disease.TreatmentHints));
    }

    /// <summary>
    /// Собирает текст отчёта по результату анализа.
    /// </summary>
    /// <remarks>
    /// Общий для стационарного диагностера и ручного анализатора: у отчётов должен быть один вид,
    /// по какому бы прибору их ни печатали.
    /// </remarks>
    public string BuildReport(DiseaseAnalysisResult result)
    {
        var report = new StringBuilder();
        report.AppendLine(Loc.GetString("disease-report-header", ("subject", result.SubjectName)));

        if (result.Entries.Count == 0)
        {
            report.AppendLine(Loc.GetString("disease-report-negative"));
            return report.ToString();
        }

        foreach (var entry in result.Entries)
        {
            report.AppendLine();
            report.AppendLine(Loc.GetString("disease-report-syndrome", ("syndrome", Loc.GetString(entry.Syndrome))));

            if (entry.Symptoms.Count > 0)
            {
                var symptoms = new List<string>(entry.Symptoms.Count);
                foreach (var symptom in entry.Symptoms)
                {
                    symptoms.Add(Loc.GetString(symptom));
                }

                report.AppendLine(Loc.GetString("disease-report-symptoms", ("symptoms", string.Join(", ", symptoms))));
            }

            if (entry.Classification is { } classification)
            {
                report.AppendLine(Loc.GetString("disease-report-classification",
                    ("class", Loc.GetString(DiseaseLocalization.GetClassificationLocId(classification)))));
            }

            if (entry.StrainCode is { } strain)
                report.AppendLine(Loc.GetString("disease-report-strain", ("strain", strain)));

            if (entry.Stage is { } stage)
                report.AppendLine(Loc.GetString("disease-report-stage", ("stage", stage + 1)));

            foreach (var hint in entry.TreatmentHints)
            {
                report.AppendLine(Loc.GetString("disease-report-hint", ("hint", Loc.GetString(hint))));
            }
        }

        return report.ToString();
    }
}
