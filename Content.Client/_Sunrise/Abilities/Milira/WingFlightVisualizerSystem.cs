using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Sunrise.Abilities.Milira;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Abilities.Milira;

/// <summary>
/// Клиентская визуализация полёта милиры, плавное изменение масштаба наподобие как у броска предмета
/// </summary>
public sealed partial class WingFlightVisualizerSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;

    private const string AnimationKey = "wing-flight-scale";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WingFlightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WingFlightComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<WingFlightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WingFlightComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!ent.Comp.OriginalScale.HasValue)
            ent.Comp.OriginalScale = sprite.Scale;

        ApplyScale((ent.Owner, ent.Comp, sprite), immediate: true);
    }

    private void OnState(Entity<WingFlightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!ent.Comp.OriginalScale.HasValue)
            ent.Comp.OriginalScale = sprite.Scale;

        var baseScale = ent.Comp.OriginalScale.Value;
        var targetScale = baseScale * ent.Comp.CurrentScaleMultiplier;
        var currentScale = sprite.Scale;

        if (MathHelper.CloseTo(targetScale.Length(), currentScale.Length(), 0.001f))
            return;

        ApplyScale((ent.Owner, ent.Comp, sprite), immediate: false);
    }

    private void OnShutdown(Entity<WingFlightComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.OriginalScale.HasValue)
            return;

        var scale = ent.Comp.OriginalScale.Value;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _animation.Stop(ent.Owner, AnimationKey);
        _spriteSystem.SetScale((ent.Owner, sprite), scale);
    }

    private void ApplyScale(Entity<WingFlightComponent, SpriteComponent> ent, bool immediate)
    {
        var baseScale = ent.Comp1.OriginalScale ?? ent.Comp2.Scale;
        var targetScale = baseScale * ent.Comp1.CurrentScaleMultiplier;

        if (immediate)
        {
            _spriteSystem.SetScale((ent.Owner, ent.Comp2), targetScale);
            return;
        }

        var animationPlayer = EnsureComp<AnimationPlayerComponent>(ent.Owner);

        if (_animation.HasRunningAnimation(ent.Owner, animationPlayer, AnimationKey))
            _animation.Stop(ent.Owner, AnimationKey);

        var currentScale = ent.Comp2.Scale;
        if (MathHelper.CloseTo(targetScale.Length(), currentScale.Length(), 0.001f))
            return;

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(0.25f),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(currentScale, 0f),
                        new AnimationTrackProperty.KeyFrame(targetScale, 0.25f),
                    },
                    InterpolationMode = AnimationInterpolationMode.Linear
                }
            }
        };

        _animation.Play((ent.Owner, animationPlayer), anim, AnimationKey);
    }
}

