using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Enables the bloom effect rendered around compatible light fixtures.
    /// </summary>
    public static readonly CVarDef<bool> LightBloomEnabled =
        CVarDef.Create("sunrise.light_bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Controls the bloom effect intensity. A value of zero disables the effect.
    /// </summary>
    public static readonly CVarDef<float> LightBloomStrength =
        CVarDef.Create("sunrise.light_bloom_strength", 0.7f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
