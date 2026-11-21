using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Vaccinator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Запуск вакцинатора предсказывается: таймер, анимация, звук, расход сырья и образца появляются сразу.
/// </summary>
/// <remarks>
/// Удалась ли проба и что машина выдаст, знает только сервер, поэтому сам цикл он запускает
/// в <see cref="OnSynthesize"/>, <see cref="OnProduce"/> и <see cref="OnExtractSerum"/>, после чего
/// пересобирает состояние панели. Предсказание опирается только на реплицированную панель машины.
/// </remarks>
public abstract partial class SharedVaccinatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<VaccinatorComponent>(VaccinatorUiKey.Key, subs =>
        {
            subs.Event<VaccinatorSynthesizeMessage>(OnSynthesizeMessage);
            subs.Event<VaccinatorProduceMessage>(OnProduceMessage);
            subs.Event<VaccinatorExtractSerumMessage>(OnExtractSerumMessage);
        });
    }

    private void OnSynthesizeMessage(Entity<VaccinatorComponent> ent, ref VaccinatorSynthesizeMessage args)
    {
        var comp = ent.Comp;
        if (!CanStart(ent, comp.TrialCost) || !comp.HasSample ||
            args.AntigenIndex < 0 || args.AntigenIndex >= comp.Candidates.Count)
        {
            return;
        }

        var sample = _itemSlots.GetItemOrNull(ent.Owner, comp.SampleSlotId);
        StartPredicted(ent, comp.TrialCost, args.Actor);
        OnSynthesize(ent, args.AntigenIndex);

        // Проба расходует образец независимо от результата: промах стоит нового мазка.
        if (sample != null)
            PredictedQueueDel(sample.Value);
    }

    private void OnProduceMessage(Entity<VaccinatorComponent> ent, ref VaccinatorProduceMessage args)
    {
        var comp = ent.Comp;
        if (!CanStart(ent, comp.ProductionCost) || !comp.HasSample || comp.SolvedCode == null)
            return;

        StartPredicted(ent, comp.ProductionCost, args.Actor);
        OnProduce(ent);
    }

    private void OnExtractSerumMessage(Entity<VaccinatorComponent> ent, ref VaccinatorExtractSerumMessage args)
    {
        var comp = ent.Comp;
        if (!CanStart(ent, comp.ProductionCost) || comp.SerumStatus != DiseaseSerumStatus.Ready ||
            args.OptionIndex < 0 || args.OptionIndex >= comp.SerumOptions.Count)
        {
            return;
        }

        StartPredicted(ent, comp.ProductionCost, args.Actor);
        OnExtractSerum(ent, args.OptionIndex);
    }

    /// <summary>
    /// Может ли машина начать цикл: запитана, свободна и сырья хватает.
    /// </summary>
    public bool CanStart(Entity<VaccinatorComponent> ent, int cost)
    {
        return _power.IsPowered(ent.Owner) && ent.Comp.FinishAt == null && ent.Comp.MaterialAmount >= cost;
    }

    /// <summary>
    /// Предсказанная часть запуска: машина видимо занята, сырьё списано на панели, звучит работа.
    /// </summary>
    private void StartPredicted(Entity<VaccinatorComponent> ent, int cost, EntityUid user)
    {
        ent.Comp.FinishAt = _timing.CurTime + ent.Comp.SynthesisDelay;
        ent.Comp.MaterialAmount -= cost;
        Dirty(ent);

        _appearance.SetData(ent.Owner, PowerDeviceVisuals.VisualState, true);
        _audio.PlayPredicted(ent.Comp.RunningSound, ent.Owner, user);
        UpdateUi(ent);
    }

    /// <summary>Серверная часть пробного синтеза по выбранному антигену.</summary>
    protected virtual void OnSynthesize(Entity<VaccinatorComponent> ent, int antigenIndex)
    {
    }

    /// <summary>Серверная часть выпуска дозы по найденному рецепту.</summary>
    protected virtual void OnProduce(Entity<VaccinatorComponent> ent)
    {
    }

    /// <summary>Серверная часть выделения сыворотки из крови донора.</summary>
    protected virtual void OnExtractSerum(Entity<VaccinatorComponent> ent, int optionIndex)
    {
    }

    /// <summary>Перерисовывает открытое окно машины. На клиенте — сразу после предсказанного запуска.</summary>
    protected virtual void UpdateUi(Entity<VaccinatorComponent> ent)
    {
    }
}
