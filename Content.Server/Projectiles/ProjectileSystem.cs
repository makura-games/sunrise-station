using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared._Sunrise.Weapons.Components;
using Content.Shared._Sunrise.Weapons.Events;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Robust.Shared.Maths;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        var target = args.OtherEntity;
        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        // Sunrise edit start - Projectile ricochet integration
        if (TryComp<ProjectileRicochetComponent>(uid, out var ricochet) && ricochet.Chance > 0)
        {
            if (TryComp<PhysicsComponent>(uid, out var physics) && !physics.LinearVelocity.IsLengthZero())
            {
                var projXform = Transform(uid);
                var worldPos = _transformSystem.GetWorldPosition(projXform);
                // Sunrise edit start - Use original flight direction derived from rotation to bypass any temporary physics speed/direction anomalies
                var direction = (projXform.WorldRotation - component.Angle).ToWorldVec();
                // Sunrise edit end

                var contactPoint = args.PointCount > 0 ? args.WorldPoints[0] : worldPos;
                var raycastStart = contactPoint - direction * 0.5f;

                var ricochetEv = new HitScanRicochetAttemptEvent(ricochet.Chance, raycastStart, direction, false, args.WorldNormal);
                RaiseLocalEvent(target, ref ricochetEv);

                if (ricochetEv.Ricocheted)
                {
                    var speed = physics.LinearVelocity.Length();
                    var newVelocity = ricochetEv.Dir * speed;
                    _physics.SetLinearVelocity(uid, newVelocity, body: physics);
                    _transformSystem.SetWorldRotation(projXform, ricochetEv.Dir.ToWorldAngle() + component.Angle);

                    // Move projectile slightly outside the wall to prevent stuck physics
                    var newPosition = contactPoint + ricochetEv.Dir * 0.15f;
                    _transformSystem.SetWorldPosition(projXform, newPosition);
                    return; // bounce off, cancel damage application!
                }
            }
        }
        // Sunrise edit end

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= _damageableSystem.GetTotalDamage((target, damageableComponent));
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        var deleted = Deleted(target);

        DamageSpecifier damage;
        // Sunrise edit start - Starlight armor penetration integration
        var damageChange = _damageableSystem.ChangeDamage(
            (target, damageableComponent),
            ev.Damage,
            component.IgnoreResistances,
            origin: component.Shooter,
            armorPenetration: component.ArmorPenetration,
            canHeal: false);

        damage = damageChange;

        // Sunrise added start - сообщаем эффектам только о подтверждённом уроне снаряда
        if (!damageChange.Empty)
        {
            var contactPoint = args.PointCount > 0 ? args.WorldPoints[0] : _transformSystem.GetWorldPosition(uid);
            var direction = (Transform(uid).WorldRotation - component.Angle).ToWorldVec();
            var damageDealtEvent = new ProjectileDamageDealtEvent(target, damageChange, contactPoint, direction);
            RaiseLocalEvent(uid, ref damageDealtEvent);
        }
        // Sunrise added end

        if (!damageChange.Empty && Exists(component.Shooter))
        {
            // Guard against race conditions where collided entities are already losing transform
            // during this physics tick.
            if (!deleted)
            {
                if (TryComp(target, out TransformComponent? targetXform))
                    _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {damage:damage} damage");
        }

        var projectileSpent = !TryPenetrate((uid, component), damage, damageRequired);

        // Sunrise edit start - Projectile pierce integration
        if (projectileSpent && TryComp<ProjectilePierceComponent>(uid, out var pierce) && pierce.Chance > 0)
        {
            if (HasComp<MobStateComponent>(target) || HasComp<PierceableComponent>(target))
            {
                if (_rand.Prob(pierce.Chance))
                {
                    var pierceEv = new HitScanPierceAttemptEvent(pierce.PierceLevel, true);
                    RaiseLocalEvent(target, ref pierceEv);

                    if (pierceEv.Pierced)
                    {
                        projectileSpent = false;
                        pierce.PiercedEntities.Add(target);
                        Dirty(uid, pierce);

                        // Give it a little bit of swim/deviation
                        if (TryComp<PhysicsComponent>(uid, out var physics))
                        {
                            var random = pierce.Deviation > 0 ? _rand.NextFloat(-pierce.Deviation, pierce.Deviation) : 0f;
                            var projXform = Transform(uid);
                            // Sunrise edit start - Use original flight angle derived from rotation to bypass any post-collision physics state changes
                            var velocityAngle = projXform.WorldRotation - component.Angle;
                            // Sunrise edit end
                            var newDir = (velocityAngle + random).ToWorldVec();
                            _physics.SetLinearVelocity(uid, newDir * physics.LinearVelocity.Length(), body: physics);
                            _transformSystem.SetWorldRotation(uid, newDir.ToWorldAngle() + component.Angle);

                        }
                    }
                }
            }
        }
        component.ProjectileSpent = projectileSpent;
        // Sunrise edit end

        // Sunrise-Start
        if (!deleted && HasComp<TransformComponent>(target))
        {
            _guns.PlayImpactSound(target, damage, component.SoundHit, component.ForceSound);

            if (!args.OurBody.LinearVelocity.IsLengthZero())
                _sharedCameraRecoil.KickCamera(target, args.OurBody.LinearVelocity.Normalized());
        }
        // Sunrise-End

        if (component.DeleteOnCollide && component.ProjectileSpent)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, DamageSpecifier damage, FixedPoint2 damageRequired)
    {
        // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
        if (projectile.Comp.PenetrationThreshold == 0)
            return false;

        // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
        if (projectile.Comp.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.Comp.PenetrationDamageTypeRequirement)
            {
                if (damage.DamageDict.Keys.Contains(requiredDamageType))
                    continue;

                return false;
            }
        }

        // If the object won't be destroyed, it "tanks" the penetration hit.
        if (damage.GetTotal() < damageRequired)
        {
            return false;
        }

        if (!projectile.Comp.ProjectileSpent)
        {
            projectile.Comp.PenetrationAmount += damageRequired;
            // The projectile has dealt enough damage to be spent.
            if (projectile.Comp.PenetrationAmount >= projectile.Comp.PenetrationThreshold)
            {
                return false;
            }
        }

        return true;
    }
}
