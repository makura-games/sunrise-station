using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Execution;

public abstract partial class SharedExecutionSystem
{
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;

    protected static bool TryGetVerbContext(
        ref GetVerbsEvent<UtilityVerb> args,
        out EntityUid attacker,
        out EntityUid weapon,
        out EntityUid victim,
        out bool suicide)
    {
        attacker = default;
        weapon = default;
        victim = default;
        suicide = false;

        if (args.Hands == null || args.Using == null || !args.CanAccess || !args.CanInteract)
            return false;

        attacker = args.User;
        weapon = args.Using.Value;
        victim = args.Target;
        suicide = attacker == victim;
        return true;
    }

    protected bool CanExecuteWithAny(EntityUid victim, EntityUid attacker)
    {
        if (!HasComp<DamageableComponent>(victim))
            return false;

        if (!TryComp<MobStateComponent>(victim, out var mobState))
            return false;

        if (HasComp<BorgChassisComponent>(victim))
            return false;

        if (_mobStateSystem.IsDead(victim, mobState))
            return false;

        if (!_actionBlockerSystem.CanAttack(attacker, victim))
            return false;

        if (victim != attacker && _actionBlockerSystem.CanInteract(victim, null))
            return false;

        return true;
    }

    protected bool CanExecuteWithMelee(EntityUid weapon, EntityUid victim, EntityUid user)
    {
        if (!CanExecuteWithAny(victim, user))
            return false;

        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee) || melee.Damage.GetTotal() <= 0.0f)
            return false;

        return true;
    }

    protected bool CanExecuteWithGun(EntityUid weapon, EntityUid victim, EntityUid user)
    {
        if (!CanExecuteWithAny(victim, user))
            return false;

        if (!TryComp<GunComponent>(weapon, out var gun) || !_gunSystem.CanShoot(gun))
            return false;

        if (HasComp<DamageableComponent>(victim))
        {
            if (TryComp<BatteryAmmoProviderComponent>(weapon, out var battery) &&
                !PrototypeHasLethalEffect(battery.Prototype))
                return false;

            if (HasNonLethalAmmoPrototype(weapon))
                return false;
        }

        return true;
    }

    private bool HasNonLethalAmmoPrototype(EntityUid weapon)
    {
        if (TryComp<BallisticAmmoProviderComponent>(weapon, out var ballistic) &&
            ballistic.Proto != null &&
            IsNonLethalAmmo(ballistic.Proto.Value))
            return true;

        if (TryComp<RevolverAmmoProviderComponent>(weapon, out var revolver) &&
            revolver.FillPrototype != null &&
            IsNonLethalAmmo(revolver.FillPrototype))
            return true;

        if (TryComp<BasicEntityAmmoProviderComponent>(weapon, out var basic) &&
            IsNonLethalAmmo(basic.Proto))
            return true;

        if (_containerSystem.TryGetContainer(weapon, GunMagazineContainerId, out var container) &&
            container is ContainerSlot slot &&
            slot.ContainedEntity is { } magEntity)
        {
            if (TryComp<BallisticAmmoProviderComponent>(magEntity, out var magBallistic) &&
                magBallistic.Proto != null &&
                IsNonLethalAmmo(magBallistic.Proto.Value))
                return true;

            if (TryComp<BasicEntityAmmoProviderComponent>(magEntity, out var magBasic) &&
                IsNonLethalAmmo(magBasic.Proto))
                return true;
        }

        return false;
    }

    private bool PrototypeHasLethalEffect(EntProtoId ammoPrototype)
    {
        var proto = _prototypeManager.Index(ammoPrototype);

        if (proto.TryGetComponent<ExplosiveComponent>(out _, _componentFactory))
            return true;

        DamageSpecifier? damage = null;

        if (proto.TryGetComponent<ProjectileComponent>(out var projectile, _componentFactory) && projectile != null && !projectile.Damage.Empty)
            damage = projectile.Damage;
        else if (proto.TryGetComponent<HitscanBasicDamageComponent>(out var hitscan, _componentFactory) && hitscan != null && !hitscan.Damage.Empty)
            damage = hitscan.Damage;

        if (damage == null)
            return false;

        foreach (var (type, value) in damage.DamageDict)
        {
            if (type != StructuralDamageType && value > FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    protected static bool IsNonLethalAmmo(string? prototypeId)
    {
        if (string.IsNullOrWhiteSpace(prototypeId))
            return false;

        foreach (var token in NonLethalAmmoIdTokens)
        {
            if (prototypeId.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
