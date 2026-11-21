using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>Ведёт развитие штамма по стадиям: каждая стадия разыгрывается партией против колонии-конкурента.</summary>
public sealed partial class DiseaseMicroscopeSystem : SharedDiseaseMicroscopeSystem
{
    // Жизненный цикл слота и синхронизация состояния партии.
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseBlankSampleSystem _blankSamples = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _sampleQuery = default!;

    /// <summary>
    /// Вес каждого развитого симптома. На проявление он не влияет: стадия выведенного штамма
    /// проявляет все свои симптомы, а вес нужен лишь пулу стадии, где пустой вес означает «не выпадает».
    /// </summary>
    private const float DevelopedWeight = 1f;

    public override void Initialize()
    {
        base.Initialize();


        Subs.BuiEvents<DiseaseMicroscopeComponent>(DiseaseMicroscopeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DiseaseMicroscopeFinishMessage>(OnFinish);
            subs.Event<DiseaseMicroscopeAdvanceMessage>(OnAdvance);
            subs.Event<DiseaseMicroscopeProduceMessage>(OnProduce);
        });
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseMicroscopeComponent> ent, ref MapInitEvent args)
    {
        CompleteCommand(ent, "ready");
    }

    [SubscribeLocalEvent]
    private void OnItemInserted(Entity<DiseaseMicroscopeComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    [SubscribeLocalEvent]
    private void OnItemRemoved(Entity<DiseaseMicroscopeComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    /// <summary>Содержимое слота изменилось: панель пересобирается.</summary>
    private void SlotChanged(Entity<DiseaseMicroscopeComponent> ent, string container)
    {
        if (container == ent.Comp.SampleSlotId)
            RefreshAndPublish(ent);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<DiseaseMicroscopeComponent> ent, ref PowerChangedEvent args)
    {
        RefreshAndPublish(ent);
    }

    [SubscribeLocalEvent]
    private void OnSolutionChanged(Entity<DiseaseMicroscopeComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id == ent.Comp.SolutionName)
            RefreshAndPublish(ent);
    }

    private void OnUiOpened(Entity<DiseaseMicroscopeComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshAndPublish(ent);
    }

    private void OnFinish(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeFinishMessage args)
    {
        TryFinish(ent, args.Revision);
    }

    private void OnAdvance(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeAdvanceMessage args)
    {
        TryAdvance(ent, args.Revision);
    }

    private void OnProduce(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeProduceMessage args)
    {
        TryProduce(ent, args.Revision, args.Actor);
    }

    private void RefreshAndPublish(Entity<DiseaseMicroscopeComponent> ent)
    {
        if (!RefreshState(ent))
            return;

        ent.Comp.Revision = unchecked(ent.Comp.Revision + 1);
        Dirty(ent);
    }

    private void CompleteCommand(Entity<DiseaseMicroscopeComponent> ent, string feedback)
    {
        RefreshState(ent);
        ent.Comp.Feedback = FeedbackPrefix + feedback;
        ent.Comp.Revision = unchecked(ent.Comp.Revision + 1);
        Dirty(ent);
    }

    private bool RefreshState(Entity<DiseaseMicroscopeComponent> ent)
    {
        var comp = ent.Comp;
        var inserted = _itemSlots.GetItemOrNull(ent.Owner, comp.SampleSlotId);
        var hasSample = TryGetSample(ent, out var sample);
        var powered = _power.IsPowered(ent.Owner);
        var strainCode = hasSample ? sample.Comp.StrainCode : string.Empty;
        var stage = hasSample ? sample.Comp.Stage : 0;
        var maxStage = hasSample ? sample.Comp.MaxStage : 0;
        var open = hasSample && sample.Comp.StageOpen && sample.Comp.Genome.Count > 0;

        // Усиление идёт по правилам следующей ступени: каждый лишний симптом стадии даётся тяжелее.
        var level = open ? stage - 1 + sample.Comp.Genome[^1].Developed : stage;
        var routeMatch = hasSample && comp.SourceSample == inserted && comp.Stage == stage &&
                         comp.MaxStage == maxStage && comp.StageOpen == open &&
                         comp.TargetRoute != DiseaseTransmissionRoute.None;
        var profile = routeMatch
            ? DiseaseCultureProfile.FromStage(2, sample.Comp.Resistance)
            : hasSample && (open || stage < maxStage)
            ? DiseaseCultureProfile.FromStage(level, sample.Comp.Resistance)
            : DiseaseCultureProfile.Empty;
        var options = hasSample
            ? open ? GetBonusOptions(sample) : GetStageOptions(sample, stage)
            : new List<ProtoId<DiseaseSymptomPrototype>>();
        var reinforcements = hasSample ? GetReinforcementsLeft(sample) : 0;
        var genome = hasSample ? CopyGenome(sample.Comp.Genome) : new List<DiseaseStagePool>();
        var nutrient = GetReagentUnits(ent, comp.NutrientReagent);
        var inhibitor = GetReagentUnits(ent, comp.InhibitorReagent);
        var capacity = GetReservoirCapacity(ent);
        var routes = hasSample ? sample.Comp.Routes : DiseaseTransmissionRoute.Injection;
        var resistance = hasSample ? sample.Comp.Resistance : 0f;

        // Партия привязана к конкретной заготовке и стадии: смена любой из них обнуляет поле.
        var reset = comp.SourceSample != inserted || comp.HasSample != hasSample ||
                    comp.Stage != stage || comp.MaxStage != maxStage || comp.Profile != profile ||
                    comp.StageOpen != open;
        NetEntity? view = inserted is { } item ? GetNetEntity(item) : null;
        var changed = reset || comp.Routes != routes || comp.SampleInserted != inserted.HasValue || comp.Powered != powered ||
                      comp.StrainCode != strainCode || comp.NutrientUnits != nutrient ||
                      comp.InhibitorUnits != inhibitor || comp.ReservoirCapacity != capacity ||
                      comp.ReinforcementsLeft != reinforcements ||
                      comp.SampleView != view || !comp.SampleResistance.Equals(resistance) ||
                      !SameSymptoms(comp.StageOptions, options) || !SameGenome(comp.Genome, genome);

        if (reset)
        {
            ResetMatch(comp);
            comp.Feedback = FeedbackPrefix + (hasSample || !inserted.HasValue ? "ready" : "invalid-sample");
        }

        comp.SourceSample = inserted;
        comp.HasSample = hasSample;
        comp.SampleInserted = inserted.HasValue;
        comp.SampleView = view;
        comp.Powered = powered;
        comp.StrainCode = strainCode;
        comp.Stage = stage;
        comp.MaxStage = maxStage;
        comp.Profile = profile;
        comp.StageOptions = options;
        comp.StageOpen = open;
        comp.ReinforcementsLeft = reinforcements;
        comp.Genome = genome;
        comp.NutrientUnits = nutrient;
        comp.InhibitorUnits = inhibitor;
        comp.ReservoirCapacity = capacity;
        comp.Routes = routes;
        comp.SampleResistance = resistance;
        return changed;
    }

    /// <summary>Варианты симптомов стадии без уже развитых.</summary>
    private static List<ProtoId<DiseaseSymptomPrototype>> GetStageOptions(
        Entity<DiseaseStrainSampleComponent> sample,
        int stage)
    {
        var options = new List<ProtoId<DiseaseSymptomPrototype>>();
        if (stage < 0 || stage >= sample.Comp.StagePools.Count || stage >= sample.Comp.MaxStage)
            return options;

        foreach (var entry in sample.Comp.StagePools[stage].Symptoms)
        {
            if (!sample.Comp.Developed.Contains(entry.Symptom) && !options.Contains(entry.Symptom))
                options.Add(entry.Symptom);
        }

        return options;
    }

    /// <summary>
    /// Кандидаты на усиление последней стадии: её варианты без уже развитых и без конкурента.
    /// </summary>
    private static List<ProtoId<DiseaseSymptomPrototype>> GetBonusOptions(Entity<DiseaseStrainSampleComponent> sample)
    {
        var options = new List<ProtoId<DiseaseSymptomPrototype>>();
        var stage = sample.Comp.Stage - 1;
        if (stage < 0 || stage >= sample.Comp.StagePools.Count || sample.Comp.Genome.Count == 0)
            return options;

        // Шире предела стадия не растёт: иначе вектор развития превращается в кашу.
        var pool = sample.Comp.Genome[^1];
        if (pool.Developed >= sample.Comp.MaxStageSymptoms || GetReinforcementsLeft(sample) <= 0)
            return options;

        foreach (var entry in sample.Comp.StagePools[stage].Symptoms)
        {
            if (options.Contains(entry.Symptom))
                continue;

            var taken = false;
            foreach (var developed in pool.Symptoms)
                taken |= developed.Symptom == entry.Symptom;

            if (!taken)
                options.Add(entry.Symptom);
        }

        return options;
    }

    /// <summary>
    /// Сколько усилений ещё допускает заготовка: запас общий на весь геном.
    /// </summary>
    public static int GetReinforcementsLeft(Entity<DiseaseStrainSampleComponent> sample)
    {
        var used = 0;
        foreach (var stage in sample.Comp.Genome)
            used += Math.Max(0, stage.Developed - 1);

        return Math.Max(0, sample.Comp.MaxReinforcements - used);
    }

    private bool TryGetSample(Entity<DiseaseMicroscopeComponent> ent, out Entity<DiseaseStrainSampleComponent> sample)
    {
        sample = default;
        return _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId) is { } item && TryGetBlank(item, out sample);
    }

    private bool TryGetBlank(EntityUid item, out Entity<DiseaseStrainSampleComponent> sample)
    {
        sample = default;
        if (!_sampleQuery.TryComp(item, out var comp) || !_blankSamples.IsPreparedBlank((item, comp)))
            return false;

        sample = (item, comp);
        return true;
    }

    /// <summary>Ёмкость резервуара машины; по ней окно рисует шкалы реагентов.</summary>
    private int GetReservoirCapacity(Entity<DiseaseMicroscopeComponent> ent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return 0;

        return solution.MaxVolume.Int();
    }

    private int GetReagentUnits(Entity<DiseaseMicroscopeComponent> ent, ProtoId<ReagentPrototype> reagent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return 0;

        return solution.GetReagentQuantity(new ReagentId(reagent.Id, null)).Int();
    }

}
