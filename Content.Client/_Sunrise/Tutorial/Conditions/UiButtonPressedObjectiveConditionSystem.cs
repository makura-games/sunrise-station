using System;
using System.Collections.Generic;
using Content.Client.UserInterface.Screens;
using Content.Client._Sunrise.Tutorial.UiHighlight;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.EntitySystems;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Tutorial.Conditions;

public sealed class UiButtonPressedObjectiveConditionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedTutorialSystem _tutorial = default!;

    private readonly List<UiButtonPressedObjectiveCondition> _activeConditions = [];
    private readonly Dictionary<UiButtonPressedObjectiveCondition, ButtonSubscription> _subscriptions = [];

    private TutorialUiControlResolver _resolver = default!;
    private EntityUid? _activePlayer;
    private ProtoId<TutorialSequencePrototype>? _activeSequence;
    private ProtoId<TutorialStepPrototype>? _activeOverride;
    private int _activeStep = -1;
    private UIScreen? _activeScreen;

    public override void Initialize()
    {
        base.Initialize();

        _resolver = new TutorialUiControlResolver(EntityManager);

        _ui.OnScreenChanged += OnScreenChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _ui.OnScreenChanged -= OnScreenChanged;
        ClearState();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RefreshButtonSubscriptions();
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) args)
    {
        ClearState();
        RefreshButtonSubscriptions();
    }

    private void RefreshButtonSubscriptions()
    {
        if (!TryGetTutorialContext(out var context))
        {
            ClearState();
            return;
        }

        if (IsContextChanged(context))
        {
            SetActiveContext(context);
        }

        UpdateConditionSubscriptions(context.Root);
    }

    private bool TryGetTutorialContext(out TutorialButtonContext context)
    {
        context = default;

        if (_ui.ActiveScreen is not InGameScreen screen)
            return false;

        if (_player.LocalEntity is not { } player)
            return false;

        if (!TryComp(player, out TutorialPlayerComponent? tutorialPlayer))
            return false;

        if (!tutorialPlayer.TutorialInitialized)
            return false;

        if (!_tutorial.TryGetCurrentStep((player, tutorialPlayer), out var step))
            return false;

        context = new TutorialButtonContext(screen, _ui.RootControl, player, tutorialPlayer, step);
        return true;
    }

    private bool IsContextChanged(TutorialButtonContext context)
    {
        if (_activeScreen != context.Screen)
            return true;

        if (_activePlayer != context.Player)
            return true;

        if (_activeStep != context.TutorialPlayer.StepIndex)
            return true;

        if (_activeOverride != context.TutorialPlayer.ActiveStepOverride)
            return true;

        if (_activeSequence is not { } activeSequence)
            return true;

        return !activeSequence.Equals(context.TutorialPlayer.SequenceId);
    }

    private void SetActiveContext(TutorialButtonContext context)
    {
        ClearState();
        _activeScreen = context.Screen;
        _activePlayer = context.Player;
        _activeStep = context.TutorialPlayer.StepIndex;
        _activeSequence = context.TutorialPlayer.SequenceId;
        _activeOverride = context.TutorialPlayer.ActiveStepOverride;
        CollectButtonConditions(context.Step);
    }

    private void CollectButtonConditions(TutorialStepPrototype step)
    {
        CollectButtonConditions(step.Completion.All);
        CollectButtonConditions(step.Completion.Any);
        if (step.Preconditions != null)
        {
            CollectButtonConditions(step.Preconditions.All);
            CollectButtonConditions(step.Preconditions.Any);
        }

        for (var i = 0; i < step.Failures.Count; i++)
        {
            CollectButtonConditions(step.Failures[i].When.All);
            CollectButtonConditions(step.Failures[i].When.Any);
        }
    }

    private void CollectButtonConditions(List<ObjectiveCondition> conditions)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] is UiButtonPressedObjectiveCondition condition &&
                !string.IsNullOrWhiteSpace(condition.Button) &&
                condition.Selectors.Count > 0)
            {
                _activeConditions.Add(condition);
            }
        }
    }

    private void UpdateConditionSubscriptions(Control root)
    {
        for (var i = 0; i < _activeConditions.Count; i++)
        {
            var condition = _activeConditions[i];

            if (!_resolver.TryFind(root, condition.Selectors, out var control))
            {
                RemoveSubscription(condition);
                continue;
            }

            if (_subscriptions.TryGetValue(condition, out var subscription) &&
                subscription.Control == control)
            {
                continue;
            }

            RemoveSubscription(condition);
            AddSubscription(condition, control);
        }
    }

    private void AddSubscription(UiButtonPressedObjectiveCondition condition, Control control)
    {
        Action<GUIBoundKeyEventArgs>? keyHandler = null;
        Action<BaseButton.ButtonEventArgs>? pressedHandler = null;

        if (control is BaseButton button)
        {
            pressedHandler = _ => RaiseButtonPressed(condition.Button);
            button.OnPressed += pressedHandler;
        }
        else
        {
            keyHandler = args => OnControlKeyBindDown(condition.Button, args);
            control.OnKeyBindDown += keyHandler;
        }

        _subscriptions[condition] = new ButtonSubscription(control, keyHandler, pressedHandler);
    }

    private void RemoveSubscription(UiButtonPressedObjectiveCondition condition)
    {
        if (!_subscriptions.Remove(condition, out var subscription))
            return;

        RemoveSubscription(subscription);
    }

    private void ClearState()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            RemoveSubscription(subscription);
        }

        _subscriptions.Clear();
        _activeConditions.Clear();
        _activePlayer = null;
        _activeSequence = null;
        _activeOverride = null;
        _activeStep = -1;
        _activeScreen = null;
    }

    private void OnControlKeyBindDown(string button, GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        RaiseButtonPressed(button);
    }

    private void RaiseButtonPressed(string button)
    {
        RaiseNetworkEvent(new TutorialUiButtonPressedEvent(button));
    }

    private static void RemoveSubscription(ButtonSubscription subscription)
    {
        if (subscription.KeyHandler != null)
            subscription.Control.OnKeyBindDown -= subscription.KeyHandler;

        if (subscription.PressedHandler != null &&
            subscription.Control is BaseButton button)
        {
            button.OnPressed -= subscription.PressedHandler;
        }
    }

    private sealed record ButtonSubscription(
        Control Control,
        Action<GUIBoundKeyEventArgs>? KeyHandler,
        Action<BaseButton.ButtonEventArgs>? PressedHandler);

    private readonly record struct TutorialButtonContext(
        InGameScreen Screen,
        Control Root,
        EntityUid Player,
        TutorialPlayerComponent TutorialPlayer,
        TutorialStepPrototype Step);
}
