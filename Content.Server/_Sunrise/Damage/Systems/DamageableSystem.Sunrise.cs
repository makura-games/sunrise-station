using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Shared.Damage.Systems;

public static class DamageableSystemExtensions
{
    public static bool TryChangeDamage(
        this DamageableSystem system,
        Entity<DamageableComponent?> ent,
        DamageSpecifier damage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        EntityUid? origin = null,
        bool ignoreGlobalModifiers = false,
        bool ignoreVariance = false)
    {
        return system.TryChangeDamage(ent, damage, out _, ignoreResistances, interruptsDoAfters, origin, ignoreGlobalModifiers, ignoreVariance);
    }

    public static bool TryChangeDamage(
        this DamageableSystem system,
        Entity<DamageableComponent?> ent,
        DamageSpecifier damage,
        out DamageSpecifier newDamage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        EntityUid? origin = null,
        bool ignoreGlobalModifiers = false,
        bool ignoreVariance = false)
    {
        newDamage = system.ChangeDamage(ent, damage, ignoreResistances, interruptsDoAfters, origin, ignoreGlobalModifiers, ignoreVariance);
        return !newDamage.Empty;
    }

    public static DamageSpecifier ChangeDamage(
        this DamageableSystem system,
        Entity<DamageableComponent?> ent,
        DamageSpecifier damage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        EntityUid? origin = null,
        bool ignoreGlobalModifiers = false,
        bool ignoreVariance = false,
        float armorPenetration = 0f,
        bool canHeal = true)
    {
        if (damage.Empty)
            return new DamageSpecifier();

        if (!ignoreVariance)
        {
            var random = IoCManager.Resolve<IRobustRandom>();
            var varianceMultiplier = 1f + random.NextFloat(-0.1f, 0.1f);
            damage *= varianceMultiplier;
        }

        return system.ChangeDamage(ent, damage, ignoreResistances, interruptsDoAfters, origin, ignoreGlobalModifiers, ignoreVariance, armorPenetration, canHeal);
    }
}
