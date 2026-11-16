using System;
using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.EntitySystems;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Tutorial.Conditions;

public sealed partial class UiControlVisibleConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, UiControlVisibleCondition>
{
    [Dependency] private SharedTutorialSystem _tutorial = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TutorialUiControlVisibleEvent>(OnUiControlVisible);
    }

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<UiControlVisibleCondition> args)
    {
        if (args.Condition.Count <= 0)
        {
            args.Result = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(args.Condition.Control))
            return;

        if (!TryComp<TutorialTrackerComponent>(entity, out var tracker))
            return;

        args.Result = tracker.Counters.TryGetValue((args.Condition.CounterKey, default), out var count) &&
                      count >= args.Condition.Count;
    }

    private void OnUiControlVisible(TutorialUiControlVisibleEvent msg, EntitySessionEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(msg.Control))
            return;

        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp(player, out TutorialPlayerComponent? tutorialPlayer) ||
            !tutorialPlayer.TutorialInitialized)
        {
            return;
        }

        if (!_tutorial.TryGetCurrentStep((player, tutorialPlayer), out var step) ||
            !StepHasControlCondition(step, msg.Control))
        {
            return;
        }

        var tracker = EnsureComp<TutorialTrackerComponent>(player);
        var key = (UiControlVisibleCondition.GetCounterKey(msg.Control), default(EntProtoId));
        tracker.Counters.TryGetValue(key, out var count);
        tracker.Counters[key] = count + 1;
        Dirty(player, tracker);
    }

    private static bool StepHasControlCondition(TutorialStepPrototype step, string control)
    {
        return ConditionsHaveControl(step.Preconditions, control) ||
               ConditionsHaveControl(step.Conditions, control) ||
               ConditionsHaveControl(step.AnyConditions, control);
    }

    private static bool ConditionsHaveControl(List<TutorialCondition> conditions, string control)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] is UiControlVisibleCondition condition &&
                string.Equals(condition.Control, control, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
