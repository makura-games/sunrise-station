using System.Numerics;
using Content.Client._Sunrise.Animations; // Sunrise-Edit
using Content.Shared.Jittering;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Random;

namespace Content.Client.Jittering
{
    public sealed partial class JitteringSystem : SharedJitteringSystem
    {
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SpriteAnimationSystem _animationPlayer = default!; // Sunrise-Edit

        private readonly float[] _sign = { -1, 1 };
        private readonly string _jitterAnimationKey = "jittering";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<JitteringComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<JitteringComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnStartup(EntityUid uid, JitteringComponent jittering, ComponentStartup args)
        {
            if (!TryComp(uid, out SpriteComponent? sprite))
                return;

            // Sunrise edit start - перенос анимации в новую систему
            jittering.StartOffset = _animationPlayer.GetBaseOffset((uid, sprite));
            _animationPlayer.PlayLoop(uid, _jitterAnimationKey, PlayAnimation);
            // Sunrise edit end
        }

        private void OnShutdown(EntityUid uid, JitteringComponent jittering, ComponentShutdown args)
        {
            // Sunrise edit start - перенос анимации в новую систему
            _animationPlayer.Stop(uid, _jitterAnimationKey);
            // Sunrise edit end
        }

        // Sunrise edit start - перенос анимации в новую систему
        private void PlayAnimation(EntityUid uid)
        {
            if (TryComp(uid, out JitteringComponent? jittering) && TryComp(uid, out SpriteComponent? sprite))
                _animationPlayer.PlayOffset(uid, GetAnimation(jittering, sprite), _jitterAnimationKey,
                    jittering.StartOffset, SpriteAnimationEndMode.Hold);
        }
        // Sunrise edit end

        private Animation GetAnimation(JitteringComponent jittering, SpriteComponent sprite)
        {
            var amplitude = MathF.Min(4f, jittering.Amplitude / 100f + 1f) / 10f;
            var offset = new Vector2(_random.NextFloat(amplitude / 4f, amplitude),
                _random.NextFloat(amplitude / 4f, amplitude / 3f));

            offset.X *= _random.Pick(_sign);
            offset.Y *= _random.Pick(_sign);

            if (Math.Sign(offset.X) == Math.Sign(jittering.LastJitter.X)
                || Math.Sign(offset.Y) == Math.Sign(jittering.LastJitter.Y))
            {
                // If the sign is the same as last time on both axis we flip one randomly
                // to avoid jitter staying in one quadrant too much.
                if (_random.Prob(0.5f))
                    offset.X *= -1;
                else
                    offset.Y *= -1;
            }

            var length = 0f;
            // avoid dividing by 0 so animations don't try to be infinitely long
            if (jittering.Frequency > 0)
                length = 1f / jittering.Frequency;

            jittering.LastJitter = offset;

            return new Animation()
            {
                Length = TimeSpan.FromSeconds(length),
                AnimationTracks =
                {
                    new AnimationTrackComponentProperty()
                    {
                        ComponentType = typeof(SpriteComponent),
                        Property = nameof(SpriteComponent.Offset),
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(sprite.Offset, 0f),
                            new AnimationTrackProperty.KeyFrame(jittering.StartOffset + offset, length),
                        }
                    }
                }
            };
        }
    }
}
