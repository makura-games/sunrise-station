using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Starlight.Weapon;

public sealed partial class ProjectileHitscanCompatibilitySystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Sunrise-Edit: subscribe to MapInitEvent instead of ComponentInit to avoid breaking uninitialized save tests
        SubscribeLocalEvent<ProjectileComponent, MapInitEvent>(OnProjectileMapInit);
    }

    private void OnProjectileMapInit(EntityUid uid, ProjectileComponent component, ref MapInitEvent args)
    {
        // Sunrise-Edit: Force fixed rotation on all projectiles during map initialization to avoid modifying physics on spawn in save tests
        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetFixedRotation(uid, true, body: physics);
        }

        // Copy damage and armor penetration from HitscanBasicDamageComponent if present
        if (TryComp<HitscanBasicDamageComponent>(uid, out var hitscanDamage))
        {
            component.Damage = hitscanDamage.Damage;
            component.ArmorPenetration = hitscanDamage.ArmorPenetration;
            component.IgnoreResistances = hitscanDamage.IgnoreResistances;
        }

        // Setup stamina damage from HitscanStaminaDamageComponent if present
        if (TryComp<HitscanStaminaDamageComponent>(uid, out var hitscanStamina))
        {
            var stamina = EnsureComp<StaminaDamageOnCollideComponent>(uid);
            stamina.Damage = hitscanStamina.StaminaDamage;
        }
    }
}
