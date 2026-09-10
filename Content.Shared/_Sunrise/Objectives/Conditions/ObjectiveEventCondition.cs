using Content.Shared.Actions.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Contract used by event-history conditions that optionally observe a target prototype.
/// </summary>
public interface IObjectiveEventCondition
{
    string CounterKey { get; }
    bool ObserveAnyWithoutTarget { get; }
    EntProtoId? Target { get; }
}

/// <summary>
/// Base data for objective conditions satisfied by counting gameplay events.
/// </summary>
public abstract partial class ObjectiveEventConditionBase<TCondition> :
    ObjectiveCountConditionBase<TCondition>,
    IObjectiveEventCondition
    where TCondition : ObjectiveEventConditionBase<TCondition>
{
    /// <inheritdoc />
    public virtual string CounterKey => typeof(TCondition).Name;

    /// <inheritdoc />
    public virtual bool ObserveAnyWithoutTarget => false;

    /// <summary>
    /// Optional entity prototype that must match a primary or secondary event target.
    /// </summary>
    [DataField]
    public EntProtoId? Target { get; set; }

    /// <inheritdoc />
    public override bool GetRawSatisfied(int progress, bool reportedSatisfied)
    {
        return reportedSatisfied || base.GetRawSatisfied(progress, false);
    }
}

/// <summary>
/// Runtime registration storage shared by semantic owner components.
/// </summary>
public interface IObjectiveEventOwnerComponent
{
    Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; }
    Dictionary<ObjectiveConditionHandle, HashSet<EntityUid>> ObservedEntities { get; }
}

/// <summary>
/// Runtime registration storage shared by semantic observer components.
/// </summary>
public interface IObjectiveEventObserverComponent
{
    Dictionary<ObjectiveConditionHandle, EntityUid> Registrations { get; }
}

/// <summary>
/// Base system for one event-history condition and its semantic registration components.
/// </summary>
public abstract class ObjectiveEventConditionSystem<TCondition, TOwnerComponent, TObserverComponent> : EntitySystem
    where TCondition : ObjectiveEventConditionBase<TCondition>
    where TOwnerComponent : Component, IObjectiveEventOwnerComponent, new()
    where TObserverComponent : Component, IObjectiveEventObserverComponent, new()
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ObjectiveSystem _objectives = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private readonly HashSet<EntityUid> _emptyOwners = [];
    private readonly HashSet<EntityUid> _emptyObservers = [];

    /// <summary>
    /// Default event key used by conditions without a specialized discriminator.
    /// </summary>
    protected static string DefaultKey => typeof(TCondition).Name;

    /// <summary>
    /// Objective runtime used by specialized observer lifecycle hooks.
    /// </summary>
    protected ObjectiveSystem Objectives => _objectives;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveConditionValidationEvent<TCondition>>(OnValidation);
        SubscribeLocalEvent<ObjectiveConditionActivatedEvent<TCondition>>(OnActivated);
        SubscribeLocalEvent<ObjectiveConditionEvaluateEvent<TCondition>>(OnEvaluate);
        SubscribeLocalEvent<ObjectiveConditionDeactivatedEvent<TCondition>>(OnDeactivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        CleanupEmptyOwners();
        CleanupEmptyObservers();
    }

    private void OnValidation(ref ObjectiveConditionValidationEvent<TCondition> args)
    {
        args.Handled = true;
        if (args.Condition.Count < 0)
        {
            args.Valid = false;
            args.Error = "event count cannot be negative";
            return;
        }

        args.Valid = Validate(args.Condition, out var error);
        args.Error = error;
    }

    private void OnActivated(ref ObjectiveConditionActivatedEvent<TCondition> args)
    {
        if (TerminatingOrDeleted(args.Context.Owner))
            return;

        var owner = EnsureComp<TOwnerComponent>(args.Context.Owner);
        _emptyOwners.Remove(args.Context.Owner);
        owner.Registrations[args.Context.Handle] = args.Context.Owner;
        owner.ObservedEntities.TryAdd(args.Context.Handle, []);
        RefreshObservers(args.Context, args.Condition, owner);
    }

    private void OnEvaluate(ref ObjectiveConditionEvaluateEvent<TCondition> args)
    {
        if (!TryComp(args.Context.Owner, out TOwnerComponent? owner))
            return;

        RefreshObservers(args.Context, args.Condition, owner);
        Evaluate(args.Context, args.Condition, ref args);
    }

    private void OnDeactivated(ref ObjectiveConditionDeactivatedEvent<TCondition> args)
    {
        if (!TryComp(args.Context.Owner, out TOwnerComponent? owner))
            return;

        if (owner.ObservedEntities.Remove(args.Context.Handle, out var observed))
        {
            foreach (var target in observed)
            {
                RemoveObserverRegistration(target, args.Context.Handle);
            }
        }

        owner.Registrations.Remove(args.Context.Handle);
        if (owner.Registrations.Count == 0 && !TerminatingOrDeleted(args.Context.Owner))
            _emptyOwners.Add(args.Context.Owner);
    }

    /// <summary>
    /// Validates fields owned by a concrete event condition.
    /// </summary>
    protected virtual bool Validate(TCondition condition, out string? error)
    {
        error = null;
        return true;
    }

    /// <summary>
    /// Optionally supplements persistent event history with a current live state.
    /// </summary>
    protected virtual void Evaluate(
        ObjectiveConditionContext context,
        TCondition condition,
        ref ObjectiveConditionEvaluateEvent<TCondition> args)
    {
    }

    /// <summary>
    /// Advances active conditions on <paramref name="user"/> whose key and target filters match the event.
    /// </summary>
    protected int RecordEvent(
        EntityUid user,
        string key,
        EntityUid? primaryTarget = null,
        EntityUid? secondaryTarget = null,
        string? sourceIdentifierPrefix = null)
    {
        if (!TryComp(user, out TOwnerComponent? owner))
            return 0;

        var handles = new List<ObjectiveConditionHandle>(owner.Registrations.Keys);
        var recorded = 0;
        for (var i = 0; i < handles.Count; i++)
        {
            var handle = handles[i];
            if (!_objectives.TryGetCondition<TCondition>(handle, out var condition) ||
                condition.CounterKey != key ||
                !MatchesEventTarget(condition, primaryTarget, secondaryTarget))
            {
                continue;
            }

            if (sourceIdentifierPrefix != null &&
                (!_objectives.TryGetObjectiveStatus(handle.Objective, out var status) ||
                 status.SourceIdentifier == null ||
                 !status.SourceIdentifier.StartsWith(sourceIdentifierPrefix, StringComparison.Ordinal)))
            {
                continue;
            }

            if (_objectives.TryAddConditionProgress(handle, 1))
                recorded++;
        }

        return recorded;
    }

    /// <summary>
    /// Advances registrations attached to an observed entity, optionally restricting them to an event user.
    /// </summary>
    protected void RecordObservedEvent(
        Entity<TObserverComponent> observed,
        string key,
        EntityUid? eventUser = null,
        EntityUid? primaryTarget = null,
        EntityUid? secondaryTarget = null)
    {
        var registrations = new List<KeyValuePair<ObjectiveConditionHandle, EntityUid>>(
            observed.Comp.Registrations);
        for (var i = 0; i < registrations.Count; i++)
        {
            var (handle, owner) = registrations[i];
            if ((eventUser != null && owner != eventUser) ||
                !_objectives.TryGetCondition<TCondition>(handle, out var condition) ||
                condition.CounterKey != key ||
                !MatchesEventTarget(condition, primaryTarget ?? observed, secondaryTarget))
            {
                continue;
            }

            _objectives.TryAddConditionProgress(handle, 1);
        }
    }

    /// <summary>
    /// Gets the prototype ID of an entity for condition-specific event keys.
    /// </summary>
    protected bool TryGetPrototypeId(EntityUid? uid, out EntProtoId prototype)
    {
        prototype = default;
        if (uid is not { } target || Prototype(target) is not { } entityPrototype)
            return false;

        prototype = entityPrototype.ID;
        return true;
    }

    /// <summary>
    /// Copies active registrations from an observed source to a newly-created related entity.
    /// </summary>
    protected void CopyObserverRegistrations(
        Entity<TObserverComponent> source,
        EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return;

        var targetObserver = EnsureComp<TObserverComponent>(target);
        _emptyObservers.Remove(target);
        var registrations = new List<KeyValuePair<ObjectiveConditionHandle, EntityUid>>(
            source.Comp.Registrations);
        for (var i = 0; i < registrations.Count; i++)
        {
            var (handle, ownerUid) = registrations[i];
            if (!_objectives.TryGetCondition<TCondition>(handle, out _))
                continue;

            targetObserver.Registrations[handle] = ownerUid;
            OnObserverRegistrationAdded((target, targetObserver), handle);
            if (TryComp(ownerUid, out TOwnerComponent? owner) &&
                owner.ObservedEntities.TryGetValue(handle, out var observed))
            {
                observed.Add(target);
            }
        }
    }

    private void RefreshObservers(
        ObjectiveConditionContext context,
        TCondition condition,
        TOwnerComponent owner)
    {
        if (condition.Target == null && !condition.ObserveAnyWithoutTarget)
            return;

        if (!owner.ObservedEntities.TryGetValue(context.Handle, out var observed))
        {
            observed = [];
            owner.ObservedEntities.Add(context.Handle, observed);
        }

        RemoveDeletedObservers(context.Handle, observed);
        TryObserve(context, condition, context.Owner, context.Owner, observed);

        if ((context.Options.ObservationScope & ObjectiveObservationScope.Nearby) != 0)
        {
            foreach (var target in _lookup.GetEntitiesInRange(context.Owner, context.Options.ObservationRange))
            {
                TryObserve(context, condition, context.Owner, target, observed);
            }
        }

        if ((context.Options.ObservationScope & ObjectiveObservationScope.Hands) != 0 &&
            TryComp(context.Owner, out HandsComponent? hands))
        {
            foreach (var target in _hands.EnumerateHeld((context.Owner, hands)))
            {
                TryObserve(context, condition, context.Owner, target, observed);
            }
        }

        if ((context.Options.ObservationScope & ObjectiveObservationScope.Equipment) != 0 &&
            TryComp(context.Owner, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
            {
                if (_inventory.TryGetSlotEntity(context.Owner, slot.Name, out var item, inventory))
                    TryObserve(context, condition, context.Owner, item.Value, observed);
            }
        }

        if ((context.Options.ObservationScope & ObjectiveObservationScope.Actions) == 0 ||
            !TryComp(context.Owner, out ActionsComponent? actions))
        {
            return;
        }

        foreach (var action in actions.Actions)
        {
            TryObserve(context, condition, context.Owner, action, observed);
        }
    }

    private void TryObserve(
        ObjectiveConditionContext context,
        TCondition condition,
        EntityUid ownerEntity,
        EntityUid target,
        HashSet<EntityUid> observed)
    {
        if (TerminatingOrDeleted(target) || !ShouldObserve(condition, target) || !observed.Add(target))
            return;

        var observer = EnsureComp<TObserverComponent>(target);
        _emptyObservers.Remove(target);
        observer.Registrations[context.Handle] = ownerEntity;
        OnObserverRegistrationAdded((target, observer), context.Handle);
    }

    /// <summary>
    /// Allows a concrete condition family to attach condition-specific observer state.
    /// </summary>
    protected virtual void OnObserverRegistrationAdded(
        Entity<TObserverComponent> observer,
        ObjectiveConditionHandle handle)
    {
    }

    /// <summary>
    /// Allows a concrete condition family to remove condition-specific observer state.
    /// </summary>
    protected virtual void OnObserverRegistrationRemoved(
        Entity<TObserverComponent> observer,
        ObjectiveConditionHandle handle)
    {
    }

    /// <summary>
    /// Determines whether a target should receive this condition registration.
    /// </summary>
    protected virtual bool ShouldObserve(TCondition condition, EntityUid target)
    {
        if (condition.Target == null)
            return condition.ObserveAnyWithoutTarget;

        return TryGetPrototypeId(target, out var prototype) && prototype == condition.Target.Value;
    }

    /// <summary>
    /// Determines whether event targets match condition-specific target filters.
    /// </summary>
    protected virtual bool MatchesEventTarget(
        TCondition condition,
        EntityUid? primary,
        EntityUid? secondary)
    {
        var expected = condition.Target;
        if (expected == null)
            return true;

        return TryGetPrototypeId(primary, out var primaryPrototype) && primaryPrototype == expected.Value ||
               TryGetPrototypeId(secondary, out var secondaryPrototype) && secondaryPrototype == expected.Value;
    }

    private void RemoveDeletedObservers(ObjectiveConditionHandle handle, HashSet<EntityUid> observed)
    {
        List<EntityUid>? removed = null;
        foreach (var target in observed)
        {
            if (!TerminatingOrDeleted(target) && TryComp(target, out TObserverComponent? observer) &&
                observer.Registrations.ContainsKey(handle))
            {
                continue;
            }

            removed ??= [];
            removed.Add(target);
        }

        if (removed == null)
            return;

        for (var i = 0; i < removed.Count; i++)
        {
            observed.Remove(removed[i]);
        }
    }

    private void RemoveObserverRegistration(EntityUid target, ObjectiveConditionHandle handle)
    {
        if (TerminatingOrDeleted(target) || !TryComp(target, out TObserverComponent? observer))
            return;

        observer.Registrations.Remove(handle);
        OnObserverRegistrationRemoved((target, observer), handle);
        if (observer.Registrations.Count == 0)
            _emptyObservers.Add(target);
    }

    private void CleanupEmptyOwners()
    {
        foreach (var owner in _emptyOwners)
        {
            if (!TerminatingOrDeleted(owner) &&
                TryComp(owner, out TOwnerComponent? component) &&
                component.Registrations.Count == 0)
            {
                RemComp<TOwnerComponent>(owner);
            }
        }

        _emptyOwners.Clear();
    }

    private void CleanupEmptyObservers()
    {
        foreach (var observer in _emptyObservers)
        {
            if (!TerminatingOrDeleted(observer) &&
                TryComp(observer, out TObserverComponent? component) &&
                component.Registrations.Count == 0)
            {
                RemComp<TObserverComponent>(observer);
            }
        }

        _emptyObservers.Clear();
    }
}
