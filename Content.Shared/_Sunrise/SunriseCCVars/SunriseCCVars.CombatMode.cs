using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Определяет, показывать ли значок боевого режима над персонажем игрока.
    /// </summary>
    public static readonly CVarDef<bool> CombatModeOverheadIndicatorEnabled =
        CVarDef.Create("sunrise.combat_mode_overhead_indicator_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет, проигрывать ли звук переключения боевого режима клиенту.
    /// </summary>
    public static readonly CVarDef<bool> CombatModeToggleSoundEnabled =
        CVarDef.Create("sunrise.combat_mode_toggle_sound_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
