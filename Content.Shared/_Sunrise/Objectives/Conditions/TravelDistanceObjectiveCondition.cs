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
/// Накапливает пройденный владельцем путь для каждого активного условия.
/// Висит на владельце цели, чтобы получать его <see cref="MoveEvent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectiveTravelDistanceOwnerComponent : Component
{
    /// <summary>
    /// Накопленный путь по хэндлу условия.
    /// </summary>
    [ViewVariables]
    public Dictionary<ObjectiveConditionHandle, ObjectiveTravelProgress> Progress = [];
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
        if (TerminatingOrDeleted(args.Context.Owner))
            return;

        var owner = EnsureComp<ObjectiveTravelDistanceOwnerComponent>(args.Context.Owner);
        owner.Progress[args.Context.Handle] = new ObjectiveTravelProgress();
    }

    [SubscribeLocalEvent]
    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<TravelDistanceObjectiveCondition> args)
    {
        args.Satisfied = TryComp(args.Context.Owner, out ObjectiveTravelDistanceOwnerComponent? owner) &&
                         owner.Progress.TryGetValue(args.Context.Handle, out var progress) &&
                         progress.Distance >= args.Condition.Distance;
    }

    [SubscribeLocalEvent]
    private void OnDeactivated(ref ObjectiveConditionDeactivatedEvent<TravelDistanceObjectiveCondition> args)
    {
        if (!TryComp(args.Context.Owner, out ObjectiveTravelDistanceOwnerComponent? owner))
            return;

        owner.Progress.Remove(args.Context.Handle);
        if (owner.Progress.Count == 0 && !TerminatingOrDeleted(args.Context.Owner))
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

        foreach (var progress in ent.Comp.Progress.Values)
        {
            progress.Distance += segment;
        }
    }
}
