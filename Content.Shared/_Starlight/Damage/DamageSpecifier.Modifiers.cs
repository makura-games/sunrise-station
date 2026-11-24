using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    private static float ApplyDamageModifier(
        DamageModifierSet modifierSet,
        ProtoId<DamageTypePrototype> damageType,
        float value,
        float armorPenetration,
        bool canHeal)
    {
        if (modifierSet.FlatReductions.TryGetValue(damageType, out var reduction))
            value = Math.Max(0f, value - reduction * (1f - armorPenetration));

        if (!modifierSet.Coefficients.TryGetValue(damageType, out var coefficient))
            return value;

        var effectiveCoefficient = coefficient + (1f - coefficient) * armorPenetration;
        return value * (canHeal ? effectiveCoefficient : Math.Max(0f, effectiveCoefficient));
    }
}
