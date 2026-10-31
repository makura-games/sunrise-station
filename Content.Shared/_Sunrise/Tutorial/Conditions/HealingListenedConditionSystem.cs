using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Отслеживает успешное завершение применения лечебного предмета.
/// </summary>
public sealed partial class HealingListenedConditionSystem
    : EventListenedConditionSystemBase<HealingListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, HealingDoAfterEvent>(
            OnHealingCompleted,
            after: [typeof(HealingSystem)]);
        SubscribeLocalEvent<TutorialPlayerComponent, HealingDoAfterEvent>(
            OnSelfHealingCompleted,
            after: [typeof(HealingSystem)]);
    }

    private void OnHealingCompleted(Entity<TutorialObservableComponent> ent, ref HealingDoAfterEvent args)
    {
        if (!args.Handled || args.Cancelled || !Tutorial.TryGetPrototypeId(args.Used, out var item))
            return;

        RecordEvent(args.User, HealingListenedCondition.GetCounterKey(item), ent);
    }

    private void OnSelfHealingCompleted(Entity<TutorialPlayerComponent> ent, ref HealingDoAfterEvent args)
    {
        if (!args.Handled || args.Cancelled)
            return;

        if (args.Target != ent.Owner)
            return;

        if (!Tutorial.TryGetPrototypeId(args.Used, out var item))
            return;

        RecordEvent(ent, HealingListenedCondition.GetCounterKey(item), ent);
    }
}

/// <summary>
/// Проверяет, что указанный лечебный предмет применили к указанной сущности.
/// </summary>
public sealed partial class HealingListenedCondition : EventListenedConditionBase<HealingListenedCondition>
{
    /// <summary>
    /// Прототип лечебного предмета, которым должно быть выполнено лечение.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    public override string CounterKey => GetCounterKey(Item);

    public static string GetCounterKey(EntProtoId item)
    {
        return $"{nameof(HealingListenedCondition)}:{item.Id}";
    }
}
