using System.Collections.Generic;
using Content.Shared.Actions.Components;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

public abstract partial class SharedTutorialSystem
{
    private void ResetTracking(Entity<TutorialPlayerComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var tracker = EnsureComp<TutorialTrackerComponent>(ent);
        tracker.Counters.Clear();
        UpdateObservedEntities(ent, tracker);
        Dirty(ent, tracker);
    }

    private void ClearTracking(Entity<TutorialPlayerComponent> ent)
    {
        if (!TryComp<TutorialTrackerComponent>(ent, out var tracker))
            return;

        foreach (var observed in tracker.ObservedEntities)
        {
            RemoveObserver(ent, observed);
        }

        tracker.ObservedEntities.Clear();
        tracker.TargetPrototypes.Clear();
        tracker.Counters.Clear();
        if (!TerminatingOrDeleted(ent))
            Dirty(ent, tracker);
    }

    private void UpdateObservedEntities(Entity<TutorialPlayerComponent> ent, TutorialTrackerComponent tracker)
    {
        foreach (var observed in tracker.ObservedEntities)
        {
            RemoveObserver(ent, observed);
        }

        tracker.ObservedEntities.Clear();
        tracker.TargetPrototypes.Clear();

        if (!TryGetCurrentStep(ent, out var step))
            return;

        // Условия, отслеживающие события, определяют сущности, на которые нужно добавить TutorialObservableComponent.
        CollectObservedConditions(tracker, step.Conditions);
        CollectObservedConditions(tracker, step.AnyConditions);
        CollectObservedConditions(tracker, step.Preconditions);
        CollectObservedFailureConditions(tracker, step.Failures);

        ObserveNearbyEntities(ent, tracker, step);
        ObserveEquippedEntities(ent, tracker);
        ObserveActionEntities(ent, tracker);
    }

    private static void CollectObservedConditions(
        TutorialTrackerComponent tracker,
        List<TutorialCondition> conditions)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            if (condition is not IEventListenedCondition listened)
                continue;

            if (listened.Target != null)
            {
                tracker.TargetPrototypes.Add(listened.Target.Value);
                continue;
            }

            if (listened.ObserveAnyWithoutTarget)
                tracker.Counters.TryAdd((listened.ObserveKey, default), 0);
        }
    }

    private static void CollectObservedFailureConditions(
        TutorialTrackerComponent tracker,
        List<TutorialFailureRule> failures)
    {
        for (var i = 0; i < failures.Count; i++)
        {
            var failure = failures[i];
            CollectObservedConditions(tracker, failure.Conditions);
            CollectObservedConditions(tracker, failure.AnyConditions);
        }
    }

    private void ObserveNearbyEntities(EntityUid user, TutorialTrackerComponent tracker, TutorialStepPrototype step)
    {
        if (tracker.TargetPrototypes.Count == 0 && !ObservesAny(tracker))
            return;

        foreach (var uid in _lookupSystem.GetEntitiesInRange(user, step.ObserveRange))
        {
            TryObserveEntityInternal(user, uid, tracker);
        }
    }

    private void ObserveEquippedEntities(EntityUid user, TutorialTrackerComponent tracker)
    {
        if (tracker.TargetPrototypes.Count == 0 && !ObservesAny(tracker))
            return;

        if (TryComp(user, out HandsComponent? hands))
        {
            foreach (var held in _hands.EnumerateHeld((user, hands)))
            {
                TryObserveEntityInternal(user, held, tracker);
            }
        }

        if (!TryComp(user, out InventoryComponent? inventory))
            return;

        foreach (var slot in inventory.Slots)
        {
            if (_inventory.TryGetSlotEntity(user, slot.Name, out var item, inventory))
            {
                TryObserveEntityInternal(user, item.Value, tracker);
            }
        }
    }

    private void ObserveActionEntities(EntityUid user, TutorialTrackerComponent tracker)
    {
        if (tracker.TargetPrototypes.Count == 0 && !ObservesAny(tracker))
            return;

        if (!TryComp(user, out ActionsComponent? actions))
            return;

        foreach (var action in actions.Actions)
        {
            TryObserveEntityInternal(user, action, tracker);
        }
    }

    /// <summary>
    /// Starts tracking tutorial-relevant events from <paramref name="target"/> for <paramref name="user"/>.
    /// </summary>
    public void TryObserveEntity(EntityUid user, EntityUid target, TutorialTrackerComponent tracker)
    {
        if (!TryObserveEntityInternal(user, target, tracker))
            return;

        Dirty(user, tracker);
    }

    private bool TryObserveEntityInternal(EntityUid user, EntityUid target, TutorialTrackerComponent tracker)
    {
        if (TerminatingOrDeleted(user) || TerminatingOrDeleted(target))
            return false;

        if (!ShouldObserveEntity(target, tracker))
            return false;

        if (!tracker.ObservedEntities.Add(target))
            return false;

        var observable = EnsureComp<TutorialObservableComponent>(target);
        observable.Observers.Add(user);
        Dirty(target, observable);
        return true;
    }

    private void RemoveObserver(EntityUid user, EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return;

        if (!TryComp(target, out TutorialObservableComponent? observable))
            return;

        if (!observable.Observers.Remove(user))
            return;

        if (observable.Observers.Count == 0)
        {
            RemComp<TutorialObservableComponent>(target);
            return;
        }

        Dirty(target, observable);
    }

    private bool ShouldObserveEntity(EntityUid target, TutorialTrackerComponent tracker)
    {
        if (ObservesAny(tracker))
            return true;

        return TryGetPrototypeId(target, out var protoId) && tracker.TargetPrototypes.Contains(protoId);
    }

    private static bool ObservesAny(TutorialTrackerComponent tracker)
    {
        foreach (var (key, target) in tracker.Counters.Keys)
        {
            if (target.Equals(default(EntProtoId)) &&
                key.EndsWith(EventListenedConditionKeys.ObserveSuffix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
