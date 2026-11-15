using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Animations.ContainerInteraction;

public sealed partial class ContainerInteractionAnimationVisualsSystem : EntitySystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;
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
        SubscribeLocalEvent<ContainerInteractionAnimationVisualsComponent, ComponentShutdown>(OnShutdown);

    }

    private void HandleEvent<T>(Entity<ContainerInteractionAnimationVisualsComponent> ent, ref T args)
    {
        TryDoAnimation(ent);
    }

    private bool TryDoAnimation(Entity<ContainerInteractionAnimationVisualsComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted || IsClientSide(ent))
            return false;

        if (!_spriteQuery.HasComp(ent))
            return false;

        if (_spriteAnimation.IsPlaying(ent.Owner, InsertTrack))
            return false;

        DoAnimation(ent);
        return true;
    }

    private void DoAnimation(Entity<ContainerInteractionAnimationVisualsComponent> ent)
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
                        new AnimationTrackProperty.KeyFrame(Vector2.One, ent.Comp.Duration),
                    },
                },
            },
        };

        _spriteAnimation.PlayScale(ent.Owner, animation, InsertTrack);
    }

    private void OnShutdown(Entity<ContainerInteractionAnimationVisualsComponent> ent, ref ComponentShutdown args)
    {
        _spriteAnimation.Stop(ent.Owner, InsertTrack);
    }
}
