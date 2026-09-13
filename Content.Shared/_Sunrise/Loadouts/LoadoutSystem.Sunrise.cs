using Content.Shared._Sunrise.Loadouts;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Clothing;

public sealed partial class LoadoutSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private static bool _customLoadoutEnabled = SunriseCCVars.CustomLoadoutEnabled.DefaultValue;
    private static string _loadoutPool = SunriseCCVars.LoadoutPool.DefaultValue;

    private void InitializeSunrise()
    {
        Subs.CVar(_cfg, SunriseCCVars.CustomLoadoutEnabled, value => _customLoadoutEnabled = value, true);
        Subs.CVar(_cfg, SunriseCCVars.LoadoutPool, value => _loadoutPool = value, true);
    }

    public static ProtoId<RoleLoadoutPrototype> GetEffectiveJobPrototype(string? loadout, IPrototypeManager protoMan)
    {
        var rolePrototype = GetJobPrototype(loadout);
        return GetEffectiveRolePrototype(rolePrototype, protoMan);
    }

    public static ProtoId<RoleLoadoutPrototype> GetEffectiveRolePrototype(ProtoId<RoleLoadoutPrototype> rolePrototype, IPrototypeManager protoMan)
    {
        if (!_customLoadoutEnabled)
            return rolePrototype;

        if (!protoMan.TryIndex<LoadoutPoolPrototype>(_loadoutPool, out var poolProto))
            return rolePrototype;

        if (poolProto.RoleLoadouts.TryGetValue(rolePrototype, out var overridePrototype) && protoMan.HasIndex(overridePrototype))
            return overridePrototype;

        return rolePrototype;
    }
}
