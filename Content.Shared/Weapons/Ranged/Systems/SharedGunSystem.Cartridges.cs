using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;

    // needed for server system
    protected virtual void InitializeCartridge()
    {
        SubscribeLocalEvent<CartridgeAmmoComponent, ExaminedEvent>(OnCartridgeExamine);
        SubscribeLocalEvent<CartridgeAmmoComponent, DamageExamineEvent>(OnCartridgeDamageExamine);
    }

    private void OnCartridgeExamine(Entity<CartridgeAmmoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Spent
            ? Loc.GetString("gun-cartridge-spent")
            : Loc.GetString("gun-cartridge-unspent"));
    }

    private void OnCartridgeDamageExamine(Entity<CartridgeAmmoComponent> ent, ref DamageExamineEvent args)
    {
        var damageSpec = GetProjectileDamage(ent.Comp.Prototype);

        if (damageSpec != null)
            _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec), Loc.GetString("damage-projectile"));

    // Sunrise-Start
        var ap = GetProjectileArmorPenetration(ent.Comp.Prototype);
        if (ap != null && MathF.Abs(ap.Value) > 0.001f)
        {
            var percent = (int)MathF.Round(ap.Value * 100f);
            args.Message.PushNewline();
            if (ap.Value >= 0)
                args.Message.AddMarkupOrThrow(Loc.GetString("gun-cartridge-armor-penetration", ("percent", percent)));
            else
                args.Message.AddMarkupOrThrow(Loc.GetString("gun-cartridge-armor-penetration-negative", ("percent", percent)));
        }
    }

    /// <summary>
    /// Returns armor penetration for a projectile or hitscan prototype.
    /// </summary>
    public float? GetProjectileArmorPenetration(EntProtoId proto)
    {
        if (!ProtoManager.TryIndex(proto, out var entityProto))
            return null;

        if (entityProto.TryGetComponent<ProjectileComponent>(out var projectile, Factory))
        {
            if (projectile.IgnoreResistances)
                return 1.0f;

            if (MathF.Abs(projectile.ArmorPenetration) > 0.001f)
                return projectile.ArmorPenetration;
        }

        if (entityProto.TryGetComponent<Hitscan.Components.HitscanBasicDamageComponent>(out var hitscan, Factory))
        {
            if (hitscan.IgnoreResistances)
                return 1.0f;

            if (MathF.Abs(hitscan.ArmorPenetration) > 0.001f)
                return hitscan.ArmorPenetration;
        }

        return null;
    }
    // Sunrise-End

    private DamageSpecifier? GetProjectileDamage(EntProtoId proto)
    {
        if (!ProtoManager.TryIndex(proto, out var entityProto))
            return null;

        if (!entityProto.TryGetComponent<ProjectileComponent>(out var projectile, Factory))
            return null;

        if (!projectile.Damage.Empty)
            return projectile.Damage * Damageable.UniversalProjectileDamageModifier;

        return null;
    }
}
