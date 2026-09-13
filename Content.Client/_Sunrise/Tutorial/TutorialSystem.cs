using System;
using System.Collections.Generic;
using Content.Client._Sunrise.PlayerCache;
using Content.Client.UserInterface.Screens;
using Content.Client._Sunrise.Tutorial.Components;
using Content.Client._Sunrise.Tutorial.Overlays;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.EntitySystems;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Client._Sunrise.Tutorial.TutorialBubbleControl;
using Content.Client._Sunrise.Tutorial.UiHighlight;

namespace Content.Client._Sunrise.Tutorial;

/// <summary>
/// Client-side tutorial controller for bubbles, target highlighting, path overlay, and tutorial menu requests.
/// </summary>
public sealed partial class TutorialSystem : SharedTutorialSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PlayerCacheManager _playerCache = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly ProtoId<ShaderPrototype> TutorialShader = "TutorialTargetOutline";
    private static readonly Color TargetOutlineColor = Color.FromHex("#FFF15C");

    private const float TargetOutlineMinAlpha = 0.65f;
    private const float TargetOutlineMaxAlpha = 1f;
    private const float TargetOutlineMinWidth = 1.5f;
    private const float TargetOutlineMaxWidth = 4f;
    private const float HighlightPulseSpeed = 3.5f;

    private ShaderInstance? _shaderInstance;
    private EntityUid? _highlightedTarget;
    private uint? _highlightedTargetRenderOrder;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    /// <summary>
    /// Raised after the server sends the list of completed tutorial sequences.
    /// </summary>
    public event Action? WindowDataReceived;

    /// <summary>
    /// Whether completion data has been received from the server during this client session.
    /// </summary>
    public bool CompletedTutorialsReceived { get; private set; }

    /// <summary>
    /// Prototype IDs of tutorial sequences completed by the local user.
    /// </summary>
    public readonly HashSet<string> CompletedTutorials = [];

    [Dependency] private EntityQuery<TutorialBubbleUiComponent> _bubbleUiQuery = default!;
    private LayoutContainer? _tutorialBubbleRoot;
    private TutorialUiHighlightOverlay? _uiHighlightOverlay;

    public override void Initialize()
    {
        base.Initialize();

        _input.FirstChanceOnKeyEvent += OnFirstChanceKeyEvent;

        SubscribeLocalEvent<TutorialBubbleComponent, AfterAutoHandleStateEvent>(AfterAutoHandleState);
        SubscribeLocalEvent<TutorialBubbleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<TutorialBubbleComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<TutorialPlayerComponent, AfterAutoHandleStateEvent>(OnTutorialPlayerState);
        SubscribeLocalEvent<TutorialBubbleComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<TutorialBubbleComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<TutorialWindowDataResponseEvent>(OnWindowDataResponse);
        SubscribeNetworkEvent<TutorialStartDeniedEvent>(OnStartDenied);

        _tutorialBubbleRoot = new LayoutContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        _ui.OnScreenChanged += OnScreenChanged;

        _shaderInstance = _proto.Index(TutorialShader).InstanceUnique();
        _overlayManager.AddOverlay(new TutorialPathOverlay(EntityManager, _player, _timing, _transform, _proto));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_highlightedTarget == null || _shaderInstance == null)
            return;

        var pulse = (MathF.Sin((float)_timing.RealTime.TotalSeconds * HighlightPulseSpeed) + 1f) * 0.5f;
        var alpha = TargetOutlineMinAlpha + (TargetOutlineMaxAlpha - TargetOutlineMinAlpha) * pulse;
        var width = TargetOutlineMinWidth + (TargetOutlineMaxWidth - TargetOutlineMinWidth) * pulse;
        _shaderInstance.SetParameter("outline_color", TargetOutlineColor.WithAlpha(alpha));
        _shaderInstance.SetParameter("outline_width", width);
    }

    private void OnFirstChanceKeyEvent(KeyEventArgs args, KeyEventType type)
    {
        if (type == KeyEventType.Up || args.Key != Keyboard.Key.Escape)
            return;

        if (_player.LocalEntity is not { } localPlayer)
            return;

        if (!TryComp(localPlayer, out TutorialPlayerComponent? tutorialPlayer))
            return;

        if (!tutorialPlayer.TutorialInitialized)
            return;

        if (!TryGetCurrentStep((localPlayer, tutorialPlayer), out var step))
            return;

        if (!step.BlockUiInteraction)
            return;

        args.Handle();
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) ev)
    {
        if (ev.New is not InGameScreen)
        {
            SetHighlight(null);
            ClearUiHighlight();
            _tutorialBubbleRoot?.Orphan();
            return;
        }

        RefreshBubbles();
        RefreshUiHighlight();
    }

    private void OnStartDenied(TutorialStartDeniedEvent msg, EntitySessionEventArgs args)
    {
        if (string.IsNullOrEmpty(msg.Reason))
            return;

        _ui.Popup(msg.Reason, null, false);
    }

    private void OnPlayerAttached(Entity<TutorialBubbleComponent> ent, ref LocalPlayerAttachedEvent ev)
    {
        RefreshBubbles();
    }

    private void OnPlayerDetached(Entity<TutorialBubbleComponent> ent, ref LocalPlayerDetachedEvent ev)
    {
        SetHighlight(null);
        ClearUiHighlight();
        _tutorialBubbleRoot?.Orphan();
    }

    private void OnTutorialPlayerState(Entity<TutorialPlayerComponent> ent, ref AfterAutoHandleStateEvent ev)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        // Clear highlight if target changed or was removed.
        // The new highlight is applied in OnComponentInit/AfterAutoHandleState of TutorialBubbleComponent,
        // so path and highlight always appear together with the bubble.
        if (ent.Comp.CurrentBubbleTarget != _highlightedTarget)
            SetHighlight(null);

        RefreshBubbles();
        UpdateUiHighlight(ent);
    }

    private void SetHighlight(EntityUid? target)
    {
        if (_highlightedTarget == target)
            return;

        // Remove from old target
        if (_highlightedTarget is { } old && _spriteQuery.TryGetComponent(old, out var oldSprite))
        {
            if (oldSprite.PostShader == _shaderInstance)
            {
                oldSprite.PostShader = null;
                if (_highlightedTargetRenderOrder is { } renderOrder)
                    oldSprite.RenderOrder = renderOrder;
            }
        }

        _highlightedTarget = null;
        _highlightedTargetRenderOrder = null;

        // Apply to new target — never highlight the local player
        if (target is not { } newTarget || newTarget == _player.LocalEntity)
            return;

        if (!_spriteQuery.TryGetComponent(newTarget, out var sprite))
            return;

        // Don't overwrite an existing unrelated post-shader
        if (sprite.PostShader != null && sprite.PostShader != _shaderInstance)
            return;

        _highlightedTarget = target;
        _highlightedTargetRenderOrder = sprite.RenderOrder;
        sprite.PostShader = _shaderInstance;
        sprite.RenderOrder = EntityManager.CurrentTick.Value;
    }

    private void AfterAutoHandleState(Entity<TutorialBubbleComponent> ent, ref AfterAutoHandleStateEvent ev)
    {
        SetHighlight(ent);
        UpdateBubble(ent);
    }

    private void OnComponentInit(Entity<TutorialBubbleComponent> ent, ref ComponentInit ev)
    {
        SetHighlight(ent);
        UpdateBubble(ent);
    }

    private void OnComponentShutdown(Entity<TutorialBubbleComponent> ent, ref ComponentShutdown ev)
    {
        if (_highlightedTarget == ent.Owner)
            SetHighlight(null);
        RemoveBubble(ent);
    }

    private void RefreshBubbles()
    {
        var query = EntityQueryEnumerator<TutorialBubbleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateBubble((uid, comp));
        }
    }

    private void RemoveBubble(EntityUid uid)
    {
        if (!_bubbleUiQuery.TryGetComponent(uid, out var bubbleUi) || bubbleUi.Bubble == null)
            return;

        var bubble = bubbleUi.Bubble;
        bubbleUi.Bubble = null;
        RemComp<TutorialBubbleUiComponent>(uid);

        bubble.DisposeAllChildren();
        bubble.Orphan();
    }

    private void UpdateBubble(Entity<TutorialBubbleComponent> ent)
    {
        if (string.IsNullOrEmpty(ent.Comp.Instruction))
        {
            RemoveBubble(ent.Owner);
            return;
        }

        if (_ui.ActiveScreen is not InGameScreen)
            return;

        if (_bubbleUiQuery.TryGetComponent(ent.Owner, out var uiComp) && uiComp.Bubble != null)
        {
            if (uiComp.LastInstruction == ent.Comp.Instruction)
            {
                // Text unchanged — just ensure parenting is correct.
                SetSpeechBubbleRoot(uiComp.Bubble);
                return;
            }

            // Instruction changed — discard existing bubble and fall through to create a fresh one.
            // This gives consistent behaviour regardless of whether the network delivered the
            // change as a component update or as a remove + re-add.
            RemoveBubble(ent.Owner);
        }

        var bubble = TutorialBubble.Create(
            Loc.GetString(ent.Comp.Instruction),
            ent);

        SetSpeechBubbleRoot(bubble);

        var bubbleUi = EnsureComp<TutorialBubbleUiComponent>(ent.Owner);
        bubbleUi.Bubble = bubble;
        bubbleUi.LastInstruction = ent.Comp.Instruction;
    }

    private void SetSpeechBubbleRoot(TutorialBubble bubble)
    {
        if (_tutorialBubbleRoot == null)
            return;

        var root = _ui.PopupRoot;

        if (bubble.Parent != _tutorialBubbleRoot)
            _tutorialBubbleRoot.AddChild(bubble);

        // Only re-parent when necessary — orphaning every update causes a blank render tick.
        if (_tutorialBubbleRoot.Parent != root)
        {
            _tutorialBubbleRoot.Orphan();
            root.AddChild(_tutorialBubbleRoot);
            LayoutContainer.SetAnchorPreset(_tutorialBubbleRoot, LayoutContainer.LayoutPreset.Wide);
        }

        _tutorialBubbleRoot.SetPositionLast();
    }

    private void RefreshUiHighlight()
    {
        if (_player.LocalEntity is not { } localPlayer ||
            !TryComp(localPlayer, out TutorialPlayerComponent? tutorialPlayer))
        {
            ClearUiHighlight();
            return;
        }

        UpdateUiHighlight((localPlayer, tutorialPlayer));
    }

    private void UpdateUiHighlight(Entity<TutorialPlayerComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        if (!ent.Comp.TutorialInitialized ||
            !TryGetCurrentStep(ent, out var step) ||
            !NeedsUiOverlay(step))
        {
            ClearUiHighlight();
            return;
        }

        SetUiHighlight(step.UiHighlight, step.BlockUiInteraction);
    }

    private static bool HasUiHighlight(TutorialStepPrototype step)
    {
        return step.UiHighlight.Count > 0;
    }

    private static bool NeedsUiOverlay(TutorialStepPrototype step)
    {
        return HasUiHighlight(step) || step.BlockUiInteraction;
    }

    private void SetUiHighlight(IReadOnlyList<TutorialUiHighlightSelector> selectors, bool blockInput)
    {
        if (_ui.ActiveScreen is not InGameScreen)
        {
            ClearUiHighlight();
            return;
        }

        var resolverRoot = _ui.RootControl;
        var overlayRoot = _ui.PopupRoot;
        _uiHighlightOverlay ??= new TutorialUiHighlightOverlay(resolverRoot, selectors, blockInput);
        _uiHighlightOverlay.SetTarget(resolverRoot, selectors, blockInput);

        if (_uiHighlightOverlay.Parent != overlayRoot)
        {
            _uiHighlightOverlay.Orphan();
            overlayRoot.AddChild(_uiHighlightOverlay);
            LayoutContainer.SetAnchorPreset(_uiHighlightOverlay, LayoutContainer.LayoutPreset.Wide);
        }

        _uiHighlightOverlay.SetPositionLast();
        if (_tutorialBubbleRoot?.Parent != null)
            _tutorialBubbleRoot.SetPositionLast();

        if (!blockInput)
            return;

        _ui.ControlFocused = null;
    }

    private void ClearUiHighlight()
    {
        _uiHighlightOverlay?.Orphan();
    }

    /// <summary>
    /// Requests that the server ends the local player's active tutorial session.
    /// </summary>
    public void RequestQuitTutorial()
    {
        RaiseNetworkEvent(new TutorialQuitRequestEvent());
    }

    /// <summary>
    /// Requests the server-side list of tutorials completed by the local user.
    /// </summary>
    public void RequestWindowData()
    {
        RaiseNetworkEvent(new TutorialWindowDataRequestEvent());
    }

    /// <summary>
    /// Requests that the server starts the selected tutorial sequence for the local user.
    /// </summary>
    public void RequestStartTutorial(ProtoId<TutorialSequencePrototype> sequenceId)
    {
        RaiseNetworkEvent(new TutorialStartRequestEvent(sequenceId));
    }

    /// <summary>
    /// Records that the local user has acted on the first-join tutorial prompt.
    /// </summary>
    public void MarkTutorialPromptSeen()
    {
        _playerCache.SetTutorialPromptSeen();
    }

    public bool IsTutorialPromptSeen()
    {
        return _playerCache.IsTutorialPromptSeen();
    }

    public float GetTutorialPromptSkipDelay()
    {
        return _cfg.GetCVar(SunriseCCVars.TutorialPromptSkipDelay);
    }

    private void OnWindowDataResponse(TutorialWindowDataResponseEvent msg, EntitySessionEventArgs args)
    {
        CompletedTutorials.Clear();
        CompletedTutorials.UnionWith(msg.CompletedTutorials);
        CompletedTutorialsReceived = true;
        WindowDataReceived?.Invoke();
    }

    public override void Shutdown()
    {
        _input.FirstChanceOnKeyEvent -= OnFirstChanceKeyEvent;
        base.Shutdown();
        SetHighlight(null);
        ClearUiHighlight();
        _ui.OnScreenChanged -= OnScreenChanged;
        _overlayManager.RemoveOverlay<TutorialPathOverlay>();
        _shaderInstance?.Dispose();
        _shaderInstance = null;
    }
}
