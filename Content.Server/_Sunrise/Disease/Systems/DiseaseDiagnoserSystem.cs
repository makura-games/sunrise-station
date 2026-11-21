using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease;
using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Paper;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Анализирует вставленный мазок и печатает отчёт. Образец при этом не тратится,
/// поэтому его можно отнести в вакцинатор.
/// </summary>
public sealed partial class DiseaseDiagnoserSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    [Dependency] private EntityQuery<DiseaseSampleComponent> _sampleQuery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveDiseaseDiagnoserComponent, DiseaseDiagnoserComponent>();
        while (query.MoveNext(out var uid, out var active, out var diagnoser))
        {
            if (active.FinishAt > curTime)
                continue;

            RemComp<ActiveDiseaseDiagnoserComponent>(uid);
            PrintReport((uid, diagnoser));
        }
    }

    [SubscribeLocalEvent]
    private void OnSampleInserted(Entity<DiseaseDiagnoserComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SampleSlotId)
            return;

        TryStartAnalysis(ent);
    }

    [SubscribeLocalEvent]
    private void OnSampleRemoved(Entity<DiseaseDiagnoserComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SampleSlotId)
            return;

        RemComp<ActiveDiseaseDiagnoserComponent>(ent);
        SetRunning(ent, false);
    }

    /// <summary>
    /// Запускает анализ вставленного образца.
    /// </summary>
    /// <returns>True, если анализ начался.</returns>
    public bool TryStartAnalysis(Entity<DiseaseDiagnoserComponent> ent)
    {
        if (!CanAnalyze(ent))
            return false;

        var active = EnsureComp<ActiveDiseaseDiagnoserComponent>(ent);
        active.FinishAt = _timing.CurTime + ent.Comp.PrintDelay;
        SetRunning(ent, true);
        _audio.PlayPvs(ent.Comp.RunningSound, ent.Owner);
        return true;
    }

    /// <summary>
    /// Проверяет, может ли машина начать анализ.
    /// </summary>
    public bool CanAnalyze(Entity<DiseaseDiagnoserComponent> ent)
    {
        if (!_power.IsPowered(ent.Owner))
            return false;

        if (HasComp<ActiveDiseaseDiagnoserComponent>(ent))
            return false;

        var sample = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId);
        return sample != null && _sampleQuery.HasComp(sample.Value);
    }

    private void PrintReport(Entity<DiseaseDiagnoserComponent> ent)
    {
        SetRunning(ent, false);

        var sampleUid = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId);
        if (sampleUid is not { } sample || !_sampleQuery.TryComp(sample, out var sampleComp))
            return;

        var result = _analysis.Analyze((sample, sampleComp), ent.Comp.AnalysisLevel);

        // Данные дела пополняются из образца: симптомы пациента и, при точном анализе, класс возбудителя.
        var revealClassification = ent.Comp.AnalysisLevel >= DiseaseAnalysisLevel.Classification;
        foreach (var entry in sampleComp.Entries)
        {
            _research.RevealFacts(entry.Disease, entry.Symptoms, revealClassification);
        }

        GrantResearch(ent, sampleComp);

        var paper = Spawn(ent.Comp.ReportPrototype, Transform(ent).Coordinates);
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), _analysis.BuildReport(result));

        _metaData.SetEntityName(paper, Loc.GetString("disease-report-title", ("subject", result.SubjectName)));
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
    }

    /// <summary>
    /// Начисляет прогресс исследования за образец.
    /// </summary>
    /// <remarks>
    /// Образец не расходуется, поэтому очки выдаются один раз: иначе один мазок можно было бы
    /// перепечатывать до полного исследования штамма.
    /// </remarks>
    private void GrantResearch(Entity<DiseaseDiagnoserComponent> ent, DiseaseSampleComponent sample)
    {
        if (sample.Counted)
            return;

        sample.Counted = true;

        var culture = sample.Kind == DiseaseSampleKind.Culture;
        var points = culture ? ent.Comp.CultureResearchPoints : ent.Comp.HostResearchPoints;

        foreach (var entry in sample.Entries)
        {
            _research.AddResearch(entry.Disease, points);

            // У культуры носителя нет, зато лаборатория видит, на что штамм способен.
            if (culture)
                _research.TryRevealRandomSymptom(entry.Disease);
        }
    }

    private void SetRunning(EntityUid uid, bool running)
    {
        _appearance.SetData(uid, PowerDeviceVisuals.VisualState, running);
    }
}
