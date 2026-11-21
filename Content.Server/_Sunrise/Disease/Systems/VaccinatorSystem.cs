using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Vaccinator;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Вакцинатор: синтезирует вакцину по выбранному антигену и выделяет сыворотку из крови переболевшего.
/// Вакцина требует разгадать штамм, сыворотка — найти донора; это два независимых пути лечения.
/// </summary>
public sealed partial class VaccinatorSystem : SharedVaccinatorSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseDonorRegistrySystem _donors = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    [Dependency] private EntityQuery<DiseaseSampleComponent> _sampleQuery = default!;

    public override void Initialize()
    {
        base.Initialize();


        Subs.BuiEvents<VaccinatorComponent>(VaccinatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveVaccinatorComponent, VaccinatorComponent>();
        while (query.MoveNext(out var uid, out var active, out var vaccinator))
        {
            if (active.FinishAt > curTime)
                continue;

            var cleaning = active.Cleaning;
            if (!cleaning)
                FinishSynthesis((uid, vaccinator), active);

            RemComp<ActiveVaccinatorComponent>(uid);

            // После неудачной партии машину надо промыть: это и есть цена перебора вслепую.
            if (!cleaning && active.Disease == null)
                StartCleaning((uid, vaccinator));

            UpdateState((uid, vaccinator));
        }
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<VaccinatorComponent> ent, ref MapInitEvent args)
    {
        UpdateState(ent);
    }

    /// <summary>Содержимое слота образца или крови изменилось: панель пересобирается.</summary>
    private void SlotChanged(Entity<VaccinatorComponent> ent, string container)
    {
        if (container != ent.Comp.SampleSlotId && container != ent.Comp.BloodSlotId)
            return;

        UpdateState(ent);
    }

    [SubscribeLocalEvent]
    private void OnItemInserted(Entity<VaccinatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    [SubscribeLocalEvent]
    private void OnItemRemoved(Entity<VaccinatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    [SubscribeLocalEvent]
    private void OnMaterialChanged(Entity<VaccinatorComponent> ent, ref MaterialAmountChangedEvent args)
    {
        UpdateState(ent);
    }

    [SubscribeLocalEvent]
    private void OnResearchChanged(ref DiseaseResearchChangedEvent args)
    {
        UpdateAll();
    }

    private void OnUiOpened(Entity<VaccinatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateState(ent);
    }

    // Запуск уже предсказан общей системой: здесь сервер решает исход и пересобирает панель,
    // чтобы отменить предсказание, если цикл на деле не начался.
    protected override void OnSynthesize(Entity<VaccinatorComponent> ent, int antigenIndex)
    {
        FinishStart(ent, TrySynthesizeVaccine(ent, antigenIndex));
    }

    protected override void OnProduce(Entity<VaccinatorComponent> ent)
    {
        FinishStart(ent, TryProduceVaccine(ent));
    }

    protected override void OnExtractSerum(Entity<VaccinatorComponent> ent, int optionIndex)
    {
        FinishStart(ent, TryExtractSerum(ent, optionIndex));
    }

    private void FinishStart(Entity<VaccinatorComponent> ent, bool started)
    {
        if (!started)
            _appearance.SetData(ent, PowerDeviceVisuals.VisualState, false);

        UpdateState(ent);
    }

    /// <summary>
    /// Запускает синтез вакцины по выбранному антигену. Образец расходуется в любом случае,
    /// поэтому неудачная догадка стоит одного мазка.
    /// </summary>
    /// <returns>True, если синтез начался.</returns>
    public bool TrySynthesizeVaccine(Entity<VaccinatorComponent> ent, int antigenIndex)
    {
        if (!CanRun(ent) ||
            !TryGetSampleDisease(ent, out var disease, out var sample) ||
            !_research.HasCandidate(disease, antigenIndex))
        {
            return false;
        }

        // Сырьё списывается последним: до этой строки все отказы ничего не стоят.
        if (!TryTakeMaterial(ent, ent.Comp.TrialCost))
            return false;

        var success = _research.TrySynthesize(disease, antigenIndex);

        // Образец уходит на пробу независимо от результата: промах стоит нового мазка.
        QueueDel(sample);

        StartSynthesis(ent,
            success ? ent.Comp.VaccinePrototype : ent.Comp.FailurePrototype,
            success ? disease : null,
            _analysis.GetStrainCode(disease));

        return true;
    }

    /// <summary>
    /// Штампует дозу по уже найденному рецепту. Образец остаётся в машине и служит образцом штамма,
    /// поэтому массовая вакцинация ограничена только запасом сырья.
    /// </summary>
    /// <returns>True, если цикл начался.</returns>
    public bool TryProduceVaccine(Entity<VaccinatorComponent> ent)
    {
        if (!CanRun(ent) || !TryGetSampleDisease(ent, out var disease, out _) || !_research.IsSolved(disease))
            return false;

        if (!TryTakeMaterial(ent, ent.Comp.ProductionCost))
            return false;

        StartSynthesis(ent, ent.Comp.VaccinePrototype, disease, _analysis.GetStrainCode(disease));
        return true;
    }

    /// <summary>
    /// Запускает выделение сыворотки из крови донора.
    /// </summary>
    /// <returns>True, если выделение началось.</returns>
    public bool TryExtractSerum(Entity<VaccinatorComponent> ent, int optionIndex)
    {
        if (!CanRun(ent))
            return false;

        var options = GetSerumOptions(ent, out _);
        if (optionIndex < 0 || optionIndex >= options.Count)
            return false;

        var disease = options[optionIndex];
        if (FindBlood(ent, out var solutionEnt, out _, out var reagent) != DiseaseSerumStatus.Ready)
            return false;

        if (!TryTakeMaterial(ent, ent.Comp.ProductionCost))
            return false;

        // Удаляем по полному ReagentId: у крови в данных лежит ДНК донора.
        _solution.RemoveReagent(solutionEnt, reagent, FixedPoint2.New(ent.Comp.BloodCost));
        StartSynthesis(ent, ent.Comp.SerumPrototype, disease, _analysis.GetStrainCode(disease));
        return true;
    }

    /// <summary>
    /// Проверяет, может ли машина начать работу.
    /// </summary>
    public bool CanRun(Entity<VaccinatorComponent> ent)
    {
        return _power.IsPowered(ent.Owner) && !HasComp<ActiveVaccinatorComponent>(ent);
    }

    /// <summary>
    /// Списывает сырьё на один цикл.
    /// </summary>
    /// <returns>False, если сырья не хватает.</returns>
    private bool TryTakeMaterial(Entity<VaccinatorComponent> ent, int cost)
    {
        return _materialStorage.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -cost);
    }

    private void StartSynthesis(Entity<VaccinatorComponent> ent, EntProtoId result, EntityUid? disease, string strainCode)
    {
        var active = EnsureComp<ActiveVaccinatorComponent>(ent);
        active.FinishAt = _timing.CurTime + ent.Comp.SynthesisDelay;
        active.Result = result;
        active.Disease = disease;
        active.StrainCode = strainCode;

        // Звук запуска уже предсказан общей системой для нажавшего и разослан остальным.
        _appearance.SetData(ent, PowerDeviceVisuals.VisualState, true);
        UpdateState(ent);
    }

    /// <summary>
    /// Запускает промывку после неудачной партии: машина занята и ничего не выдаст.
    /// </summary>
    private void StartCleaning(Entity<VaccinatorComponent> ent)
    {
        if (ent.Comp.FailureCooldown <= TimeSpan.Zero)
            return;

        var active = EnsureComp<ActiveVaccinatorComponent>(ent);
        active.FinishAt = _timing.CurTime + ent.Comp.FailureCooldown;
        active.Cleaning = true;
        active.Disease = null;

        _appearance.SetData(ent, PowerDeviceVisuals.VisualState, true);
        _audio.PlayPvs(ent.Comp.RunningSound, ent.Owner);
    }

    private void FinishSynthesis(Entity<VaccinatorComponent> ent, ActiveVaccinatorComponent active)
    {
        _appearance.SetData(ent, PowerDeviceVisuals.VisualState, false);

        var product = Spawn(active.Result, Transform(ent).Coordinates);
        if (active.Disease is not { } disease || !TryComp<DiseaseVaccineComponent>(product, out var vaccine))
            return;

        vaccine.Disease = disease;
        vaccine.StrainCode = active.StrainCode;
        Dirty(product, vaccine);
        _label.Label(product, active.StrainCode);
    }

    /// <summary>
    /// Переписывает в компонент всё, что показывает панель, и помечает его изменённым.
    /// </summary>
    /// <remarks>
    /// Данные едут состоянием компонента, а не состоянием интерфейса: клиент рисует окно
    /// из того, что у него уже есть, и обновляет его сам, как только приходит новое состояние.
    /// </remarks>
    private void UpdateState(Entity<VaccinatorComponent> ent)
    {
        DiseaseResearchState? research = null;
        if (TryGetSampleDisease(ent, out var disease, out _))
            research = _research.GetResearchState(disease);

        ent.Comp.HasSample = research != null;
        ent.Comp.StrainCode = research?.StrainCode ?? string.Empty;
        // Списки пересобираются целиком: состояние хранит ту же ссылку, менять их на месте нельзя.
        ent.Comp.Candidates = research?.Candidates ?? new List<DiseaseVaccineCandidate>();
        ent.Comp.SolvedCode = research?.SolvedCode;
        ent.Comp.Research = research?.Research ?? 0;
        ent.Comp.ResearchGoal = research?.ResearchGoal ?? 0;
        ent.Comp.HasData = research?.HasData ?? false;
        ent.Comp.MissingFacts = research?.MissingFacts ?? 0;

        var options = GetSerumOptions(ent, out var serumStatus);
        var codes = new List<string>(options.Count);
        foreach (var option in options)
        {
            codes.Add(_analysis.GetStrainCode(option));
        }

        ent.Comp.SerumStatus = serumStatus;
        ent.Comp.SerumOptions = codes;
        ent.Comp.MaterialAmount = _materialStorage.GetMaterialAmount(ent.Owner, ent.Comp.Material);
        var busy = TryComp<ActiveVaccinatorComponent>(ent, out var active);
        ent.Comp.FinishAt = busy ? active!.FinishAt : null;
        ent.Comp.Cleaning = busy && active!.Cleaning;

        Dirty(ent);
    }

    /// <summary>
    /// Обновляет состояние всех вакцинаторов станции.
    /// </summary>
    /// <remarks>
    /// Дело по штамму общее, поэтому новый отчёт меняет картину во всех машинах сразу,
    /// даже если рядом с ними никого нет.
    /// </remarks>
    private void UpdateAll()
    {
        var query = EntityQueryEnumerator<VaccinatorComponent>();
        while (query.MoveNext(out var uid, out var vaccinator))
        {
            UpdateState((uid, vaccinator));
        }
    }

    /// <summary>
    /// Определяет штамм по вставленному мазку. Берётся первая найденная болезнь образца.
    /// </summary>
    private bool TryGetSampleDisease(Entity<VaccinatorComponent> ent, out EntityUid disease, out EntityUid sample)
    {
        disease = default;
        sample = default;

        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId) is not { } item ||
            !_sampleQuery.TryComp(item, out var sampleComp) ||
            sampleComp.Entries.Count == 0)
        {
            return false;
        }

        sample = item;
        disease = sampleComp.Entries[0].Disease;
        return true;
    }

    /// <summary>
    /// Возвращает болезни, против которых годится кровь во вставленной ёмкости.
    /// </summary>
    private List<EntityUid> GetSerumOptions(Entity<VaccinatorComponent> ent, out DiseaseSerumStatus status)
    {
        var options = new List<EntityUid>();

        status = FindBlood(ent, out _, out var dna, out _);
        if (status != DiseaseSerumStatus.Ready || dna == null)
            return options;

        foreach (var disease in _donors.GetDonorDiseases(dna))
        {
            options.Add(disease);
        }

        // Кровь годная, но её донор ничем не болел: сыворотку выделять не из чего.
        if (options.Count == 0)
            status = DiseaseSerumStatus.UnknownDonor;

        return options;
    }

    /// <summary>
    /// Ищет во вставленной ёмкости кровь с данными донора и достаточным объёмом.
    /// </summary>
    /// <returns>
    /// <see cref="DiseaseSerumStatus.Ready"/>, если кровь найдена; иначе причина, по которой она не годится.
    /// </returns>
    private DiseaseSerumStatus FindBlood(
        Entity<VaccinatorComponent> ent,
        out Entity<SolutionComponent> solutionEnt,
        out string? dna,
        out ReagentId reagent)
    {
        solutionEnt = default;
        dna = null;
        reagent = default;

        var container = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.BloodSlotId);
        if (container == null)
            return DiseaseSerumStatus.NoContainer;

        var required = FixedPoint2.New(ent.Comp.BloodCost);
        var anyBlood = false;

        foreach (var (_, solution) in _solution.EnumerateSolutions(container.Value))
        {
            foreach (var quantity in solution.Comp.Solution.Contents)
            {
                // Кровь ищется по меткам донора, а не по названию реагента: у каждого вида она своя,
                // от Blood у людей до AmmoniaBlood у воксов. Данные ДНК есть только у крови.
                if (!TryGetDonorDna(quantity.Reagent, out var donorDna))
                    continue;

                anyBlood = true;

                if (quantity.Quantity < required)
                    continue;

                solutionEnt = solution;
                dna = donorDna;
                reagent = quantity.Reagent;
                return DiseaseSerumStatus.Ready;
            }
        }

        return anyBlood ? DiseaseSerumStatus.NotEnoughBlood : DiseaseSerumStatus.NoBlood;
    }

    /// <summary>
    /// Достаёт ДНК донора из данных реагента крови.
    /// </summary>
    private static bool TryGetDonorDna(ReagentId reagent, out string? dna)
    {
        dna = null;

        if (reagent.Data is not { } data)
            return false;

        foreach (var reagentData in data)
        {
            if (reagentData is not DnaData dnaData)
                continue;

            dna = dnaData.DNA;
            return true;
        }

        return false;
    }
}
