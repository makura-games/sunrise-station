using Content.Shared._RMC14.Explosion.Components;
using Content.Shared._Sunrise.Helpers;
using Content.Shared.Explosion.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Shared._RMC14.Explosion;

public abstract partial class SharedRMCExplosionSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private const float MinSmokeCountPer100 = 12f;
    private const float MaxSmokeCountPer100 = 17f;

    private const float SmokeSpawnRadiusPer100 = 2f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMExplosionEffectComponent, CMExplosiveTriggeredEvent>(OnExplosionEffectTriggered);
    }

    private void OnExplosionEffectTriggered(Entity<CMExplosionEffectComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        DoEffect(ent);
    }

    // Sunrise edit start
    public void DoEffect(Entity<CMExplosionEffectComponent> ent)
    {
        // Cleanup may fire trigger paths while parents are already terminating.
        // Spawning effects on such coordinates logs transform errors and breaks tests.
        if (TerminatingOrDeleted(ent))
            return;

        if (!TryComp<ExplosiveComponent>(ent, out var explosionComponent))
            return;

        if (ent.Comp.ShockWave is { } shockwave)
        {
            var wave = SpawnNextToOrDrop(shockwave, ent);
            ModifyShockwave(wave, explosionComponent);
        }

        if (ent.Comp.Explosion is { } explosion)
        {
            var explosionEntity = SpawnNextToOrDrop(explosion, ent);
            CreateFancyExplosionEffect(explosionEntity, explosionComponent);
        }

        if (ent.Comp.Smoke is { } smoke)
            CreateFancySmoke(ent, explosionComponent, smoke);
    }

    private void ModifyShockwave(EntityUid wave, ExplosiveComponent explosionComponent)
    {
        // Дальше идут просто числа, которые я придумал особо не думая, мб нужно подумать
        // Но идея в том, чтобы чем сильнее взрыв, тем сильнее эффект и наоборот
        // TODO: Реализовать радиус действия волны и убрать стандартные значения в компоненте

        if (TryComp<RMCExplosionShockWaveComponent>(wave, out var waveComponent))
        {
            waveComponent.FalloffPower ??= explosionComponent.TotalIntensity / 4f;
            waveComponent.Width ??= Math.Clamp(explosionComponent.TotalIntensity / 200f, 0.1f, 0.5f);

            Dirty(wave, waveComponent);
        }

        if (TryComp<TimedDespawnComponent>(wave, out var timedDespawnComponent))
            timedDespawnComponent.Lifetime = Math.Clamp(explosionComponent.TotalIntensity / 50f, 0.1f, 0.8f);
    }

    private void CreateFancyExplosionEffect(EntityUid explosionEntity, ExplosiveComponent explosionComponent)
    {
        if (!TryComp<TimedDespawnComponent>(explosionEntity, out var timedDespawnComponent))
            return;

        if (!TryComp<ExplosionEffectComponent>(explosionEntity, out var explosionEffectComponent))
            return;

        var sizeModifier = Math.Clamp(explosionComponent.TotalIntensity / 50f, 1f, 12f);
        explosionEffectComponent.SizeModifier = sizeModifier;
        explosionEffectComponent.LifeTime = timedDespawnComponent.Lifetime;

        Dirty(explosionEntity, explosionEffectComponent);
    }

    private void CreateFancySmoke(Entity<CMExplosionEffectComponent> ent, ExplosiveComponent explosionComponent, EntProtoId smokeId)
    {
        var modifier = explosionComponent.TotalIntensity / 100f;

        var smokeCount = _random.NextFloat(MinSmokeCountPer100 * modifier, MaxSmokeCountPer100 * modifier);
        var coords = Transform(ent).Coordinates;
        var modifiedRadius = Math.Clamp(SmokeSpawnRadiusPer100 * modifier, 2f, 10f);

        for (var i = 0; i < smokeCount; i++)
        {
            var smoke = Spawn(smokeId, coords.GetRandomInRadius(modifiedRadius));

            if (!TryComp<TimedDespawnComponent>(smoke, out var timedDespawnComponent))
                continue;

            if (!TryComp<ExplosionSmokeEffectComponent>(smoke, out var explosionSmokeEffectComponent))
                continue;

            timedDespawnComponent.Lifetime = ExplosionSmokeEffectComponent.AnimationDuration + _random.NextFloat(-ExplosionSmokeEffectComponent.Variation, ExplosionSmokeEffectComponent.Variation);

            // Это нужно, чтобы анимация на клиенте знала, когда мы решили заканчиваться
            explosionSmokeEffectComponent.LifeTime = timedDespawnComponent.Lifetime;
            Dirty(smoke, explosionSmokeEffectComponent);
        }
    }
    // Sunrise edit end

    public void TryDoEffect(Entity<CMExplosionEffectComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        DoEffect((ent, ent.Comp));
    }
}

[ByRefEvent]
public readonly record struct CMExplosiveTriggeredEvent;

