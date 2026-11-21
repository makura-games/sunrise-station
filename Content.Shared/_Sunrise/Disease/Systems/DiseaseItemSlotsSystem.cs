using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Регистрирует слоты вирусологических машин на обеих сторонах: вставка, извлечение и доступность
/// вербов предсказываются на клиенте. Анализ мазков и производство лекарств остаются на сервере.
/// </summary>
/// <remarks>
/// Негодный образец отсекается здесь, а не на сервере, именно ради предсказания: иначе клиент
/// успевал вставить пробирку в слот, и через тик сервер возвращал её обратно в руки.
/// Решение принимается по реплицированным признакам образца — списки симптомов клиенту не уходят.
/// </remarks>
public sealed partial class DiseaseItemSlotsSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _sampleQuery = default!;

    [SubscribeLocalEvent]
    private void OnDiagnoserInit(Entity<DiseaseDiagnoserComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, ent.Comp.SampleSlotId, ent.Comp.SampleSlot);
    }

    [SubscribeLocalEvent]
    private void OnDiagnoserShutdown(Entity<DiseaseDiagnoserComponent> ent, ref ComponentShutdown args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.SampleSlot);
    }

    [SubscribeLocalEvent]
    private void OnVaccinatorInit(Entity<VaccinatorComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, ent.Comp.SampleSlotId, ent.Comp.SampleSlot);
        _itemSlots.AddItemSlot(ent.Owner, ent.Comp.BloodSlotId, ent.Comp.BloodSlot);
    }

    [SubscribeLocalEvent]
    private void OnVaccinatorShutdown(Entity<VaccinatorComponent> ent, ref ComponentShutdown args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.SampleSlot);
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.BloodSlot);
    }

    [SubscribeLocalEvent]
    private void OnMicroscopeInit(Entity<DiseaseMicroscopeComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, ent.Comp.SampleSlotId, ent.Comp.SampleSlot);
    }

    [SubscribeLocalEvent]
    private void OnMicroscopeShutdown(Entity<DiseaseMicroscopeComponent> ent, ref ComponentShutdown args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.SampleSlot);
    }

    [SubscribeLocalEvent]
    private void OnVesselInit(Entity<DiseaseResearchVesselComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, ent.Comp.SampleSlotId, ent.Comp.SampleSlot);
    }

    [SubscribeLocalEvent]
    private void OnVesselShutdown(Entity<DiseaseResearchVesselComponent> ent, ref ComponentShutdown args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.SampleSlot);
    }

    /// <summary>Микроскоп принимает только подготовленную заготовку, ещё без штамма.</summary>
    [SubscribeLocalEvent]
    private void OnMicroscopeInsertAttempt(Entity<DiseaseMicroscopeComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.SlotEntity != ent.Owner || args.Slot.ID != ent.Comp.SampleSlotId)
            return;

        if (!IsBlank(args.Item))
            args.Cancelled = true;
    }

    /// <summary>
    /// То же решение на уровне контейнера: по этому пути пробирку кладут мимо меню слота.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMicroscopeContainerInsertAttempt(Entity<DiseaseMicroscopeComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.SampleSlotId && !IsBlank(args.EntityUid))
            args.Cancel();
    }

    /// <summary>Сосуд для исследования принимает только выведенный в лаборатории штамм.</summary>
    [SubscribeLocalEvent]
    private void OnVesselInsertAttempt(Entity<DiseaseResearchVesselComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.SlotEntity != ent.Owner || args.Slot.ID != ent.Comp.SampleSlotId)
            return;

        if (!IsDesignedStrain(args.Item))
            args.Cancelled = true;
    }

    /// <inheritdoc cref="OnMicroscopeContainerInsertAttempt"/>
    [SubscribeLocalEvent]
    private void OnVesselContainerInsertAttempt(Entity<DiseaseResearchVesselComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.SampleSlotId && !IsDesignedStrain(args.EntityUid))
            args.Cancel();
    }

    /// <summary>
    /// Готовая заготовка под селекцию.
    /// </summary>
    /// <remarks>
    /// Списки симптомов заготовки не реплицируются, поэтому решение принимается по признаку
    /// готовности: его выставляет сервер, когда разыгрывает заготовку.
    /// </remarks>
    private bool IsBlank(EntityUid item)
    {
        return _sampleQuery.TryComp(item, out var sample) && sample.IsBlank && !sample.HasStrain;
    }

    /// <summary>Пробирка с выведенным штаммом: природный штамм в сосуд не идёт.</summary>
    private bool IsDesignedStrain(EntityUid item)
    {
        return _sampleQuery.TryComp(item, out var sample) && sample.HasStrain && sample.IsDesigned;
    }
}
