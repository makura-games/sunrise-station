namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Completes after the owner travels a configured distance while the condition is active.
/// </summary>
public sealed partial class TravelDistanceObjectiveCondition : ObjectiveConditionBase<TravelDistanceObjectiveCondition>
{
    /// <summary>
    /// Required distance in world units.
    /// </summary>
    [DataField]
    public float Distance = 1f;
}

/// <summary>
/// Хранит накопленный путь условий на сущности цели. Живёт столько же, сколько цель,
/// поэтому перенос цели на другого владельца не сбрасывает пройденное расстояние.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveTravelDistanceComponent : Component
{
    /// <summary>
    /// Накопленный путь по хэндлу условия.
    /// </summary>
    [ViewVariables]
    public Dictionary<ObjectiveConditionHandle, ObjectiveTravelProgress> Progress = [];
}

/// <summary>
/// Висит на текущем владельце цели, чтобы получать его <see cref="MoveEvent"/>.
/// Ссылается на те же накопители, что лежат в <see cref="ObjectiveTravelDistanceComponent"/> цели.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveTravelDistanceOwnerComponent : Component
{
    /// <summary>
    /// Накопители активных условий этого владельца по хэндлу.
    /// </summary>
    [ViewVariables]
    public Dictionary<ObjectiveConditionHandle, ObjectiveTravelProgress> Tracked = [];
}

/// <summary>
/// Изменяемый накопитель пути одного условия.
/// </summary>
public sealed class ObjectiveTravelProgress
{
    /// <summary>
    /// Пройденное расстояние в мировых единицах.
    /// </summary>
    public float Distance;
}

/// <summary>
/// Tracks travel-distance condition lifecycles and accumulates the owner's path from movement events.
/// </summary>
public sealed partial class TravelDistanceObjectiveConditionSystem : EntitySystem
{
    /// <summary>
    /// Перемещение длиннее этого за одно событие считается телепортом и не засчитывается.
    /// Обычная ходьба, бег и толчки дают на порядок меньшие шаги за тик.
    /// </summary>
    private const float MaxSegmentLength = 2f;

    [SubscribeLocalEvent]
    private void OnValidation(ref ObjectiveConditionValidationEvent<TravelDistanceObjectiveCondition> args)
    {
        args.Handled = true;
        args.Valid = float.IsFinite(args.Condition.Distance) && args.Condition.Distance >= 0f;
        args.Error = args.Valid ? null : "travel distance cannot be negative";
    }

    [SubscribeLocalEvent]
    private void OnActivated(ref ObjectiveConditionActivatedEvent<TravelDistanceObjectiveCondition> args)
    {
        // Накопитель создаётся один раз на условие; повторная активация после переноса переиспользует его.
        var objective = EnsureComp<ObjectiveTravelDistanceComponent>(args.Context.Objective);
        if (!objective.Progress.TryGetValue(args.Context.Handle, out var progress))
        {
            progress = new ObjectiveTravelProgress();
            objective.Progress.Add(args.Context.Handle, progress);
        }

        if (TerminatingOrDeleted(args.Context.Owner))
            return;

        var owner = EnsureComp<ObjectiveTravelDistanceOwnerComponent>(args.Context.Owner);
        owner.Tracked[args.Context.Handle] = progress;
    }

    [SubscribeLocalEvent]
    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<TravelDistanceObjectiveCondition> args)
    {
        args.Satisfied = TryComp(args.Context.Objective, out ObjectiveTravelDistanceComponent? objective) &&
                         objective.Progress.TryGetValue(args.Context.Handle, out var progress) &&
                         progress.Distance >= args.Condition.Distance;
    }

    [SubscribeLocalEvent]
    private void OnDeactivated(ref ObjectiveConditionDeactivatedEvent<TravelDistanceObjectiveCondition> args)
    {
        // Снимаем только подписку владельца; накопитель остаётся на цели до её удаления.
        if (!TryComp(args.Context.Owner, out ObjectiveTravelDistanceOwnerComponent? owner))
            return;

        owner.Tracked.Remove(args.Context.Handle);
        if (owner.Tracked.Count == 0 && !TerminatingOrDeleted(args.Context.Owner))
            RemComp<ObjectiveTravelDistanceOwnerComponent>(args.Context.Owner);
    }

    [SubscribeLocalEvent]
    private void OnOwnerMove(Entity<ObjectiveTravelDistanceOwnerComponent> ent, ref MoveEvent args)
    {
        // Смена родителя (переход между гридами, контейнеры) даёт координаты в разных системах отсчёта,
        // поэтому такой шаг пропускаем: реальный путь при нём ничтожен.
        if (args.ParentChanged || args.OnlyRotation)
            return;

        var segment = (args.NewPosition.Position - args.OldPosition.Position).Length();
        if (segment <= 0f || segment > MaxSegmentLength)
            return;

        foreach (var progress in ent.Comp.Tracked.Values)
        {
            progress.Distance += segment;
        }
    }
}
