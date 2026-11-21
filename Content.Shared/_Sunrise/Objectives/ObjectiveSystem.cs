using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Owns the lifecycle and state of server-authoritative objective instances.
/// </summary>
/// <remarks>
/// All objective lifecycle events (<see cref="ObjectiveStartedEvent"/>, <see cref="ObjectiveStateChangedEvent"/>,
/// <see cref="ObjectiveCompletedEvent"/> and others) are queued and raised from <see cref="Update"/>,
/// never synchronously from inside gameplay event handlers. Objective status queried through
/// <see cref="TryGetObjectiveStatus"/> is always up to date.
/// </remarks>
public sealed partial class ObjectiveSystem : EntitySystem, IObjectiveConditionEventRaiser
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// Интервал периодической оценки live-условий активных целей.
    /// </summary>
    public static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(0.25);

    // Снапшот целей на текущую оценку: обработчики событий цели могут стартовать новые цели,
    // а добавление ActiveObjectiveComponent во время обхода EntityQueryEnumerator ломает энумератор.
    private readonly List<Entity<ObjectiveRuntimeComponent>> _toEvaluate = [];

    // События цели, ожидающие рассылки из Update.
    private readonly Queue<Action> _pendingEvents = new();

    private TimeSpan _nextEvaluation;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        if (_timing.CurTime >= _nextEvaluation)
        {
            _nextEvaluation = _timing.CurTime + EvaluationInterval;

            _toEvaluate.Clear();
            var query = EntityQueryEnumerator<ActiveObjectiveComponent, ObjectiveRuntimeComponent>();
            while (query.MoveNext(out var uid, out _, out var objective))
            {
                _toEvaluate.Add((uid, objective));
            }

            foreach (var objective in _toEvaluate)
            {
                if (TerminatingOrDeleted(objective))
                    continue;

                EvaluateObjective(objective);
            }

            _toEvaluate.Clear();
        }

        DispatchPendingEvents();
    }

    /// <summary>
    /// Ставит событие цели в очередь. Оно будет разослано из <see cref="Update"/>,
    /// чтобы потребители не реагировали посреди чужого игрового обработчика.
    /// </summary>
    private void QueueObjectiveEvent<T>(T ev) where T : struct
    {
        _pendingEvents.Enqueue(() =>
        {
            var copy = ev;
            RaiseLocalEvent(ref copy);
        });
    }

    private void DispatchPendingEvents()
    {
        // Рассылаем только накопленное к этому моменту: события, поставленные обработчиками,
        // уйдут следующим тиком, поэтому зациклиться здесь нельзя.
        var count = _pendingEvents.Count;
        for (var i = 0; i < count; i++)
        {
            _pendingEvents.Dequeue().Invoke();
        }
    }

    [SubscribeLocalEvent]
    private void OnOwnerDidEquipHand(Entity<ObjectiveOwnerComponent> ent, ref DidEquipHandEvent args)
    {
        NotifyEntityAcquired(ent, args.Equipped, ObjectiveObservationScope.Hands);
    }

    [SubscribeLocalEvent]
    private void OnOwnerDidEquip(Entity<ObjectiveOwnerComponent> ent, ref DidEquipEvent args)
    {
        NotifyEntityAcquired(ent, args.Equipment, ObjectiveObservationScope.Equipment);
    }

    /// <summary>
    /// Сообщает условиям активных целей владельца о новой сущности в руках или экипировке,
    /// чтобы наблюдатели обновлялись по событиям, а не на каждой оценке.
    /// </summary>
    private void NotifyEntityAcquired(
        Entity<ObjectiveOwnerComponent> owner,
        EntityUid target,
        ObjectiveObservationScope scope)
    {
        if (!_net.IsServer || TerminatingOrDeleted(target))
            return;

        var objectives = new List<EntityUid>(owner.Comp.Objectives);
        for (var i = 0; i < objectives.Count; i++)
        {
            if (!TryComp(objectives[i], out ObjectiveRuntimeComponent? objective) ||
                !objective.ConditionsActive ||
                (objective.Options.ObservationScope & scope) == 0 ||
                !HasComp<ActiveObjectiveComponent>(objectives[i]))
            {
                continue;
            }

            foreach (var runtime in objective.Conditions.Values)
            {
                var context = GetContext((objectives[i], objective), runtime.Key);
                runtime.Condition.RaiseEntityAcquired(context, target, this);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnOwnerTerminating(Entity<ObjectiveOwnerComponent> ent, ref EntityTerminatingEvent args)
    {
        var objectives = new List<EntityUid>(ent.Comp.Objectives);
        ent.Comp.Objectives.Clear();

        for (var i = 0; i < objectives.Count; i++)
        {
            TryStopObjective(objectives[i], ObjectiveStopReason.OwnerTerminating);
        }
    }

    [SubscribeLocalEvent]
    private void OnObjectiveTerminating(Entity<ObjectiveRuntimeComponent> ent, ref EntityTerminatingEvent args)
    {
        // Компонент без запущенной цели (например, добавленный вручную) не должен рассылать событие остановки.
        if (!ent.Comp.TrackedOwner.Valid)
            return;

        if (!HasComp<StoppingObjectiveComponent>(ent))
        {
            DeactivateConditions(ent);
            RaiseStopped(ent, ObjectiveStopReason.ObjectiveTerminating);
        }

        UnlinkOwner(ent);
    }

    /// <summary>
    /// Starts an inline objective definition for an owner.
    /// </summary>
    public bool TryStartObjective(
        Entity<ObjectiveOwnerComponent?> owner,
        ObjectiveDefinition definition,
        ObjectiveStartOptions options,
        out EntityUid objective)
    {
        if (!_net.IsServer)
        {
            objective = EntityUid.Invalid;
            return false;
        }

        return TryStartObjective(owner, definition, options, null, false, out objective);
    }

    /// <summary>
    /// Starts a reusable objective prototype for an owner.
    /// </summary>
    public bool TryStartObjective(
        Entity<ObjectiveOwnerComponent?> owner,
        ProtoId<ObjectivePrototype> prototypeId,
        ObjectiveStartOptions options,
        out EntityUid objective)
    {
        objective = EntityUid.Invalid;

        if (!_net.IsServer || !_prototype.TryIndex(prototypeId, out var prototype))
            return false;

        return TryStartObjective(owner, prototype.Definition, options, prototypeId, true, out objective);
    }

    private bool TryStartObjective(
        Entity<ObjectiveOwnerComponent?> owner,
        ObjectiveDefinition definition,
        ObjectiveStartOptions options,
        ProtoId<ObjectivePrototype>? prototype,
        bool requireStableIds,
        out EntityUid objective)
    {
        objective = EntityUid.Invalid;

        if (!owner.Owner.Valid || TerminatingOrDeleted(owner))
            return false;

        if (!TryBuildConditions(owner, definition, options, requireStableIds, out var conditions, out var error))
        {
            Log.Error("Cannot start objective {Objective} for {Owner}: {Error}",
                prototype?.Id ?? "inline",
                ToPrettyString(owner),
                error);
            return false;
        }

        objective = Spawn(prototype: null, MapCoordinates.Nullspace);
        var objectiveComponent = EnsureComp<ObjectiveRuntimeComponent>(objective);
        objectiveComponent.TrackedOwner = owner;
        objectiveComponent.Prototype = prototype;
        objectiveComponent.Definition = definition;
        objectiveComponent.Options = CopyOptions(options);
        objectiveComponent.Conditions = conditions;

        EnsureComp<ActiveObjectiveComponent>(objective);
        var tracker = EnsureComp<ObjectiveOwnerComponent>(owner);
        tracker.Objectives.Add(objective);

        ActivateConditions((objective, objectiveComponent));

        var started = new ObjectiveStartedEvent(
            owner,
            objective,
            prototype,
            objectiveComponent.Options.SourceIdentifier);
        QueueObjectiveEvent(started);

        EvaluateObjective((objective, objectiveComponent));
        return true;
    }

    /// <summary>
    /// Stops an objective and removes all of its condition registrations.
    /// </summary>
    public bool TryStopObjective(
        Entity<ObjectiveRuntimeComponent?> objective,
        ObjectiveStopReason reason = ObjectiveStopReason.Cancelled)
    {
        if (!_net.IsServer ||
            !Resolve(objective, ref objective.Comp, false) ||
            HasComp<StoppingObjectiveComponent>(objective))
        {
            return false;
        }

        EnsureComp<StoppingObjectiveComponent>(objective);
        RemComp<ActiveObjectiveComponent>(objective);
        RemComp<CompletedObjectiveComponent>(objective);
        DeactivateConditions((objective, objective.Comp));
        RaiseStopped((objective, objective.Comp), reason);
        UnlinkOwner((objective, objective.Comp));
        QueueDel(objective);
        return true;
    }

    /// <summary>
    /// Stops every objective linked to an owner.
    /// </summary>
    public int StopAllObjectives(
        Entity<ObjectiveOwnerComponent?> owner,
        ObjectiveStopReason reason = ObjectiveStopReason.Cancelled)
    {
        if (!_net.IsServer || !Resolve(owner, ref owner.Comp, false))
            return 0;

        var objectives = new List<EntityUid>(owner.Comp.Objectives);
        var stopped = 0;
        for (var i = 0; i < objectives.Count; i++)
        {
            if (TryStopObjective(objectives[i], reason))
                stopped++;
        }

        return stopped;
    }

    /// <summary>
    /// Transfers an objective to another owner without resetting progress.
    /// </summary>
    public bool TryTransferObjective(
        Entity<ObjectiveRuntimeComponent?> objective,
        Entity<ObjectiveOwnerComponent?> newOwner)
    {
        if (!_net.IsServer ||
            !Resolve(objective, ref objective.Comp, false) ||
            HasComp<StoppingObjectiveComponent>(objective) ||
            !newOwner.Owner.Valid ||
            TerminatingOrDeleted(newOwner))
        {
            return false;
        }

        var previousOwner = objective.Comp.TrackedOwner;
        if (previousOwner == newOwner.Owner)
            return true;

        DeactivateConditions((objective, objective.Comp));
        UnlinkOwner((objective, objective.Comp));

        objective.Comp.TrackedOwner = newOwner;
        var tracker = EnsureComp<ObjectiveOwnerComponent>(newOwner);
        tracker.Objectives.Add(objective);

        var active = HasComp<ActiveObjectiveComponent>(objective);
        if (active)
            ActivateConditions((objective, objective.Comp));

        var changed = new ObjectiveOwnerChangedEvent(
            previousOwner,
            newOwner,
            objective,
            objective.Comp.Prototype,
            objective.Comp.Options.SourceIdentifier);
        QueueObjectiveEvent(changed);

        if (active)
        {
            EvaluateObjective((objective, objective.Comp));
        }

        return true;
    }

    /// <summary>
    /// Adds persistent history progress to one condition.
    /// </summary>
    public bool TryAddConditionProgress(ObjectiveConditionHandle handle, int amount)
    {
        if (!_net.IsServer ||
            amount <= 0 ||
            !TryGetActiveRuntime(handle, out var objective, out var runtime))
            return false;

        var previousProgress = runtime.Progress;
        var previousRaw = runtime.RawSatisfied;
        var previousSatisfied = runtime.Satisfied;
        runtime.Progress = amount > int.MaxValue - runtime.Progress
            ? int.MaxValue
            : runtime.Progress + amount;
        RefreshConditionState(runtime);

        RaiseConditionChangedIfNeeded(
            objective,
            handle,
            runtime,
            previousProgress,
            previousRaw,
            previousSatisfied);
        UpdateObjectiveState(objective);
        return true;
    }

    /// <summary>
    /// Reports the current reversible state of one condition.
    /// </summary>
    public bool TrySetConditionSatisfied(ObjectiveConditionHandle handle, bool satisfied)
    {
        if (!_net.IsServer || !TryGetActiveRuntime(handle, out var objective, out var runtime))
            return false;

        ApplyReportedState(objective, handle, runtime, satisfied);
        UpdateObjectiveState(objective);
        return true;
    }

    /// <summary>
    /// Validates an inline objective definition without starting it or writing errors to the log.
    /// </summary>
    public bool TryValidateObjective(
        EntityUid owner,
        ObjectiveDefinition definition,
        ObjectiveStartOptions options,
        [NotNullWhen(false)] out string? error)
    {
        return TryBuildConditions(owner, definition, options, false, out _, out error);
    }

    /// <summary>
    /// Validates a reusable objective prototype without starting it or writing errors to the log.
    /// </summary>
    public bool TryValidateObjective(
        EntityUid owner,
        ProtoId<ObjectivePrototype> prototypeId,
        ObjectiveStartOptions options,
        [NotNullWhen(false)] out string? error)
    {
        if (!_prototype.TryIndex(prototypeId, out var prototype))
        {
            error = $"unknown objective prototype {prototypeId}";
            return false;
        }

        return TryBuildConditions(owner, prototype.Definition, options, true, out _, out error);
    }

    private void ApplyReportedState(
        Entity<ObjectiveRuntimeComponent> objective,
        ObjectiveConditionHandle handle,
        ObjectiveConditionRuntimeState runtime,
        bool satisfied)
    {
        var previousProgress = runtime.Progress;
        var previousRaw = runtime.RawSatisfied;
        var previousSatisfied = runtime.Satisfied;
        runtime.ReportedSatisfied = satisfied;
        RefreshConditionState(runtime);

        RaiseConditionChangedIfNeeded(
            objective,
            handle,
            runtime,
            previousProgress,
            previousRaw,
            previousSatisfied);
    }

    /// <summary>
    /// Gets a public objective-state snapshot.
    /// </summary>
    public bool TryGetObjectiveStatus(Entity<ObjectiveRuntimeComponent?> objective, out ObjectiveStatus status)
    {
        status = default;
        if (!Resolve(objective, ref objective.Comp, false))
            return false;

        status = new ObjectiveStatus(
            HasComp<ActiveObjectiveComponent>(objective),
            objective.Comp.Satisfied,
            HasComp<CompletedObjectiveComponent>(objective),
            objective.Comp.Options.Mode,
            objective.Comp.Options.SourceIdentifier);
        return true;
    }

    /// <summary>
    /// Gets a condition definition by handle when it has the requested concrete type.
    /// </summary>
    public bool TryGetCondition<TCondition>(
        ObjectiveConditionHandle handle,
        [NotNullWhen(true)] out TCondition? condition)
        where TCondition : ObjectiveCondition
    {
        condition = null;
        if (!TryGetRuntime(handle, out _, out var runtime) || runtime.Condition is not TCondition typed)
            return false;

        condition = typed;
        return true;
    }

    /// <summary>
    /// Gets a public condition-state snapshot.
    /// </summary>
    public bool TryGetConditionStatus(ObjectiveConditionHandle handle, out ObjectiveConditionStatus status)
    {
        status = default;
        if (!TryGetRuntime(handle, out _, out var runtime))
            return false;

        status = new ObjectiveConditionStatus(runtime.Progress, runtime.RawSatisfied, runtime.Satisfied);
        return true;
    }

    private bool TryBuildConditions(
        EntityUid owner,
        ObjectiveDefinition definition,
        ObjectiveStartOptions options,
        bool requireStableIds,
        out Dictionary<string, ObjectiveConditionRuntimeState> runtimes,
        [NotNullWhen(false)] out string? error)
    {
        runtimes = [];
        if (!float.IsFinite(options.ObservationRange) || options.ObservationRange < 0f)
        {
            error = $"observation range {options.ObservationRange} must be finite and non-negative";
            return false;
        }

        if (definition.All.Count == 0 && definition.Any.Count == 0)
        {
            error = "objective definition is empty";
            return false;
        }

        if (!TryAddConditions(owner, definition.All, options, requireStableIds, "all", runtimes, out error))
            return false;

        return TryAddConditions(owner, definition.Any, options, requireStableIds, "any", runtimes, out error);
    }

    private bool TryAddConditions(
        EntityUid owner,
        List<ObjectiveCondition> conditions,
        ObjectiveStartOptions options,
        bool requireStableIds,
        string group,
        Dictionary<string, ObjectiveConditionRuntimeState> runtimes,
        [NotNullWhen(false)] out string? error)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var key = condition.Id;
            if (string.IsNullOrWhiteSpace(key))
            {
                if (requireStableIds)
                {
                    error = $"named objective condition {condition.GetType().Name} in group {group} has no stable id";
                    return false;
                }

                key = $"{group}:{i}";
            }

            if (runtimes.ContainsKey(key))
            {
                error = $"objective condition id {key} is duplicated";
                return false;
            }

            // Инвертированный счётчик означает «событие ещё не произошло», а это правда уже в момент старта:
            // OneShot-цель с таким условием закрылась бы мгновенно. В Monitor-режиме (failures/preconditions) это законно.
            if (condition.Inverted && condition.IsProgressBased && options.Mode == ObjectiveRunMode.OneShot)
            {
                error = $"inverted history condition {condition.GetType().Name} is satisfied immediately " +
                        "in one-shot mode; use it only in monitor objectives";
                return false;
            }

            if (!condition.RaiseValidation(owner, options, this, out var conditionError))
            {
                error = $"objective condition {condition.GetType().Name} is invalid: " +
                        (conditionError ?? "unspecified validation error");
                return false;
            }

            var runtime = new ObjectiveConditionRuntimeState
            {
                Key = key,
                Condition = condition,
                IsAny = group == "any",
            };
            RefreshConditionState(runtime);
            runtimes.Add(key, runtime);
        }

        error = null;
        return true;
    }

    private void ActivateConditions(Entity<ObjectiveRuntimeComponent> objective)
    {
        if (objective.Comp.ConditionsActive)
            return;

        objective.Comp.ConditionsActive = true;
        foreach (var runtime in objective.Comp.Conditions.Values)
        {
            var context = GetContext(objective, runtime.Key);
            runtime.Condition.RaiseActivated(context, this);
        }
    }

    private void EvaluateObjective(Entity<ObjectiveRuntimeComponent> objective)
    {
        if (!objective.Comp.ConditionsActive || !HasComp<ActiveObjectiveComponent>(objective))
            return;

        // Сначала опрашиваем все условия, затем применяем результаты и один раз пересчитываем цель:
        // иначе цель оценивается на смеси свежих и устаревших значений (например, инвертированное
        // live-условие считается выполненным до своего первого опроса).
        List<(ObjectiveConditionRuntimeState Runtime, bool Satisfied)>? reports = null;
        foreach (var runtime in objective.Comp.Conditions.Values)
        {
            var context = GetContext(objective, runtime.Key);
            var reported = runtime.Condition.RaiseEvaluate(context, this);
            if (reported == null)
                continue;

            reports ??= [];
            reports.Add((runtime, reported.Value));
        }

        if (reports != null)
        {
            for (var i = 0; i < reports.Count; i++)
            {
                if (!HasComp<ActiveObjectiveComponent>(objective) || HasComp<StoppingObjectiveComponent>(objective))
                    return;

                var (runtime, satisfied) = reports[i];
                ApplyReportedState(objective, new ObjectiveConditionHandle(objective, runtime.Key), runtime, satisfied);
            }
        }

        UpdateObjectiveState(objective);
    }

    private void DeactivateConditions(Entity<ObjectiveRuntimeComponent> objective)
    {
        if (!objective.Comp.ConditionsActive)
            return;

        objective.Comp.ConditionsActive = false;
        foreach (var runtime in objective.Comp.Conditions.Values)
        {
            var context = GetContext(objective, runtime.Key);
            runtime.Condition.RaiseDeactivated(context, this);
        }
    }

    private void UpdateObjectiveState(Entity<ObjectiveRuntimeComponent> objective)
    {
        if (!HasComp<ActiveObjectiveComponent>(objective))
            return;

        var satisfied = IsDefinitionSatisfied(objective.Comp);
        if (objective.Comp.Satisfied == satisfied)
            return;

        var previous = objective.Comp.Satisfied;
        objective.Comp.Satisfied = satisfied;
        var changed = new ObjectiveStateChangedEvent(
            objective.Comp.TrackedOwner,
            objective,
            objective.Comp.Prototype,
            previous,
            satisfied,
            objective.Comp.Options.SourceIdentifier);
        QueueObjectiveEvent(changed);

        if (!satisfied)
            return;

        var completed = new ObjectiveCompletedEvent(
            objective.Comp.TrackedOwner,
            objective,
            objective.Comp.Prototype,
            objective.Comp.Options.SourceIdentifier);
        QueueObjectiveEvent(completed);

        if (objective.Comp.Options.Mode == ObjectiveRunMode.Monitor)
            return;

        RemComp<ActiveObjectiveComponent>(objective);
        DeactivateConditions(objective);

        if (objective.Comp.Options.CompletionRetention == ObjectiveCompletionRetention.Retain)
        {
            EnsureComp<CompletedObjectiveComponent>(objective);
            return;
        }

        TryStopObjective(objective.Owner, ObjectiveStopReason.Completed);
    }

    private bool IsDefinitionSatisfied(ObjectiveRuntimeComponent objective)
    {
        var hasAny = false;
        var anySatisfied = false;
        foreach (var runtime in objective.Conditions.Values)
        {
            if (!runtime.IsAny)
            {
                if (!runtime.Satisfied)
                    return false;

                continue;
            }

            hasAny = true;
            anySatisfied |= runtime.Satisfied;
        }

        return !hasAny || anySatisfied;
    }

    private void RaiseConditionChangedIfNeeded(
        Entity<ObjectiveRuntimeComponent> objective,
        ObjectiveConditionHandle handle,
        ObjectiveConditionRuntimeState runtime,
        int previousProgress,
        bool previousRaw,
        bool previousSatisfied)
    {
        if (previousProgress == runtime.Progress &&
            previousRaw == runtime.RawSatisfied &&
            previousSatisfied == runtime.Satisfied)
        {
            return;
        }

        var changed = new ObjectiveConditionChangedEvent(
            objective.Comp.TrackedOwner,
            objective,
            objective.Comp.Prototype,
            handle,
            runtime.Progress,
            runtime.RawSatisfied,
            runtime.Satisfied,
            objective.Comp.Options.SourceIdentifier);
        QueueObjectiveEvent(changed);
    }

    private static void RefreshConditionState(ObjectiveConditionRuntimeState runtime)
    {
        runtime.RawSatisfied = runtime.Condition.GetRawSatisfied(runtime.Progress, runtime.ReportedSatisfied);
        runtime.Satisfied = runtime.RawSatisfied != runtime.Condition.Inverted;
    }

    private bool TryGetActiveRuntime(
        ObjectiveConditionHandle handle,
        out Entity<ObjectiveRuntimeComponent> objective,
        out ObjectiveConditionRuntimeState runtime)
    {
        if (!TryGetRuntime(handle, out objective, out runtime) ||
            !HasComp<ActiveObjectiveComponent>(objective) ||
            HasComp<StoppingObjectiveComponent>(objective))
        {
            return false;
        }

        return true;
    }

    private bool TryGetRuntime(
        ObjectiveConditionHandle handle,
        out Entity<ObjectiveRuntimeComponent> objective,
        out ObjectiveConditionRuntimeState runtime)
    {
        objective = default;
        runtime = default!;
        if (!handle.Objective.Valid ||
            string.IsNullOrWhiteSpace(handle.Key) ||
            !TryComp(handle.Objective, out ObjectiveRuntimeComponent? component) ||
            !component.Conditions.TryGetValue(handle.Key, out var found) ||
            found == null)
        {
            return false;
        }

        runtime = found;
        objective = (handle.Objective, component);
        return true;
    }

    private ObjectiveConditionContext GetContext(Entity<ObjectiveRuntimeComponent> objective, string key)
    {
        return new ObjectiveConditionContext(
            objective.Comp.TrackedOwner,
            objective,
            new ObjectiveConditionHandle(objective, key),
            objective.Comp.Options,
            objective.Comp.Prototype);
    }

    private void RaiseStopped(Entity<ObjectiveRuntimeComponent> objective, ObjectiveStopReason reason)
    {
        var stopped = new ObjectiveStoppedEvent(
            objective.Comp.TrackedOwner,
            objective,
            objective.Comp.Prototype,
            reason,
            objective.Comp.Options.SourceIdentifier);
        QueueObjectiveEvent(stopped);
    }

    private void UnlinkOwner(Entity<ObjectiveRuntimeComponent> objective)
    {
        if (!TryComp(objective.Comp.TrackedOwner, out ObjectiveOwnerComponent? tracker))
            return;

        tracker.Objectives.Remove(objective);
        if (tracker.Objectives.Count == 0 && !TerminatingOrDeleted(objective.Comp.TrackedOwner))
            RemComp<ObjectiveOwnerComponent>(objective.Comp.TrackedOwner);
    }

    private static ObjectiveStartOptions CopyOptions(ObjectiveStartOptions options)
    {
        return new ObjectiveStartOptions
        {
            Mode = options.Mode,
            CompletionRetention = options.CompletionRetention,
            ObservationScope = options.ObservationScope,
            ObservationRange = options.ObservationRange,
            SourceIdentifier = options.SourceIdentifier,
        };
    }

    bool IObjectiveConditionEventRaiser.RaiseValidation<TCondition>(
        EntityUid owner,
        ObjectiveStartOptions options,
        TCondition condition,
        out string? error)
    {
        var ev = new ObjectiveConditionValidationEvent<TCondition>(owner, options, condition);
        RaiseLocalEvent(ref ev);
        error = ev.Error;
        return ev.Handled && ev.Valid;
    }

    void IObjectiveConditionEventRaiser.RaiseActivated<TCondition>(
        ObjectiveConditionContext context,
        TCondition condition)
    {
        var ev = new ObjectiveConditionActivatedEvent<TCondition>(context, condition);
        RaiseLocalEvent(ref ev);
    }

    bool? IObjectiveConditionEventRaiser.RaiseEvaluate<TCondition>(
        ObjectiveConditionContext context,
        TCondition condition)
    {
        var ev = new ObjectiveConditionEvaluateEvent<TCondition>(context, condition);
        RaiseLocalEvent(ref ev);
        return ev.Satisfied;
    }

    void IObjectiveConditionEventRaiser.RaiseEntityAcquired<TCondition>(
        ObjectiveConditionContext context,
        TCondition condition,
        EntityUid target)
    {
        var ev = new ObjectiveConditionEntityAcquiredEvent<TCondition>(context, condition, target);
        RaiseLocalEvent(ref ev);
    }

    void IObjectiveConditionEventRaiser.RaiseDeactivated<TCondition>(
        ObjectiveConditionContext context,
        TCondition condition)
    {
        var ev = new ObjectiveConditionDeactivatedEvent<TCondition>(context, condition);
        RaiseLocalEvent(ref ev);
    }
}
