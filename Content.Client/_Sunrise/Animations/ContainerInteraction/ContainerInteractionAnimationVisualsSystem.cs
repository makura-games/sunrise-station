using System.Numerics;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Animations.ContainerInteraction;

public sealed partial class ContainerInteractionAnimationVisualsSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    /// <summary>
    /// Минимальный скейл, который будет у спрайта при анимации.
    /// Используется как нижняя граница допустимого скейла, чтобы проиграть хоть сколько-то видимую анимацию.
    /// </summary>
    private const float MinimumScale = 1.1f;

    private const string InsertTrack = "insert-animation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainerInteractionAnimationVisualsComponent, EntInsertedIntoContainerMessage>(HandleEvent);
        SubscribeLocalEvent<ContainerInteractionAnimationVisualsComponent, EntRemovedFromContainerMessage>(HandleEvent);

    }

    private void HandleEvent<T>(Entity<ContainerInteractionAnimationVisualsComponent> ent, ref T args)
    {
        TryDoAnimation(ent);
    }

    private bool TryDoAnimation(Entity<ContainerInteractionAnimationVisualsComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted || IsClientSide(ent))
            return false;

        if (!_spriteQuery.TryComp(ent, out var sprite))
            return false;

        if (_animation.HasRunningAnimation(ent, InsertTrack))
            return false;

        DoAnimation(ent, sprite);
        return true;
    }

    private void DoAnimation(Entity<ContainerInteractionAnimationVisualsComponent> ent, SpriteComponent sprite)
    {
        var targetScaleX = ent.Comp.Scale + _random.NextFloat(-ent.Comp.ScaleVariation, ent.Comp.ScaleVariation);
        var targetScaleY = ent.Comp.Scale + _random.NextFloat(-ent.Comp.ScaleVariation, ent.Comp.ScaleVariation);

        // Чтобы была хоть какая-то видимая анимация, если после рандома значение получилось слишком маленьким.
        targetScaleX = MathF.Max(targetScaleX, MinimumScale);
        targetScaleY = MathF.Max(targetScaleY, MinimumScale);

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.Duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    Property = nameof(SpriteComponent.Scale),
                    ComponentType = typeof(SpriteComponent),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(targetScaleX, targetScaleY), 0f),
                        new AnimationTrackProperty.KeyFrame(sprite.Scale, ent.Comp.Duration),
                    },
                },
            },
        };

        _animation.Play(ent, animation, InsertTrack);
    }
}
