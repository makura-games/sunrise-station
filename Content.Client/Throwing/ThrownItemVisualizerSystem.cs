using Content.Shared.Throwing;
using Robust.Client.Animations;
// Sunrise added start
using Content.Client._Sunrise.Animations;
using System.Numerics;
using Robust.Shared.Timing;
// Sunrise added end
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Throwing;

/// <summary>
///     Handles animating thrown items.
/// </summary>
public sealed partial class ThrownItemVisualizerSystem : EntitySystem
{
    // Sunrise edit start - перевод на новую систему
    [Dependency] private SpriteAnimationSystem _anim = default!;
    [Dependency] private IGameTiming _timing = default!;
    // Sunrise edit end

    private const string AnimationKey = "thrown-item";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrownItemComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
        SubscribeLocalEvent<ThrownItemComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnAutoHandleState(EntityUid uid, ThrownItemComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Sunrise edit start - время броска могло пройти, пока предмет был вне pvs
        if (!component.Animate || component.LandTime == null || component.LandTime <= _timing.CurTime)
        {
            _anim.Stop(uid, AnimationKey);
            return;
        }

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        // Sunrise edit end

        var anim = GetAnimation((uid, component, sprite));
        if (anim == null)
            return;

        // Sunrise edit start - восстановление анимации по серверному времени
        _anim.PlayScale(uid, anim, AnimationKey);
        _anim.Seek(uid, AnimationKey, (float) (_timing.CurTime - component.ThrownTime!.Value).TotalSeconds);
        // Sunrise edit end
    }

    private void OnShutdown(EntityUid uid, ThrownItemComponent component, ComponentShutdown args)
    {
        // Sunrise edit start - перевод на новую систему
        _anim.Stop(uid, AnimationKey);
        // Sunrise edit end
    }

    private static Animation? GetAnimation(Entity<ThrownItemComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.LandTime - ent.Comp1.ThrownTime is not { } length)
            return null;

        if (length <= TimeSpan.Zero)
            return null;

        var scale = Vector2.One; // Sunrise-Edit - множитель относительно базы
        var lenFloat = (float)length.TotalSeconds;

        // TODO use like actual easings here
        return new Animation
        {
            Length = length,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(scale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(scale * 1.4f, lenFloat * 0.25f),
                        new AnimationTrackProperty.KeyFrame(scale, lenFloat * 0.75f)
                    },
                    InterpolationMode = AnimationInterpolationMode.Linear
                }
            }
        };
    }
}
