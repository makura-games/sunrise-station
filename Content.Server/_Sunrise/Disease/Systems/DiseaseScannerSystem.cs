using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть ручного анализатора: составляет отчёт по цели и выдаёт распечатку.
/// </summary>
/// <remarks>
/// Полевая разведка рядом с лабораторией: прибор говорит, что перед тобой заразно, и пополняет
/// данные дела, но очков исследования не даёт — их зарабатывают диагностером.
/// </remarks>
public sealed partial class DiseaseScannerSystem : SharedDiseaseScannerSystem
{
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private DiseaseSourceSystem _source = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void OnScanned(Entity<DiseaseScannerComponent> scanner, EntityUid user, EntityUid target)
    {
        var result = _analysis.AnalyzeTarget(target, scanner.Comp.AnalysisLevel);

        // Данные дела пополняются и с поля: увиденные симптомы и, при точном анализе, класс возбудителя.
        var revealClassification = scanner.Comp.AnalysisLevel >= DiseaseAnalysisLevel.Classification;
        foreach (var instance in _disease.GetDiseases(target))
        {
            var symptoms = new List<ProtoId<DiseaseSymptomPrototype>>(instance.Symptoms.Count);
            foreach (var active in instance.Symptoms)
            {
                symptoms.Add(active.Symptom);
            }

            _research.RevealFacts(instance.Disease, symptoms, revealClassification);
        }

        if (TryComp<DiseaseSourceComponent>(target, out var source))
        {
            foreach (var disease in _source.GetDiseases((target, source)))
            {
                _research.RevealFacts(disease, Array.Empty<ProtoId<DiseaseSymptomPrototype>>(), revealClassification);
            }
        }

        var paper = Spawn(scanner.Comp.ReportPrototype, Transform(scanner).Coordinates);
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), _analysis.BuildReport(result));

        _metaData.SetEntityName(paper, Loc.GetString("disease-report-title", ("subject", result.SubjectName)));
        _hands.PickupOrDrop(user, paper);
    }
}
