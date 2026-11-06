using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Repairable;

public sealed partial class RepairableSystem
{
    private static readonly ProtoId<DamageTypePrototype> ManglenessDamageType = "Mangleness";

    private static bool CanRepair(DamageSpecifier damage, RepairableComponent component)
    {
        if (component.Damage == null)
            return true;

        foreach (var type in component.Damage.DamageDict.Keys)
        {
            if (type == ManglenessDamageType)
                continue;

            if (damage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                return true;
        }

        return false;
    }
}
