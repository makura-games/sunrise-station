using System.Numerics;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared._Sunrise.Animations;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;

namespace Content.Client._Sunrise.Animations;

public sealed partial class EmoteAnimationSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    private readonly Dictionary<ProtoId<EmotePrototype>, EmoteAnimation> _emotes = new();

    private static readonly ProtoId<EmotePrototype> Flip = "Flip";
    private static readonly ProtoId<EmotePrototype> Jump = "Jump";
    private static readonly ProtoId<EmotePrototype> Dance = "Dance";

    private const string FlipAnimationKey = "emote-flip";
    private const string DanceAnimationKey = "emote-dance";
    private const string JumpAnimationKey = "jump";
    private const float EmoteDuration = 0.5f;
    private const int DanceDurationMilliseconds = 900;
    private const float DanceDuration = DanceDurationMilliseconds / 1000f;

    public override void Initialize()
    {
        base.Initialize();
        _emotes.Add(Flip, new EmoteAnimation(PlayFlipAnimation, EmoteDuration));
        _emotes.Add(Jump, new EmoteAnimation(PlayJumpAnimation, EmoteDuration));
        _emotes.Add(Dance, new EmoteAnimation(PlayDanceAnimation, DanceDuration));
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, EmoteAnimationComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not EmoteAnimationComponentState state)
            return;

        component.AnimationId = state.AnimationId;
        if (_emotes.TryGetValue(state.AnimationId, out var emote))
            emote.Play(uid, 0f);
    }

    private void PlayFlipAnimation(EntityUid uid, float elapsed)
    {
        const float halfTurnDuration = EmoteDuration / 2f;
        var windup = (Angle.FromDegrees(-10), 0f);
        var halfTurn = (Angle.FromDegrees(180), halfTurnDuration);
        var fullTurn = (Angle.FromDegrees(360), halfTurnDuration);
        var resetToBase = (Angle.Zero, 0f);

        _spriteAnimation.PlayRotation(uid, FlipAnimationKey, windup, halfTurn, fullTurn, resetToBase);
        _spriteAnimation.Seek(uid, FlipAnimationKey, elapsed);
    }

    private void PlayJumpAnimation(EntityUid uid, float elapsed)
    {
        const float stepDuration = EmoteDuration / 4f;
        var start = (Vector2.Zero, 0f);
        var takeoff = (new Vector2(0f, 0.3f), stepDuration);
        var peak = (new Vector2(0f, 0.7f), stepDuration);
        var landing = (new Vector2(0f, 0.3f), stepDuration);
        var resetToBase = (Vector2.Zero, stepDuration);

        _spriteAnimation.PlayOffset(uid, JumpAnimationKey, true, start, takeoff, peak, landing, resetToBase);
        _spriteAnimation.Seek(uid, JumpAnimationKey, elapsed);
    }

    private void PlayDanceAnimation(EntityUid uid, float elapsed)
    {
        if (_animation.HasRunningAnimation(uid, DanceAnimationKey))
            return;

        const float turnDuration = 0.075f;
        var animation = new Animation
        {
            Length = TimeSpan.FromMilliseconds(DanceDurationMilliseconds),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(TransformComponent),
                    Property = nameof(TransformComponent.LocalRotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), turnDuration),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, turnDuration),
                    }
                }
            }
        };

        if (elapsed > 0f)
        {
            var track = (AnimationTrackComponentProperty) animation.AnimationTracks[0];
            var (index, time) = track.AdvancePlayback(uid, -1, 0f, elapsed);
            track.KeyFrames.RemoveRange(0, index + 1);
            if (track.KeyFrames.Count > 0)
            {
                var next = track.KeyFrames[0];
                track.KeyFrames[0] = new AnimationTrackProperty.KeyFrame(next.Value, next.KeyTime - time, next.Easing);
            }
            track.KeyFrames.Insert(0, new AnimationTrackProperty.KeyFrame(Transform(uid).LocalRotation, 0f));
            animation.Length -= TimeSpan.FromSeconds(elapsed);
        }

        _animation.Play(uid, animation, DanceAnimationKey);
    }
    private sealed record EmoteAnimation(Action<EntityUid, float> Play, float Duration);
}
