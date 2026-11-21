using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Shared._Sunrise.Disease;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.GameTicking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>Превращает потенциал изучения сконструированных культур в очки исследования.</summary>
public sealed partial class DiseaseResearchVesselSystem : SharedDiseaseResearchVesselSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseStrengthSystem _strength = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private EntityQuery<ResearchClientComponent> _clientQuery = default!;
    [Dependency] private EntityQuery<ResearchServerComponent> _serverQuery = default!;
    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _sampleQuery = default!;

    // Запас привязан к сущности штамма, а не пробирке: копирование и перестановка его не обновляют.
    private readonly Dictionary<EntityUid, int> _remainingSeconds = new();
    private readonly HashSet<EntityUid> _studiedThisUpdate = new();
    private TimeSpan _nextUpdate;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseResearchVesselComponent> ent, ref MapInitEvent args)
    {
        RefreshAppearance(ent, false);
        UpdateExamineState(ent);
    }

    [SubscribeLocalEvent]
    private void OnItemInserted(Entity<DiseaseResearchVesselComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    [SubscribeLocalEvent]
    private void OnItemRemoved(Entity<DiseaseResearchVesselComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        SlotChanged(ent, args.Container.ID);
    }

    /// <summary>Содержимое слота образца изменилось: вид и описание сосуда пересобираются.</summary>
    private void SlotChanged(Entity<DiseaseResearchVesselComponent> ent, string container)
    {
        if (container != ent.Comp.SampleSlotId)
            return;

        RefreshAppearance(ent, false);
        UpdateExamineState(ent);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<DiseaseResearchVesselComponent> ent, ref PowerChangedEvent args)
    {
        RefreshAppearance(ent, false);
        UpdateExamineState(ent);
    }

    [SubscribeLocalEvent]
    private void OnRegistrationChanged(Entity<DiseaseResearchVesselComponent> ent, ref ResearchRegistrationChangedEvent args)
    {
        RefreshAppearance(ent, false);
        UpdateExamineState(ent);
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _remainingSeconds.Clear();
        _studiedThisUpdate.Clear();
        _nextUpdate = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_nextUpdate > _timing.CurTime)
            return;

        // Простой и отсутствие питания не копят выплату задним числом.
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        _studiedThisUpdate.Clear();
        var query = EntityQueryEnumerator<DiseaseResearchVesselComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            var active = TryStudy(ent);
            RefreshAppearance(ent, active);
        }

        // Сначала списываем общий запас: у всех копий штамма в описании должен быть одинаковый остаток.
        query = EntityQueryEnumerator<DiseaseResearchVesselComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateExamineState((uid, comp));
        }
    }

    /// <summary>Снимает одну секунду данных исследования с подключённого сосуда под питанием.</summary>
    public bool TryStudy(Entity<DiseaseResearchVesselComponent> ent)
    {
        if (!CanStudy(ent, out var disease, out var server))
            return false;

        var seconds = GetRemainingSeconds(ent, disease);
        if (seconds <= 0 || !_studiedThisUpdate.Add(disease))
            return false;

        _remainingSeconds[disease] = seconds - 1;
        _research.ModifyServerResearchPoints(server, GetPointsPerSecond(ent, disease));
        return true;
    }

    /// <summary>
    /// Сломанный или разрушенный сосуд выпускает культуру большим облаком.
    /// </summary>
    /// <remarks>
    /// Пробирка гибнет при первой же аварии, поэтому поломка и последующее разрушение не дают двух облаков.
    /// Станционную вспышку поломка не запускает: её приносит только случайное событие разгерметизации.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnBreakage(Entity<DiseaseResearchVesselComponent> ent, ref BreakageEventArgs args)
    {
        TryBreach(ent, out _);
    }

    [SubscribeLocalEvent]
    private void OnDestruction(Entity<DiseaseResearchVesselComponent> ent, ref DestructionEventArgs args)
    {
        TryBreach(ent, out _);
    }

    /// <summary>
    /// Есть ли в сосуде культура, которую может разгерметизировать станционное событие.
    /// </summary>
    /// <remarks>
    /// Полезные штаммы событие не трогает: вспышка заражает экипаж напрямую, и баффы раздавались бы даром.
    /// </remarks>
    public bool CanBreach(Entity<DiseaseResearchVesselComponent> ent)
    {
        return TryGetSample(ent, out var disease) && !_registry.IsBeneficial(disease);
    }

    /// <summary>
    /// Разгерметизирует сосуд: культура вырывается облаком, пробирка с ней гибнет.
    /// </summary>
    /// <remarks>
    /// Обычная культура заражает из облака, даже если сам штамм по воздуху не передаётся.
    /// Полезный штамм такой поблажки не получает: из облака он заражает, только если воздушный путь
    /// ему развили на взломанном микроскопе. Иначе авария стала бы бесплатной раздачей баффов.
    /// </remarks>
    /// <param name="disease">Штамм, который вырвался наружу.</param>
    /// <returns>True, если в сосуде была культура и она вырвалась наружу.</returns>
    public bool TryBreach(Entity<DiseaseResearchVesselComponent> ent, out EntityUid disease)
    {
        disease = default;
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId) is not { } sample ||
            !TryGetDesignedStrain(sample, out var strain))
            return false;

        QueueDel(sample);
        _popup.PopupEntity(Loc.GetString("disease-vessel-popup-breach"), ent, PopupType.LargeCaution);

        var cloud = Spawn(ent.Comp.BreachCloudPrototype, Transform(ent).Coordinates);
        var cloudComp = EnsureComp<DiseaseCloudComponent>(cloud);
        cloudComp.Disease = strain;
        if (_registry.IsBeneficial(strain))
            cloudComp.IgnoreRoutes = false;

        _adminLog.Add(LogType.Disease, LogImpact.High,
            $"{ToPrettyString(ent.Owner):vessel} breached and released designed strain {ToPrettyString(strain)}");

        disease = strain;
        return true;
    }

    /// <summary>Проверяет образец, питание машины и выбранный сервер исследований.</summary>
    public bool CanStudy(Entity<DiseaseResearchVesselComponent> ent, out EntityUid disease, out EntityUid server)
    {
        disease = server = default;
        if (TerminatingOrDeleted(ent) || IsPaused(ent) || !_power.IsPowered(ent.Owner) ||
            !TryGetSample(ent, out disease) || !_clientQuery.TryComp(ent.Owner, out var client) ||
            client.Server is not { } selected || TerminatingOrDeleted(selected) || IsPaused(selected) ||
            !_serverQuery.HasComp(selected) || !_power.IsPowered(selected))
            return false;

        server = selected;
        return true;
    }

    private bool TryGetSample(Entity<DiseaseResearchVesselComponent> ent, out EntityUid disease)
    {
        disease = default;
        return _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SampleSlotId) is { } sample &&
               TryGetDesignedStrain(sample, out disease);
    }

    private bool TryGetDesignedStrain(EntityUid sample, out EntityUid disease)
    {
        disease = default;
        if (TerminatingOrDeleted(sample) || !_sampleQuery.TryComp(sample, out var comp) ||
            comp.Disease is not { } strain || !_registry.TryGetData(strain, out var definition) || !definition.Designed)
            return false;

        disease = strain;
        return true;
    }

    private int GetPointsPerSecond(Entity<DiseaseResearchVesselComponent> ent, EntityUid disease)
    {
        return Math.Clamp((int)MathF.Round(_strength.GetStrength(disease) * ent.Comp.PointsPerStrength),
            1, Math.Max(1, ent.Comp.MaxPointsPerSecond));
    }

    private int GetRemainingSeconds(Entity<DiseaseResearchVesselComponent> ent, EntityUid disease)
    {
        if (_remainingSeconds.TryGetValue(disease, out var remaining))
            return remaining;

        if (!_registry.TryGetData(disease, out var definition))
            return 0;

        var resistance = float.IsFinite(definition.Resistance) ? Math.Clamp(definition.Resistance, 0f, 1f) : 0f;
        var minimum = Math.Max(0, ent.Comp.MinStudySeconds);
        var maximum = Math.Max(minimum, ent.Comp.MaxStudySeconds);
        return _remainingSeconds[disease] = (int)MathF.Round(minimum + (maximum - minimum) * resistance);
    }

    private void RefreshAppearance(Entity<DiseaseResearchVesselComponent> ent, bool active)
    {
        _appearance.SetData(ent.Owner, DiseaseResearchVesselVisuals.Loaded, TryGetSample(ent, out _));
        _appearance.SetData(ent.Owner, DiseaseResearchVesselVisuals.Active, active);
    }

    private void UpdateExamineState(Entity<DiseaseResearchVesselComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var comp = ent.Comp;
        var hasSample = TryGetSample(ent, out var disease);
        var strainCode = string.Empty;
        var strength = 0;
        var resistance = 0;
        var rate = 0;
        var remaining = 0;
        var paused = !CanStudy(ent, out _, out _);

        if (hasSample && _registry.TryGetData(disease, out var definition))
        {
            strainCode = definition.StrainCode;
            strength = _strength.GetStrength(disease);
            resistance = float.IsFinite(definition.Resistance)
                ? (int)MathF.Round(Math.Clamp(definition.Resistance, 0f, 1f) * 100f)
                : 0;
            rate = GetPointsPerSecond(ent, disease);
            remaining = GetRemainingSeconds(ent, disease);
        }

        // При обычном ходе исследования меняется только остаток, поэтому отправляем дельту одного поля.
        if (comp.RemainingSeconds != remaining)
        {
            comp.RemainingSeconds = remaining;
            DirtyField(ent, comp, nameof(DiseaseResearchVesselComponent.RemainingSeconds));
        }

        if (comp.HasSample == hasSample && comp.StrainCode == strainCode && comp.Strength == strength &&
            comp.ResistancePercent == resistance && comp.PointsPerSecond == rate && comp.StudyPaused == paused)
        {
            return;
        }

        comp.HasSample = hasSample;
        comp.StrainCode = strainCode;
        comp.Strength = strength;
        comp.ResistancePercent = resistance;
        comp.PointsPerSecond = rate;
        comp.StudyPaused = paused;
        Dirty(ent);
    }
}
