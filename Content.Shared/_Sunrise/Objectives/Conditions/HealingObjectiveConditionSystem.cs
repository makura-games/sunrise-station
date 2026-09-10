using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Отслеживает успешное завершение применения лечебного предмета.
/// </summary>
public sealed partial class HealingObjectiveConditionSystem
    : ObjectiveEventConditionSystem<HealingObjectiveCondition, ObjectiveHealthOwnerComponent, ObjectiveHealthObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveHealthObserverComponent, HealingDoAfterEvent>(
            OnHealingCompleted,
            after: [typeof(HealingSystem)]);
        SubscribeLocalEvent<ObjectiveHealthOwnerComponent, HealingDoAfterEvent>(
            OnSelfHealingCompleted,
            after: [typeof(HealingSystem)]);
    }

    private void OnHealingCompleted(Entity<ObjectiveHealthObserverComponent> ent, ref HealingDoAfterEvent args)
    {
        if (!args.Handled || args.Cancelled || !TryGetPrototypeId(args.Used, out var item))
            return;

        RecordObservedEvent(ent, HealingObjectiveCondition.GetCounterKey(item), args.User);
    }

    private void OnSelfHealingCompleted(Entity<ObjectiveHealthOwnerComponent> ent, ref HealingDoAfterEvent args)
    {
        if (!args.Handled || args.Cancelled)
            return;

        if (args.Target != ent.Owner)
            return;

        if (!TryGetPrototypeId(args.Used, out var item))
            return;

        RecordEvent(ent, HealingObjectiveCondition.GetCounterKey(item), ent);
    }
}

/// <summary>
/// Проверяет, что указанный лечебный предмет применили к указанной сущности.
/// </summary>
public sealed partial class HealingObjectiveCondition : ObjectiveEventConditionBase<HealingObjectiveCondition>
{
    /// <summary>
    /// Прототип лечебного предмета, которым должно быть выполнено лечение.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    public override string CounterKey => GetCounterKey(Item);

    public static string GetCounterKey(EntProtoId item)
    {
        return $"{nameof(HealingObjectiveCondition)}:{item.Id}";
    }
}
