using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared.Chasm;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

#pragma warning disable IDE0130
namespace Content.Client.Chasm;

public sealed partial class ChasmFallingVisualsSystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    private void StartFallingAnimation(Entity<ChasmFallingComponent> ent, SpriteComponent sprite)
    {
        var spriteEntity = (ent.Owner, sprite);
        if (_spriteAnimation.IsPlaying(spriteEntity, ChasmFallAnimationKey))
            return;

        ent.Comp.OriginalScale = _spriteAnimation.GetBaseScale(spriteEntity);
        _spriteAnimation.PlayScale(spriteEntity, GetFallingAnimation(ent.Comp), ChasmFallAnimationKey, hold: true);
    }

    private void StopFallingAnimation(Entity<SpriteComponent?> ent)
    {
        _spriteAnimation.Stop(ent, ChasmFallAnimationKey);
    }

    private static Animation GetFallingAnimation(ChasmFallingComponent component)
    {
        var scale = new Vector2(
            component.OriginalScale.X == 0f ? 1f : component.AnimationScale.X / component.OriginalScale.X,
            component.OriginalScale.Y == 0f ? 1f : component.AnimationScale.Y / component.OriginalScale.Y);

        return new Animation
        {
            Length = component.AnimationTime,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.One, 0f),
                        new AnimationTrackProperty.KeyFrame(scale, (float) component.AnimationTime.TotalSeconds),
                    },
                },
            },
        };
    }
}
