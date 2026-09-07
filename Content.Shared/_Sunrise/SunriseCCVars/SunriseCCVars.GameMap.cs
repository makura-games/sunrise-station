using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Разрешает загружать игровые карты из UserData с приоритетом над ресурсами билда.
    /// Если выключено, игровые карты читаются только из упакованного контента.
    /// </summary>
    public static readonly CVarDef<bool> GameMapUseUserData =
        CVarDef.Create("sunrise.game_map_use_user_data", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
