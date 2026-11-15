using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared.Gravity;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

#pragma warning disable IDE0130
namespace Content.Client.Gravity;

public sealed partial class FloatingVisualizerSystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    private void OnFloatingShutdown(Entity<FloatingVisualsComponent> ent, ref ComponentShutdown args)
    {
        _spriteAnimation.Stop(ent.Owner, ent.Comp.AnimationKey);
    }

    private void StartFloatAnimation(EntityUid uid, Vector2 offset, string key, float duration, bool stop)
    {
        if (stop)
        {
            _spriteAnimation.Stop(uid, key);
            return;
        }

        if (duration <= 0f)
            return;

        _spriteAnimation.PlayLoop(uid, key, Play, CanPlay);
        return;

        void Play(EntityUid target) => PlayAnimation(target, offset, key, duration);
        bool CanPlay(EntityUid target) => !TryComp<FloatingVisualsComponent>(target, out var floating) || floating.CanFloat;
    }

    private void PlayAnimation(EntityUid uid, Vector2 offset, string animationKey, float animationTime)
    {
        var animation = new Animation
        {
            // Длительность учитывает подъём и возвращение к базе
            Length = TimeSpan.FromSeconds(animationTime*2),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(offset, animationTime),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, animationTime),
                    }
                }
            }
        };

        _spriteAnimation.PlayOffset(uid, animation, animationKey, Vector2.Zero);
    }
}
