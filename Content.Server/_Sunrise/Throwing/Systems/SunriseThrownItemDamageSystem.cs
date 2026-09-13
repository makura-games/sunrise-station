using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._Sunrise.Throwing.Components;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Clothing.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Throwing.Systems;

/// <summary>
///     Sunrise-Edit: handles throwing damage and effects based on item size.
/// </summary>
public sealed partial class SunriseThrownItemDamageSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SunriseThrownItemDamageComponent, ThrowDoHitEvent>(OnDoHit);
        SubscribeLocalEvent<SunriseThrownItemDamageComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<SunriseThrownItemDamageComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnThrown(EntityUid uid, SunriseThrownItemDamageComponent component, ThrownEvent args)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        component.OriginalLinearDamping ??= physics.LinearDamping;

        float weight;
        if (TryComp<ItemComponent>(uid, out var item) && _proto.TryIndex(item.Size, out var sizeProto))
        {
            weight = sizeProto.Weight;
        }
        else
        {
            // Fallback for non-items (e.g. structures) based on mass
            weight = MathF.Max(1f, physics.Mass / 2f);
        }

        if (weight > 4)
        {
            // Increase damping significantly for heavy items
            var damping = component.OriginalLinearDamping.Value + (weight * 0.1f);
            _physics.SetLinearDamping(uid, physics, damping);
        }
    }

    private void OnStopThrow(EntityUid uid, SunriseThrownItemDamageComponent component, ref StopThrowEvent args)
    {
        if (component.OriginalLinearDamping != null && TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetLinearDamping(uid, physics, component.OriginalLinearDamping.Value);
            component.OriginalLinearDamping = null;
        }
    }

    private void OnDoHit(EntityUid uid, SunriseThrownItemDamageComponent component, ThrowDoHitEvent args)
    {
        if (TerminatingOrDeleted(args.Target))
            return;

        if (args.Target == args.Component.Thrower)
            return;

        // Try to get item/physics info
        TryComp<PhysicsComponent>(uid, out var physics);
        ItemSizePrototype? sizeProto = null;
        var hasItem = TryComp<ItemComponent>(uid, out var item) && _proto.TryIndex(item.Size, out sizeProto);

        float multiplier;
        float weight;
        bool forceKnockdown = false;

        if (hasItem && sizeProto != null)
        {
            multiplier = sizeProto.ThrowDamage * component.WeightMultiplier;
            weight = sizeProto.Weight;
        }
        else if (physics != null)
        {
            // Non-item with physics (e.g. structure)
            // Using mass as a base for multiplier
            multiplier = (physics.Mass / 2.0f) * component.WeightMultiplier;
            weight = MathF.Max(1f, physics.Mass / 2f);
            forceKnockdown = true; // Always stun for non-items (structures)
        }
        else
        {
            return;
        }

        // Get velocity early for bounce and recoil
        var velocity = physics?.LinearVelocity ?? Vector2.Zero;

        var isStructure = TryComp<InjurableComponent>(args.Target, out var injurable) &&
            injurable.DamageContainer?.Id.Contains("Structural") == true;

        if (HasComp<MobStateComponent>(args.Target) || isStructure)
        {
            var canImpact = weight >= 4;

            if (physics != null && canImpact)
            {
                if (velocity.LengthSquared() > 0.1f)
                {
                    // Calculate bounce speed based on impact velocity
                    // Energy loss: 40% (0.6 multiplier) + weight-based loss
                    var energyLoss = 0.6f;
                    if (weight > 4)
                        energyLoss -= (weight - 4) * 0.03f;

                    energyLoss = MathF.Max(energyLoss, 0.1f);
                    var speed = velocity.Length() * energyLoss;

                    // Stop bouncing if speed is too low (prevents infinite short-range bouncing)
                    if (speed < 2.0f)
                    {
                        _thrown.StopThrow(uid, args.Component);
                    }
                    else
                    {
                        _thrown.StopThrow(uid, args.Component);

                        // Determine bounce direction using reflection
                        var currentPos = _transform.GetWorldPosition(uid);
                        var targetPos = _transform.GetWorldPosition(args.Target);
                        var diff = currentPos - targetPos;
                        var normal = diff.LengthSquared() > 0.0001f
                            ? diff.Normalized()
                            : -velocity.Normalized();

                        // Reflection formula: R = V - 2(V.N)N
                        // Only reflect if moving towards the target
                        var dot = Vector2.Dot(velocity, normal);
                        var reflection = dot < 0 ? velocity - 2 * dot * normal : velocity;

                        // Mix reflection with normal (push away) for robustness
                        var bounceDir = reflection.LengthSquared() > 0.001f
                            ? (reflection.Normalized() + normal * 0.4f).Normalized()
                            : normal;

                        // Immediately move out of the target's collision bounds to prevent getting stuck
                        _transform.SetWorldPosition(uid, currentPos + bounceDir * 0.25f);

                        // Reset velocity to zero before applying new throw
                        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

                        // Re-throw with the new direction and speed
                        _throwing.TryThrow(uid, bounceDir * MathF.Max(1f, speed * 0.5f), speed, user: args.Target);
                    }
                }
                else
                {
                    _thrown.StopThrow(uid, args.Component);
                }
            }
            else if (HasComp<MobStateComponent>(args.Target))
            {
                _thrown.StopThrow(uid, args.Component);
            }
        }

        if (forceKnockdown || weight >= component.KnockdownWeightThreshold)
        {
            _stun.TryKnockdown(args.Target, component.KnockdownDuration, drop: false, refresh: false);
        }

        if (multiplier <= 0)
            return;

        if (isStructure && weight < component.StructureDamageWeightThreshold)
            return;

        var damageDistribution = GetDamageDistribution(uid, component);
        if (damageDistribution.Empty || damageDistribution.GetTotal() <= 0)
            return;

        var damage = damageDistribution * multiplier * _damageable.UniversalThrownDamageModifier;
        var dmg = _damageable.ChangeDamage(args.Target, damage, component.IgnoreResistances, origin: args.Component.Thrower);

        if (dmg.GetTotal() > 0)
        {
            _adminLogger.Add(LogType.ThrowHit, $"{ToPrettyString(args.Target):target} received {dmg.GetTotal():damage} damage from collision with {ToPrettyString(uid):thrown}");
            _color.RaiseEffect(Color.Red, new List<EntityUid> { args.Target }, Filter.Pvs(args.Target));

            if (velocity.LengthSquared() > 0.001f)
            {
                _sharedCameraRecoil.KickCamera(args.Target, velocity.Normalized() * 0.5f);
            }
        }
    }

    private DamageSpecifier GetDamageDistribution(EntityUid uid, SunriseThrownItemDamageComponent component)
    {
        // 1. If component has explicit damage types, use them (normalized)
        if (!component.DamageTypes.Empty)
        {
            var total = component.DamageTypes.GetTotal();
            if (total > 0)
                return component.DamageTypes * (1.0f / (float) total);

            return component.DamageTypes;
        }

        // 2. Check MeleeWeaponComponent for damage types
        if (TryComp<MeleeWeaponComponent>(uid, out var melee) && !melee.Damage.Empty)
        {
            var total = melee.Damage.GetTotal();
            if (total > 0)
                return melee.Damage * (1.0f / (float) total);
        }

        // 3. Fallback: No damage distribution
        return new DamageSpecifier();
    }
}
