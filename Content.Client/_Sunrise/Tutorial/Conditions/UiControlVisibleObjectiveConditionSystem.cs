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
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Tutorial.Conditions;

public sealed class UiControlVisibleObjectiveConditionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedTutorialSystem _tutorial = default!;

    private readonly List<UiControlVisibleObjectiveCondition> _activeConditions = [];
    private readonly HashSet<string> _reportedControls = [];

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

        RefreshVisibleControls();
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) args)
    {
        ClearState();
        RefreshVisibleControls();
    }

    private void RefreshVisibleControls()
    {
        if (!TryGetTutorialContext(out var context))
        {
            ClearState();
            return;
        }

        if (IsContextChanged(context))
            SetActiveContext(context);

        ReportVisibleControls(context.Root);
    }

    private bool TryGetTutorialContext(out TutorialVisibleContext context)
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

        context = new TutorialVisibleContext(screen, _ui.RootControl, player, tutorialPlayer, step);
        return true;
    }

    private bool IsContextChanged(TutorialVisibleContext context)
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

    private void SetActiveContext(TutorialVisibleContext context)
    {
        ClearState();
        _activeScreen = context.Screen;
        _activePlayer = context.Player;
        _activeStep = context.TutorialPlayer.StepIndex;
        _activeSequence = context.TutorialPlayer.SequenceId;
        _activeOverride = context.TutorialPlayer.ActiveStepOverride;
        CollectVisibleConditions(context.Step);
    }

    private void CollectVisibleConditions(TutorialStepPrototype step)
    {
        CollectVisibleConditions(step.Completion.All);
        CollectVisibleConditions(step.Completion.Any);
        if (step.Preconditions != null)
        {
            CollectVisibleConditions(step.Preconditions.All);
            CollectVisibleConditions(step.Preconditions.Any);
        }

        for (var i = 0; i < step.Failures.Count; i++)
        {
            CollectVisibleConditions(step.Failures[i].When.All);
            CollectVisibleConditions(step.Failures[i].When.Any);
        }
    }

    private void CollectVisibleConditions(List<ObjectiveCondition> conditions)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] is UiControlVisibleObjectiveCondition condition &&
                !string.IsNullOrWhiteSpace(condition.Control) &&
                condition.Selectors.Count > 0)
            {
                _activeConditions.Add(condition);
            }
        }
    }

    private void ReportVisibleControls(Control root)
    {
        for (var i = 0; i < _activeConditions.Count; i++)
        {
            var condition = _activeConditions[i];

            if (_reportedControls.Contains(condition.Control))
                continue;

            if (!_resolver.TryFind(root, condition.Selectors, out var control))
                continue;

            if (!control.VisibleInTree || control.PixelSize.X <= 0 || control.PixelSize.Y <= 0)
                continue;

            _reportedControls.Add(condition.Control);
            RaiseNetworkEvent(new TutorialUiControlVisibleEvent(condition.Control));
        }
    }

    private void ClearState()
    {
        _activeConditions.Clear();
        _reportedControls.Clear();
        _activePlayer = null;
        _activeSequence = null;
        _activeOverride = null;
        _activeStep = -1;
        _activeScreen = null;
    }

    private readonly record struct TutorialVisibleContext(
        InGameScreen Screen,
        Control Root,
        EntityUid Player,
        TutorialPlayerComponent TutorialPlayer,
        TutorialStepPrototype Step);
}
