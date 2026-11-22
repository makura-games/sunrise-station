using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Записывает установку предмета в наблюдаемую туториалом сущность.
/// </summary>
public sealed partial class ItemInsertedListenedConditionSystem
    : EventListenedConditionSystemBase<ItemInsertedListenedCondition>
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(Entity<TutorialObservableComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || ent.Comp.Observers.Count == 0)
            return;

        if (!Tutorial.TryGetPrototypeId(args.Entity, out var item))
            return;

        var key = ItemInsertedListenedCondition.GetItemKey(item);
        foreach (var observer in ent.Comp.Observers)
        {
            if (TerminatingOrDeleted(observer))
                continue;

            RecordEvent(observer, key, ent);
        }
    }
}

/// <summary>
/// Проверяет установку указанного предмета в сущность целевого прототипа.
/// </summary>
public sealed partial class ItemInsertedListenedCondition
    : EventListenedConditionBase<ItemInsertedListenedCondition>
{
    /// <summary>
    /// Прототип установленного предмета.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    public override string CounterKey => GetItemKey(Item);

    public static string GetItemKey(EntProtoId item)
    {
        return string.Concat(nameof(ItemInsertedListenedCondition), ".Item.", item.Id);
    }
}
