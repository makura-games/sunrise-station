using Content.Client._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Content.Client._Sunrise.TimeCounterContainer;
using Content.Client.UserInterface.Screens;
using Robust.Shared.Player;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Sunrise.Tutorial;

public sealed partial class TimeCounterSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private EntityQuery<TimeCounterUiComponent> _timeCounterUiQuery = default!;
    private LayoutContainer _timeCounterRoot = default!;
    /// <summary>
    /// Why? The in-game screen hierarchy is recreated after OnScreenChanged.
    /// Soo we have to wait one frame before reattaching controls.
    /// </summary>
    private bool _pendingRefresh;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimeCounterComponent, AfterAutoHandleStateEvent>(OnTimeCounterState);
        SubscribeLocalEvent<TimeCounterComponent, ComponentInit>(OnTimeCounterInit);
        SubscribeLocalEvent<TimeCounterComponent, ComponentShutdown>(OnTimeCounterShutdown);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);

        _timeCounterRoot = new LayoutContainer();
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
            _timeCounterRoot.Orphan();
            RemoveAllCounters();
            _pendingRefresh = false;
            return;
        }

        _pendingRefresh = true;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        _pendingRefresh = true;
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _timeCounterRoot.Orphan();
        RemoveAllCounters();
        _pendingRefresh = false;
    }

    private void OnTimeCounterState(Entity<TimeCounterComponent> ent, ref AfterAutoHandleStateEvent ev)
    {
        UpdateTimeCounter(ent);
    }

    private void OnTimeCounterInit(Entity<TimeCounterComponent> ent, ref ComponentInit ev)
    {
        UpdateTimeCounter(ent);
    }

    private void OnTimeCounterShutdown(Entity<TimeCounterComponent> ent, ref ComponentShutdown ev)
    {
        RemoveTimeCounter(ent.Owner);
    }

    private void UpdateTimeCounter(Entity<TimeCounterComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner)
        {
            RemoveTimeCounter(ent.Owner);
            return;
        }

        if (ent.Comp.EndTime == null || ent.Comp.EndTime == TimeSpan.Zero)
        {
            RemoveTimeCounter(ent.Owner);
            return;
        }

        if (_ui.ActiveScreen is not InGameScreen)
            return;

        var viewportContainer = _ui.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");

        var style = new TimeCounterStyle
        {
            FontSize = ent.Comp.FontSize,
            BackgroundColor = ent.Comp.BackgroundColor,
            BorderColor = ent.Comp.BorderColor,
            Centered = ent.Comp.Centered,
            DefaultColor = ent.Comp.DefaultColor,
            WarningColor = ent.Comp.WarningColor,
            CriticalColor = ent.Comp.CriticalColor,
            WarningTime = ent.Comp.WarningTime,
            CriticalTime = ent.Comp.CriticalTime
        };

        if (_timeCounterUiQuery.TryGetComponent(ent.Owner, out var timeUi) && timeUi.Counter != null)
            timeUi.Counter.Orphan();

        var counter = new TimeCounter(ent.Comp.EndTime, style);
        if (ent.Comp.ScreenPosition is { } position)
            counter.SetPosition(position, ent.Comp.Centered);
        else
            counter.SetTopCenter();

        SetTimeCounterRoot(viewportContainer, counter);
        var counterUi = EnsureComp<TimeCounterUiComponent>(ent.Owner);
        counterUi.Counter = counter;
    }

    private void SetTimeCounterRoot(LayoutContainer root, TimeCounter counter)
    {
        _timeCounterRoot.Orphan();

        if (counter.Parent != _timeCounterRoot)
            _timeCounterRoot.AddChild(counter);

        root.AddChild(_timeCounterRoot);

        LayoutContainer.SetAnchorPreset(_timeCounterRoot, LayoutContainer.LayoutPreset.TopWide);
        _timeCounterRoot.SetPositionLast();
    }

    private void RemoveTimeCounter(EntityUid uid)
    {
        if (!_timeCounterUiQuery.TryGetComponent(uid, out var counterUi) || counterUi.Counter == null)
            return;

        counterUi.Counter.Orphan();
        RemComp<TimeCounterUiComponent>(uid);
    }

    private void RefreshCounters()
    {
        var query = EntityQueryEnumerator<TimeCounterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateTimeCounter((uid, comp));
        }
    }

    private void RemoveAllCounters()
    {
        var query = EntityQueryEnumerator<TimeCounterUiComponent>();
        while (query.MoveNext(out var uid, out var ui))
        {
            ui.Counter?.Orphan();
            RemCompDeferred<TimeCounterUiComponent>(uid);
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
        RefreshCounters();
    }
}
