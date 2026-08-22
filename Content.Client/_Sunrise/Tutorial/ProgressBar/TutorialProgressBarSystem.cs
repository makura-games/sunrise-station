using System;
using Content.Client.UserInterface.Screens;
using Content.Client._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Sunrise.Tutorial.Events;

namespace Content.Client._Sunrise.Tutorial.ProgressBar;

/// <summary>
/// Client-side system that attaches tutorial progress bars to the active in-game viewport.
/// </summary>
public sealed partial class TutorialProgressBarSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityQuery<ProgressBarUiComponent> _progressUiQuery = default!;
    private LayoutContainer? _progressBarRoot;
    private bool _pendingRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialProgressBarComponent, ComponentInit>(OnProgressInit);
        SubscribeLocalEvent<TutorialProgressBarComponent, ComponentShutdown>(OnProgressShutdown);
        SubscribeLocalEvent<TutorialProgressBarComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<TutorialProgressBarComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<TutorialProgressBarComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);

        _progressBarRoot = new LayoutContainer();
        _ui.OnScreenChanged += OnScreenChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _ui.OnScreenChanged -= OnScreenChanged;
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) ev)
    {
        if (ev.New is not InGameScreen)
        {
            _progressBarRoot?.Orphan();
            RemoveAllBars();
            _pendingRefresh = false;
            return;
        }

        // The viewport controls are recreated with the screen, so refresh on the
        // next frame after the new InGameScreen is active.
        _pendingRefresh = true;
    }

    private void OnPlayerAttached(Entity<TutorialProgressBarComponent> ent, ref LocalPlayerAttachedEvent ev)
    {
        _pendingRefresh = true;
    }
    private void OnPlayerDetached(Entity<TutorialProgressBarComponent> ent, ref LocalPlayerDetachedEvent ev)
    {
        _progressBarRoot?.Orphan();
        RemoveAllBars();
        _pendingRefresh = false;
    }
    private void OnAfterAutoHandleState(Entity<TutorialProgressBarComponent> ent, ref AfterAutoHandleStateEvent ev)
    {
        UpdateProgressBar(ent);
    }

    private void OnProgressInit(Entity<TutorialProgressBarComponent> ent, ref ComponentInit ev)
    {
        UpdateProgressBar(ent);
    }

    private void OnProgressShutdown(Entity<TutorialProgressBarComponent> ent, ref ComponentShutdown ev)
    {
        RemoveProgressBar(ent.Owner);
    }
    private void UpdateProgressBar(EntityUid uid)
    {
        if (_player.LocalEntity != uid)
            return;

        if (!TryComp(uid, out TutorialProgressBarComponent? progressComp))
            return;

        if (!TryComp(uid, out TutorialPlayerComponent? player))
            return;

        if (_ui.ActiveScreen is not InGameScreen)
            return;

        var viewportContainer = _ui.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");

        if (!_proto.TryIndex(player.SequenceId, out var sequence))
            return;

        var total = sequence.Steps.Count;
        if (total <= 0)
            return;

        var current = Math.Clamp(progressComp.CurrentStepIndex, 0, total);
        var progress = (float)current / total;

        if (_progressUiQuery.TryGetComponent(uid, out var ui) && ui.Bar != null)
        {
            ui.Bar.SetLabel(Loc.GetString("tutorial-progress-label"));
            ui.Bar.SetProgress(progress);
            LayoutContainer.SetAnchorAndMarginPreset(ui.Bar, LayoutContainer.LayoutPreset.BottomWide, margin: 80);
            SetProgressBarRoot(viewportContainer, ui.Bar);
            return;
        }

        var bar = new TutorialProgressBar();

        bar.SetLabel(Loc.GetString("tutorial-progress-label"));
        bar.SetProgress(progress);
        LayoutContainer.SetAnchorAndMarginPreset(bar, LayoutContainer.LayoutPreset.BottomWide, margin: 80);
        SetProgressBarRoot(viewportContainer, bar);

        var uiComp = EnsureComp<ProgressBarUiComponent>(uid);
        uiComp.Bar = bar;
    }
    private void SetProgressBarRoot(LayoutContainer root, TutorialProgressBar bar)
    {
        _progressBarRoot?.Orphan();

        if (bar.Parent != _progressBarRoot)
            _progressBarRoot?.AddChild(bar);

        if (_progressBarRoot == null)
            return;

        root.AddChild(_progressBarRoot);
        LayoutContainer.SetAnchorPreset(_progressBarRoot, LayoutContainer.LayoutPreset.Wide);
        _progressBarRoot.SetPositionLast();
    }

    private void RemoveProgressBar(EntityUid uid)
    {
        if (!_progressUiQuery.TryGetComponent(uid, out var ui) || ui.Bar == null)
            return;

        ui.Bar.Orphan();
        RemComp<ProgressBarUiComponent>(uid);
    }

    private void RefreshBars()
    {
        var query = EntityQueryEnumerator<TutorialProgressBarComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            UpdateProgressBar(uid);
        }
    }

    private void RemoveAllBars()
    {
        var query = EntityQueryEnumerator<ProgressBarUiComponent>();
        while (query.MoveNext(out var uid, out var ui))
        {
            ui.Bar?.Orphan();
            RemCompDeferred<ProgressBarUiComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_pendingRefresh)
            return;

        if (_ui.ActiveScreen is not InGameScreen)
            return;

        _pendingRefresh = false;
        RefreshBars();
    }
}
