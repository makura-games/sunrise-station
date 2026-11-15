using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared.Chasm;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Chasm;

/// <summary>
/// Handles the falling animation for entities that fall into an entity with <see cref="ChasmComponent"/>.
/// </summary>
public sealed partial class ChasmFallingVisualsSystem : EntitySystem
{
    // Sunrise edit start - падение использует общий множитель спрайта
    /*
    [Dependency] private AnimationPlayerSystem _anim = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private EntityQuery<AnimationPlayerComponent> _animationPlayerQuery;
    */
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;
    // Sunrise edit end
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery;

    private const string ChasmFallAnimationKey = "chasm_fall";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmFallingComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ChasmFallingComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(Entity<ChasmFallingComponent> entity, ref ComponentInit args)
    {
        if (!_spriteQuery.TryComp(entity, out var sprite) ||
            TerminatingOrDeleted(entity))
        {
            return;
        }

        // Sunrise edit start - отдельный множитель вместо изменения базы
        /*
        entity.Comp.OriginalScale = sprite.Scale;

        if (!_animationPlayerQuery.TryComp(entity, out var player) ||
            _anim.HasRunningAnimation(player, ChasmFallAnimationKey))
        {
            return;
        }

        _anim.Play((entity, player), GetFallingAnimation(entity.Comp), ChasmFallAnimationKey);
        */
        var spriteEntity = (entity.Owner, sprite);
        if (_spriteAnimation.IsPlaying(spriteEntity, ChasmFallAnimationKey))
            return;

        entity.Comp.OriginalScale = _spriteAnimation.GetBaseScale(spriteEntity);
        _spriteAnimation.PlayScale(spriteEntity, GetFallingAnimation(entity.Comp), ChasmFallAnimationKey, hold: true);
        // Sunrise edit end
    }

    private void OnComponentRemove(Entity<ChasmFallingComponent> entity, ref ComponentRemove args)
    {
        if (!_spriteQuery.TryComp(entity, out var sprite))
        {
            return;
        }

        // Sunrise edit start - снимаем только вклад падения, актуальная база сохраняется
        /*
        _sprite.SetScale((entity, sprite), entity.Comp.OriginalScale);

        if (!_animationPlayerQuery.TryComp(entity, out var player) ||
            !_anim.HasRunningAnimation(player, ChasmFallAnimationKey))
        {
            return;
        }

        _anim.Stop((entity, player), ChasmFallAnimationKey);
        */
        _spriteAnimation.Stop((entity.Owner, sprite), ChasmFallAnimationKey);
        // Sunrise edit end
    }

    private static Animation GetFallingAnimation(ChasmFallingComponent component)
    {
        return new Animation
        {
            Length = component.AnimationTime,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        // Sunrise edit start - нулевая ось базы остаётся нулевой
                        /*
                        new AnimationTrackProperty.KeyFrame(component.OriginalScale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(component.AnimationScale, component.AnimationTime.Seconds),
                        */
                        new AnimationTrackProperty.KeyFrame(Vector2.One, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(
                            component.OriginalScale.X == 0f ? 1f : component.AnimationScale.X / component.OriginalScale.X,
                            component.OriginalScale.Y == 0f ? 1f : component.AnimationScale.Y / component.OriginalScale.Y),
                            (float) component.AnimationTime.TotalSeconds),
                        // Sunrise edit end
                    },
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                },
            },
        };
    }
}
