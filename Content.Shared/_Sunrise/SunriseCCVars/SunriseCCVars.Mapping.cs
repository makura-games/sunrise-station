using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Run the equivalent of <c>cleandevicelinks</c> before mapper save serialization.
    /// </summary>
    public static readonly CVarDef<bool> MappingAutoCleanDeviceLinks =
        CVarDef.Create("mapping.auto_clean_device_links", true, CVar.SERVERONLY);

    /// <summary>
    /// Run the equivalent of <c>fixgridatmos</c> on every grid before mapper save serialization.
    /// </summary>
    public static readonly CVarDef<bool> MappingAutoFixGridAtmos =
        CVarDef.Create("mapping.auto_fix_grid_atmos", true, CVar.SERVERONLY);

    /// <summary>
    /// Run the equivalent of <c>tilewalls</c> on every grid before mapper save serialization.
    /// </summary>
    public static readonly CVarDef<bool> MappingAutoTileWalls =
        CVarDef.Create("mapping.auto_tile_walls", true, CVar.SERVERONLY);

    /// <summary>
    /// Run the equivalent of <c>removewalleddecals</c> on every grid before mapper save serialization.
    /// </summary>
    public static readonly CVarDef<bool> MappingAutoRemoveWalledDecals =
        CVarDef.Create("mapping.auto_remove_walled_decals", true, CVar.SERVERONLY);

    /// <summary>
    /// Run the equivalent of <c>variantize</c> on every grid before mapper save serialization.
    /// </summary>
    public static readonly CVarDef<bool> MappingAutoVariantize =
        CVarDef.Create("mapping.auto_variantize", true, CVar.SERVERONLY);

    /// <summary>
    /// Разрешает игре загружать игровые карты из UserData с приоритетом над ресурсами билда.
    /// Если выключено, игровые карты читаются только из упакованного контента.
    /// </summary>
    /// <remarks>
    /// По умолчанию карты сохраненные в UserData перезаписывают карты загруженные с билдом.
    /// Это задумывалось как фича для удобной проверки локального маппинга, чтобы без перезагрузки сервера проверить новую карту.
    /// Но на проде может произойти такое, что кто-то запишет карту в UserData, чем вызовет застрявшую до рестарта сервера карту.
    /// <para>В debug конфиге по умолчанию стоит true для локального маппинга. На проде будет false, кроме мапперских серверов</para>
    /// </remarks>
    public static readonly CVarDef<bool> GameMapUseUserData =
        CVarDef.Create("mapping.game_map_use_user_data", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
