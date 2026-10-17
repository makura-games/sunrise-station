using Content.Shared.Damage.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

#pragma warning disable IDE0130 // Пространство имён общей системы сохраняется для partial-класса.
namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    private void ApplyStarlightHitscanCompatibility(Entity<ProjectileComponent> entity)
    {
        if (TryComp<PhysicsComponent>(entity, out var physics))
            _physics.SetFixedRotation(entity, true, body: physics);

        if (TryComp<HitscanBasicDamageComponent>(entity, out var hitscanDamage))
        {
            entity.Comp.Damage = hitscanDamage.Damage;
            entity.Comp.ArmorPenetration = hitscanDamage.ArmorPenetration;
            entity.Comp.IgnoreResistances = hitscanDamage.IgnoreResistances;
        }

        if (!TryComp<HitscanStaminaDamageComponent>(entity, out var hitscanStamina))
            return;

        var stamina = EnsureComp<StaminaDamageOnCollideComponent>(entity);
        stamina.Damage = hitscanStamina.StaminaDamage;
    }
}
