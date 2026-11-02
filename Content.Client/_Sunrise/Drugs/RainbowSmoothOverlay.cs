// ванильный RainbowOverlay не даёт нужного поведения (там резкий рост/падение интенсивности)
// RainbowSmoothOverlay рисует эффект с плавной огибающей (fadeIn/fadeOut)
using Content.Shared.CCVar;
using Content.Shared._Sunrise.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Client._Sunrise.Drugs;

public sealed partial class RainbowSmoothOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntitySystemManager _sysMan = default!;
    private StatusEffectsSystem _status = default!;

    private RainbowSmoothOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingRainbowsWeakStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<SeeingRainbowsWeakStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<SeeingRainbowsWeakStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<SeeingRainbowsWeakStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);
        _overlay = new();
        _status = _sysMan.GetEntitySystem<StatusEffectsSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnRemoved(Entity<SeeingRainbowsWeakStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;
        if (_overlay != null)
        {
            _overlay.Intoxication = 0;
            _overlay.TimeTicker = 0;
            _overlay.CachedEndTime = null;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnApplied(Entity<SeeingRainbowsWeakStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;
        if (_overlay == null)
            _overlay = new();
        _overlay.Phase = _random.NextFloat(MathF.Tau);
        // Кэшируем реальное время окончания эффекта
        if (_status.TryGetEffectsEndTimeWithComp<SeeingRainbowsWeakStatusEffectComponent>(args.Target, out var endTime))
            _overlay.CachedEndTime = endTime ?? TimeSpan.MaxValue;
        else
            _overlay.CachedEndTime = _timing.CurTime + TimeSpan.FromSeconds(60.0f); // fallback
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<SeeingRainbowsWeakStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        // Защита от ретрансляции событий нелокальной цели
        if (_player.LocalEntity != args.Args.Entity)
            return;
        if (_overlay == null)
            _overlay = new();
        _overlayMan.AddOverlay(_overlay);
        // Восстановить CachedEndTime если возможно
        if (_status.TryGetEffectsEndTimeWithComp<SeeingRainbowsWeakStatusEffectComponent>(args.Args.Entity, out var endTime))
            _overlay.CachedEndTime = endTime ?? TimeSpan.MaxValue;
    }

    private void OnPlayerDetached(Entity<SeeingRainbowsWeakStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        if (_player.LocalEntity != args.Args.Entity)
            return;
        if (_overlay == null)
            return;
        _overlay.Intoxication = 0;
        _overlay.TimeTicker = 0;
        _overlay.CachedEndTime = null;
        _overlayMan.RemoveOverlay(_overlay);
    }
}

public sealed partial class RainbowSmoothOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Rainbow";

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;


    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _rainbowShader;

    public float Intoxication = 0.0f;
    public float TimeTicker = 0.0f;
    public float Phase = 0.0f;

    private float _timeScale = 0.0f;
    private float _warpScale = 0.0f;
    private const float FadeSeconds = 10.0f;
    public TimeSpan? CachedEndTime { get; set; }

    private float EffectScale => Intoxication;

    public RainbowSmoothOverlay()
    {
        IoCManager.InjectDependencies(this);

        _rainbowShader = _prototypeManager.Index(Shader).InstanceUnique();
        _config.OnValueChanged(CCVars.ReducedMotion, OnReducedMotionChanged, invokeImmediately: true);
    }

    private void OnReducedMotionChanged(bool reducedMotion)
    {
        _timeScale = reducedMotion ? 0.0f : 1.0f;
        _warpScale = reducedMotion ? 0.0f : 1.0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
            return;

        if (CachedEndTime == null)
            return;

        var timeLeft = (float)(CachedEndTime.Value - _timing.CurTime).TotalSeconds;

        TimeTicker += args.DeltaSeconds;

        var fadeIn = Math.Clamp(TimeTicker / FadeSeconds, 0.0f, 1.0f);
        var fadeOut = Math.Clamp(timeLeft / FadeSeconds, 0.0f, 1.0f);
        var envelope = MathF.Min(fadeIn, fadeOut);

        float intensity = _entityManager.TryGetComponent(playerEntity, out SeeingRainbowsWeakStatusEffectComponent? comp)
            ? comp.Intensity
            : 0.1f;
        Intoxication = Math.Clamp(intensity * envelope, 0.0f, 1.0f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        return player != null &&
               _entityManager.TryGetComponent(player, out EyeComponent? eyeComp) &&
               args.Viewport.Eye == eyeComp.Eye &&
               EffectScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;
        var handle = args.WorldHandle;
        _rainbowShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _rainbowShader.SetParameter("colorScale", EffectScale);
        _rainbowShader.SetParameter("timeScale", _timeScale);
        _rainbowShader.SetParameter("warpScale", _warpScale * EffectScale);
        _rainbowShader.SetParameter("phase", Phase);
        handle.UseShader(_rainbowShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
