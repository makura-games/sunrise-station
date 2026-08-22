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

public sealed partial class UiButtonPressedConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, UiButtonPressedCondition>
{
    [Dependency] private SharedTutorialSystem _tutorial = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TutorialUiButtonPressedEvent>(OnUiButtonPressed);
    }

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<UiButtonPressedCondition> args)
    {
        if (args.Condition.Count <= 0)
        {
            args.Result = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(args.Condition.Button))
            return;

        if (!TryComp<TutorialTrackerComponent>(entity, out var tracker))
            return;

        args.Result = tracker.Counters.TryGetValue((args.Condition.CounterKey, default), out var count) &&
                      count >= args.Condition.Count;
    }

    private void OnUiButtonPressed(TutorialUiButtonPressedEvent msg, EntitySessionEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(msg.Button))
            return;

        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp(player, out TutorialPlayerComponent? tutorialPlayer) ||
            !tutorialPlayer.TutorialInitialized)
        {
            return;
        }

        if (!_tutorial.TryGetCurrentStep((player, tutorialPlayer), out var step) ||
            !StepHasButtonCondition(step, msg.Button))
        {
            return;
        }

        var tracker = EnsureComp<TutorialTrackerComponent>(player);
        var key = (UiButtonPressedCondition.GetCounterKey(msg.Button), default(EntProtoId));
        tracker.Counters.TryGetValue(key, out var count);
        tracker.Counters[key] = count + 1;
        Dirty(player, tracker);
    }

    private static bool StepHasButtonCondition(TutorialStepPrototype step, string button)
    {
        return ConditionsHaveButton(step.Preconditions, button) ||
               ConditionsHaveButton(step.Conditions, button) ||
               ConditionsHaveButton(step.AnyConditions, button);
    }

    private static bool ConditionsHaveButton(List<TutorialCondition> conditions, string button)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] is UiButtonPressedCondition condition &&
                string.Equals(condition.Button, button, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
