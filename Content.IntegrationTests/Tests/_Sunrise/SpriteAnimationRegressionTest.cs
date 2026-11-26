using System.Numerics;
using Content.Client._Sunrise.Animations;
using Content.Client._Sunrise.Animations.SpriteAnimation;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Chasm;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared.Anomaly.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Sunrise;

[TestFixture, NonParallelizable]
public sealed class SpriteAnimationRegressionTest
{
    [Test]
    public async Task SpriteRemovalClearsTracksAndAllowsFreshAnimation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = client.System<SpriteAnimationSystem>();
                animation.PlayRotation(uid, "spin", true, (Angle.Zero, 0f), (Angle.FromDegrees(720), 1f));
                animation.Seek(uid, "spin", 0.75f);
                animation.FrameUpdate(0f);
                Assert.That(sprite.Rotation.Degrees, Is.EqualTo(540).Within(0.01));
                Assert.That(entities.HasComponent<SpriteAnimationComponent>(uid), Is.True);
                entities.RemoveComponent<SpriteComponent>(uid);
                Assert.That(entities.HasComponent<SpriteAnimationComponent>(uid), Is.False);
                sprite = entities.AddComponent<SpriteComponent>(uid);
                animation.SetOffsetContribution((uid, sprite), "fresh", Vector2.UnitX);
                animation.Stop(uid, "fresh");
                animation.FrameUpdate(0f);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero));
                Assert.That(sprite.Rotation, Is.EqualTo(Angle.Zero));
                Assert.That(entities.HasComponent<SpriteAnimationComponent>(uid), Is.False);
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdapterKeepsLengthBeyondKeyframes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = new Animation
                {
                    Length = TimeSpan.FromSeconds(2),
                    AnimationTracks =
                    {
                        new AnimationTrackComponentProperty
                        {
                            ComponentType = typeof(SpriteComponent),
                            Property = nameof(SpriteComponent.Offset),
                            KeyFrames =
                            {
                                new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.4f),
                                new AnimationTrackProperty.KeyFrame(Vector2.One, 1f),
                            },
                        },
                    },
                };
                var controller = client.System<SpriteAnimationSystem>();
                controller.PlayOffset(uid, animation, "length", Vector2.Zero);
                controller.FrameUpdate(1.5f);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.One));
                Assert.That(controller.IsPlaying(uid, "length"), Is.True);
                controller.FrameUpdate(0.5f);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero));
                Assert.That(controller.IsPlaying(uid, "length"), Is.False);
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnomalyFloatsAndStopsWhenRemoved()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
#pragma warning disable RA0002
                var anomaly = new AnomalyComponent { FloatingOffset = Vector2.UnitY };
#pragma warning restore RA0002
                entities.AddComponent(uid, anomaly);
                var controller = client.System<SpriteAnimationSystem>();
                Assert.That(controller.IsPlaying(uid, anomaly.AnimationKey), Is.True);
                controller.FrameUpdate(anomaly.AnimationTime / 2f);
                Assert.That(sprite.Offset, Is.Not.EqualTo(Vector2.Zero));
                var previousDuration = anomaly.AnimationTime;
#pragma warning disable RA0002
                anomaly.FloatingOffset = Vector2.UnitX * 3f;
                anomaly.AnimationTime = previousDuration / 2f;
#pragma warning restore RA0002
                controller.FrameUpdate(previousDuration * 1.5f);
                controller.FrameUpdate(anomaly.AnimationTime / 2f);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.UnitX * 1.5f));
                entities.RemoveComponent<AnomalyComponent>(uid);
                Assert.That(controller.IsLooping(uid, anomaly.AnimationKey), Is.False);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TemporaryPosePreservesRotationWithoutExistingPose(bool suspend)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                client.System<SpriteSystem>().SetRotation((uid, sprite), Angle.FromDegrees(37));
                var pose = client.System<SpritePoseSystem>();
                if (suspend)
                {
                    pose.Suspend((uid, sprite), Direction.South);
                    pose.Resume((uid, sprite));
                }
                else
                {
                    pose.SetOverride((uid, sprite), Angle.FromDegrees(90), 0f);
                    Assert.That(sprite.Rotation.Degrees, Is.EqualTo(90).Within(0.01));
                    pose.ClearOverride((uid, sprite), 0f);
                }
                Assert.That(sprite.Rotation.Degrees, Is.EqualTo(37).Within(0.01));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(AnimationInterpolationMode.Linear, 1.25f)]
    [TestCase(AnimationInterpolationMode.Cubic, 1.203125f)]
    [TestCase(AnimationInterpolationMode.Nearest, 1f)]
    [TestCase(AnimationInterpolationMode.Previous, 1f)]
    public async Task AdapterKeepsEngineInterpolation(AnimationInterpolationMode mode, float expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = new Animation
                {
                    Length = TimeSpan.FromSeconds(1),
                    AnimationTracks =
                    {
                        new AnimationTrackComponentProperty
                        {
                            ComponentType = typeof(SpriteComponent),
                            Property = nameof(SpriteComponent.Scale),
                            InterpolationMode = mode,
                            KeyFrames =
                            {
                                new AnimationTrackProperty.KeyFrame(Vector2.One, 0f),
                                new AnimationTrackProperty.KeyFrame(new Vector2(2f), 1f),
                            },
                        },
                    },
                };
                var controller = client.System<SpriteAnimationSystem>();
                controller.PlayScale(uid, animation, "interpolation");
                controller.FrameUpdate(0.25f);
                Assert.That(sprite.Scale.X, Is.EqualTo(expected).Within(0.001f));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeekAndStopKeepIndependentKeys()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = client.System<SpriteAnimationSystem>();
                animation.SetOffsetContribution((uid, sprite), "constant", Vector2.UnitX);
                animation.PlayOffset(uid, "moving", false, true,
                    (Vector2.Zero, 0f), (Vector2.UnitY, 0f), (Vector2.UnitY * 3, 1f));
                animation.Seek(uid, "moving", 0.75f);
                animation.FrameUpdate(0f);
                Assert.That(sprite.Offset, Is.EqualTo(new Vector2(1f, 2.5f)));
                animation.Seek(uid, "moving", 0.25f);
                animation.FrameUpdate(0f);
                Assert.That(sprite.Offset, Is.EqualTo(new Vector2(1f, 1.5f)));
                animation.FrameUpdate(2f);
                Assert.That(sprite.Offset, Is.EqualTo(new Vector2(1f, 3f)));
                animation.Stop(uid, "moving");
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.UnitX));
                animation.PlayOffset(uid, "release", false, (Vector2.UnitY, 0f), (Vector2.Zero, 0.1f));
                animation.FrameUpdate(0.2f);
                Assert.That(animation.IsPlaying(uid, "release"), Is.False);
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.UnitX));
                animation.Stop(uid, "constant");
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnsupportedLoopStopsWithoutAffectingOtherKeys()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var starts = 0;
        var failureLevel = pair.ClientLogHandler.FailureLevel;
        pair.ClientLogHandler.FailureLevel = Robust.Shared.Log.LogLevel.Fatal;
        try
        {
            await client.WaitAssertion(() =>
            {
                var entities = client.EntMan;
                var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                try
                {
                    var sprite = entities.AddComponent<SpriteComponent>(uid);
                    var controller = client.System<SpriteAnimationSystem>();
                    controller.SetOffsetContribution((uid, sprite), "other", Vector2.UnitX);
                    var invalid = new Animation
                    {
                        Length = TimeSpan.FromSeconds(1),
                        AnimationTracks = { new AnimationTrackComponentProperty { Property = "Unsupported" } },
                    };
                    controller.PlayLoop(uid, "invalid", target =>
                    {
                        starts++;
                        controller.PlayOffset(target, invalid, "invalid", Vector2.Zero);
                    });
                    controller.FrameUpdate(1f);
                    controller.FrameUpdate(1f);
                    Assert.That(controller.IsLooping(uid, "invalid"), Is.False);
                    Assert.That(sprite.Offset, Is.EqualTo(Vector2.UnitX));
                    Assert.That(starts, Is.EqualTo(1));
                }
                finally
                {
                    entities.DeleteEntity(uid);
                }
            });
        }
        finally
        {
            pair.ClientLogHandler.FailureLevel = failureLevel;
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScaleConsumersRestoreBaseAfterOtherEffectEnds(bool wingFlight)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var controller = client.System<SpriteAnimationSystem>();
                controller.SetBaseScale((uid, sprite), new Vector2(2));
                controller.PlayScale(uid, "other", true,
                    (new Vector2(0.5f), 0f), (new Vector2(0.5f), 0.1f));
                controller.FrameUpdate(0.2f);
                if (wingFlight)
                    entities.AddComponent<WingFlightComponent>(uid);
                else
                    entities.AddComponent<ChasmFallingComponent>(uid);
                controller.Stop(uid, "other");
                if (wingFlight)
                    entities.RemoveComponent<WingFlightComponent>(uid);
                else
                    entities.RemoveComponent<ChasmFallingComponent>(uid);
                controller.FrameUpdate(0f);
                Assert.That(sprite.Scale, Is.EqualTo(new Vector2(2)));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InfantDoesNotCaptureAnimationScale()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = client.System<SpriteAnimationSystem>();
                animation.SetBaseScale((uid, sprite), new Vector2(2));
                animation.PlayScale(uid, "other", true,
                    (new Vector2(0.5f), 0f), (new Vector2(0.5f), 0.1f));
                animation.FrameUpdate(0.2f);
                entities.AddComponent<InfantComponent>(uid);
                animation.FrameUpdate(0f);
                entities.RemoveComponent<InfantComponent>(uid);
                animation.FrameUpdate(0f);
                Assert.That(sprite.Scale, Is.EqualTo(Vector2.One));
                animation.Stop(uid, "other");
                Assert.That(sprite.Scale, Is.EqualTo(new Vector2(2)));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppingDoesNotRestoreStaleBase()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var carrier = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                entities.AddComponent<CarrierComponent>(carrier);
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = client.System<SpriteAnimationSystem>();
                animation.SetBaseOffset((uid, sprite), Vector2.UnitX);
                var pose = client.System<SpritePoseSystem>();
                pose.SetBasePose((uid, sprite), Angle.FromDegrees(90), 0f);
                var carried = entities.AddComponent<ActiveCanBeCarriedComponent>(uid);
                carried.Carrier = carrier;
                var state = new AfterAutoHandleStateEvent();
                entities.EventBus.RaiseComponentEvent(uid, carried, ref state);
                pose.SetBasePose((uid, sprite), Angle.Zero, 0.5f);
                animation.SetBaseOffset((uid, sprite), new Vector2(4, 0));
                entities.RemoveComponent<ActiveCanBeCarriedComponent>(uid);
                Assert.That(sprite.Offset, Is.EqualTo(new Vector2(4, 0)));
                Assert.That(animation.IsPlaying(uid, SpritePoseSystem.AnimationKey), Is.False);
                Assert.That(sprite.Rotation, Is.EqualTo(Angle.Zero));
            }
            finally
            {
                entities.DeleteEntity(uid);
                entities.DeleteEntity(carrier);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdapterUsesKeyframeEasing()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.AddComponent<SpriteComponent>(uid);
                var animation = new Animation
                {
                    Length = TimeSpan.FromSeconds(1),
                    AnimationTracks =
                    {
                        new AnimationTrackComponentProperty
                        {
                            ComponentType = typeof(SpriteComponent),
                            Property = nameof(SpriteComponent.Offset),
                            KeyFrames =
                            {
                                new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                                new AnimationTrackProperty.KeyFrame(Vector2.One, 1f, t => t * t),
                            },
                        },
                    },
                };
                var controller = client.System<SpriteAnimationSystem>();
                controller.PlayOffset(uid, animation, "eased", Vector2.Zero);
                controller.FrameUpdate(0.5f);
                Assert.That(sprite.Offset, Is.EqualTo(new Vector2(0.25f)));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }
}
