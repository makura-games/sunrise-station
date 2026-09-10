using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Записывает установку предмета в наблюдаемую сущность.
/// </summary>
public sealed partial class ItemInsertedObjectiveConditionSystem
    : ObjectiveEventConditionSystem<ItemInsertedObjectiveCondition, ObjectiveContainerOwnerComponent, ObjectiveContainerObserverComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveContainerObserverComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(Entity<ObjectiveContainerObserverComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || ent.Comp.Registrations.Count == 0)
            return;

        if (!TryGetPrototypeId(args.Entity, out var item))
            return;

        var key = ItemInsertedObjectiveCondition.GetItemKey(item);
        RecordObservedEvent(ent, key);
    }
}

/// <summary>
/// Проверяет установку указанного предмета в сущность целевого прототипа.
/// </summary>
public sealed partial class ItemInsertedObjectiveCondition
    : ObjectiveEventConditionBase<ItemInsertedObjectiveCondition>
{
    /// <summary>
    /// Прототип установленного предмета.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    public override string CounterKey => GetItemKey(Item);

    public static string GetItemKey(EntProtoId item)
    {
        return string.Concat(nameof(ItemInsertedObjectiveCondition), ".Item.", item.Id);
    }
}
