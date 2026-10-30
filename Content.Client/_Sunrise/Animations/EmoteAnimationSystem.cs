using System.Numerics;
using Content.Shared._Sunrise.Animations;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Animations;

public sealed partial class EmoteAnimationSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animationSystem = default!;
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<string, Action<EntityUid>> _emoteList = new();

    public const string AnimationKey = "emoteAnimationKeyId";
    private const string AnimationKeyTurn = "emoteAnimationKeyId_rotate";
    private const string JumpAnimationKey = "jump";

    public override void Initialize()
    {
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentHandleState>(OnHandleState);

        _emoteList.Add("Flip", uid =>
        {
            _spriteAnimation.PlayRotation(uid, AnimationKey,
                (Angle.FromDegrees(-10), 0f),
                (Angle.FromDegrees(180), 0.25f),
                (Angle.FromDegrees(360), 0.25f),
                (Angle.Zero, 0f));
        });
        _emoteList.Add("Jump", uid =>
        {
            _spriteAnimation.PlayOffset(uid,
                JumpAnimationKey,
                true,
                (Vector2.Zero, 0f),
                (new Vector2(0f, 0.3f), 0.125f),
                (new Vector2(0f, 0.7f), 0.125f),
                (new Vector2(0f, 0.3f), 0.125f),
                (Vector2.Zero, 0.125f));
        });

        _emoteList.Add("Dance", uid =>
        {
            if (_animationSystem.HasRunningAnimation(uid, AnimationKeyTurn))
                return;

            var animation = new Animation
            {
                Length = TimeSpan.FromMilliseconds(900),
                AnimationTracks =
                {
                    new AnimationTrackComponentProperty
                    {
                        ComponentType = typeof(TransformComponent),
                        Property = nameof(TransformComponent.LocalRotation),
                        InterpolationMode = AnimationInterpolationMode.Linear,
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(0), 0f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.075f),
                            new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.075f),
                        }
                    }
                }
            };

            _animationSystem.Play(uid, animation, AnimationKeyTurn);
        });
    }

    private void OnHandleState(EntityUid uid, EmoteAnimationComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not EmoteAnimationComponent.EmoteAnimationComponentState state)
            return;

        var replay = component.StartedAt == state.StartedAt;
        component.AnimationId = state.AnimationId;
        component.StartedAt = state.StartedAt;
        var elapsed = MathF.Max(0f, (float) (_timing.CurTime - state.StartedAt).TotalSeconds);
        var duration = state.AnimationId == "Dance" ? 0.9f : 0.5f;
        if (replay || elapsed >= duration)
            return;

        if (_emoteList.TryGetValue(component.AnimationId, out var value))
        {
            value.Invoke(uid);
            if (component.AnimationId == "Jump")
                _spriteAnimation.Seek(uid, JumpAnimationKey, elapsed);
            else if (component.AnimationId == "Flip")
                _spriteAnimation.Seek(uid, AnimationKey, elapsed);
        }
    }
}
