using Content.Shared.FixedPoint;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    public static DamageSpecifier ApplyModifier(DamageSpecifier damageSpec, float damageModifier, float healModifier)
    {
        var newDamage = new DamageSpecifier();
        newDamage.DamageDict.EnsureCapacity(damageSpec.DamageDict.Count);

        foreach (var (damageType, value) in damageSpec.DamageDict)
        {
            if (value == FixedPoint2.Zero)
                continue;

            var modifier = value > FixedPoint2.Zero ? damageModifier : healModifier;
            var modifiedValue = value.Float() * modifier;
            if (modifiedValue != 0f)
                newDamage.DamageDict[damageType] = FixedPoint2.New(modifiedValue);
        }

        return newDamage;
    }
}
