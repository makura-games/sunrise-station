using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Random;

namespace Content.Client._RMC14._Sunrise.Explosion;

// Омг это же партикл систем за 1$
public sealed partial class RMCExplosionSystem : SharedRMCExplosionSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _player = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string SmokeTrack = "smoke-animation";
    private const string ExplosionTrack = "explosion-animation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionSmokeEffectComponent, ComponentStartup>(OnSmokeStartup);
        SubscribeLocalEvent<ExplosionEffectComponent, ComponentStartup>(OnExplosionStartup);
    }

    private void OnSmokeStartup(Entity<ExplosionSmokeEffectComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var targetX = 2f + _random.NextFloat(-ExplosionSmokeEffectComponent.Variation, ExplosionSmokeEffectComponent.Variation);
        var targetY = 2f + _random.NextFloat(-ExplosionSmokeEffectComponent.Variation, ExplosionSmokeEffectComponent.Variation);

        var animation = new Animation()
        {
            Length = TimeSpan.FromSeconds(ent.Comp.LifeTime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    Property = nameof(SpriteComponent.Offset),
                    ComponentType = typeof(SpriteComponent),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Offset, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(targetX, targetY), ent.Comp.LifeTime),
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    Property = nameof(SpriteComponent.Color),
                    ComponentType = typeof(SpriteComponent),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color, 0f),
                        new AnimationTrackProperty.KeyFrame(GetTransparentColor(sprite.Color), ent.Comp.LifeTime),
                    },
                },
            },
        };

        _player.Play(ent, animation, SmokeTrack);
    }

    private void OnExplosionStartup(Entity<ExplosionEffectComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.Scale = new Vector2(ent.Comp.SizeModifier);
        _sprite.SetAutoAnimateSync(sprite, ent.Comp.LifeTime);
    }

    private static Color GetTransparentColor(Color color)
    {
        return new Color(color.R, color.G, color.B, 0f);
    }
}
