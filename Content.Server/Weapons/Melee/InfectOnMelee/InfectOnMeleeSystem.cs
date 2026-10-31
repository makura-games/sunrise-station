using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Cluwne;
using Content.Shared.Clumsy.Components; // Sunrise-Edit - новые статус-эффекты неуклюжести
using Content.Shared.Interaction.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Zombies;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew; // Sunrise-Edit - новые статус-эффекты неуклюжести

namespace Content.Server.Weapons.Melee.InfectOnMelee;

public sealed partial class InfectOnMeleeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    // Sunrise-Edit - зависимость для новых статус-эффектов неуклюжести
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfectOnMeleeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, InfectOnMeleeComponent component, MeleeHitEvent args)
    {
        if (component.Cluwinification == true)
        {
            foreach (var entity in args.HitEntities)
            {
                if (HasComp<HumanoidProfileComponent>(entity)
                    && !_mob.IsDead(entity)
                    && _random.Prob(GenerateHitChance(entity, component))
                    // Sunrise edit start - адаптация под новые статус-эффекты неуклюжести
                    && !_statusEffects.HasEffectComp<ClumsyGunStatusEffectComponent>(entity)
                    // Sunrise edit end
                    && !HasComp<ZombieComponent>(entity)
                    && !HasComp<MindShieldComponent>(entity))
                {
                    _audio.PlayPvs(component.InfectionSound, uid);
                    EnsureComp<CluwneComponent>(entity);
                }
            }
        }
    }

    private float GenerateHitChance(EntityUid enemy, InfectOnMeleeComponent component)
    {
        float chance = component.InfectionChance;
        if (TryComp<DamageableComponent>(enemy, out var damage))
        {
            var totalDamage = _damageable.GetTotalDamage((enemy, damage));

            var additionalChance = totalDamage * 0.01f;

            var finalChance = Math.Clamp(chance + additionalChance.Float(), 0f, 1f);

            chance = finalChance;
        }

        return chance;
    }
}
