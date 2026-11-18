using System.Numerics;
using Content.Client._Sunrise.Animations; // Sunrise-Edit
using Content.Shared.Gravity;
using Robust.Client.GameObjects;
using Robust.Client.Animations;
using Robust.Shared.Animations;

namespace Content.Client.Gravity;

/// <inheritdoc/>
public sealed partial class FloatingVisualizerSystem : SharedFloatingVisualizerSystem
{
    [Dependency] private SpriteAnimationSystem AnimationSystem = default!; // Sunrise-Edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FloatingVisualsComponent, ComponentShutdown>((ent, ref _) => AnimationSystem.Stop(ent.Owner, ent.Comp.AnimationKey)); // Sunrise-Edit
    }

    // Sunrise edit start - перенос в новую систему анимации
    /// <inheritdoc/>
    public override void FloatAnimation(EntityUid uid, Vector2 offset, string animationKey, float animationTime, bool stop = false)
    {
        if (stop)
        {
            AnimationSystem.Stop(uid, animationKey);
            return;
        }

        if (animationTime <= 0f)
            return;

        AnimationSystem.PlayLoop(uid, animationKey,
            target => PlayAnimation(target, offset, animationKey, animationTime),
            target => TryComp<FloatingVisualsComponent>(target, out var floating) && floating.CanFloat);
    }

    private void PlayAnimation(EntityUid uid, Vector2 offset, string animationKey, float animationTime)
    {
        var animation = new Animation
        {
            // We multiply by the number of extra keyframes to make time for them
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

        AnimationSystem.PlayOffset(uid, animation, animationKey, Vector2.Zero);
    }
    // Sunrise edit end

}
