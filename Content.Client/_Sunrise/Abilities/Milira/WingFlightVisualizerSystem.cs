using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared._Sunrise.Abilities.Milira;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Abilities.Milira;

/// <summary>
/// Плавный множитель масштаба при полёте милиры, независимый от остальных эффектов
/// </summary>
public sealed partial class WingFlightVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    private const string AnimationKey = "wing-flight-scale";
    private const float TransitionDuration = 0.25f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WingFlightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WingFlightComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<WingFlightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WingFlightComponent> ent, ref ComponentStartup args)
    {
        ApplyScale(ent, true);
    }

    private void OnState(Entity<WingFlightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyScale(ent, false);
    }

    private void OnShutdown(Entity<WingFlightComponent> ent, ref ComponentShutdown args)
    {
        _spriteAnimation.Stop(ent.Owner, AnimationKey);
    }

    private void ApplyScale(Entity<WingFlightComponent> ent, bool immediate)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var target = new Vector2(ent.Comp.CurrentScaleMultiplier);
        if (immediate)
        {
            _spriteAnimation.SetScaleContribution((ent.Owner, sprite), AnimationKey, target);
            return;
        }

        var current = _spriteAnimation.GetScaleMultiplier(ent.Owner, AnimationKey);
        if (Vector2.DistanceSquared(current, target) < 0.000001f)
            return;

        _spriteAnimation.PlayScale(ent.Owner, AnimationKey, true,
            (current, 0f), (target, TransitionDuration));
    }
}
