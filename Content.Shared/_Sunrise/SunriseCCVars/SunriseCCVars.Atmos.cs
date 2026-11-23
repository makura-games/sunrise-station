using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// Эти переменные управляют модификаторами цен разных газов.
    /// Если для газа нет отдельного модификатора, используется цена по умолчанию из прототипа.
    /// </summary>

    public static readonly CVarDef<float> DefaultGasPriceModifier =
        CVarDef.Create("atmos.gas_price_modifier_default", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierCarbonDioxide =
        CVarDef.Create("atmos.gas_price_modifier_carbon_dioxide", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierPlasma =
        CVarDef.Create("atmos.gas_price_modifier_plasma", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierTritium =
        CVarDef.Create("atmos.gas_price_modifier_tritium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierWaterVapor =
        CVarDef.Create("atmos.gas_price_modifier_water_vapor", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierAmmonia =
        CVarDef.Create("atmos.gas_price_modifier_ammonia", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierNitrousOxide =
        CVarDef.Create("atmos.gas_price_modifier_nitrous_oxide", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierFrezon =
        CVarDef.Create("atmos.gas_price_modifier_frezon", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierBZ =
        CVarDef.Create("atmos.gas_price_modifier_bz", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierPluoxium =
        CVarDef.Create("atmos.gas_price_modifier_pluoxium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierHydrogen =
        CVarDef.Create("atmos.gas_price_modifier_hydrogen", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierNitrium =
        CVarDef.Create("atmos.gas_price_modifier_nitrium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierHealium =
        CVarDef.Create("atmos.gas_price_modifier_healium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierHyperNoblium =
        CVarDef.Create("atmos.gas_price_modifier_hyper_noblium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierProtoNitrate =
        CVarDef.Create("atmos.gas_price_modifier_proto_nitrate", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierZauker =
        CVarDef.Create("atmos.gas_price_modifier_zauker", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierHalon =
        CVarDef.Create("atmos.gas_price_modifier_halon", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierHelium =
        CVarDef.Create("atmos.gas_price_modifier_helium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierAntiNoblium =
        CVarDef.Create("atmos.gas_price_modifier_anti_noblium", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierElectrovae =
        CVarDef.Create("atmos.gas_price_modifier_electrovae", 1f, CVar.SERVER);

    public static readonly CVarDef<float> GasPriceModifierChargedElectrovae =
        CVarDef.Create("atmos.gas_price_modifier_charged_electrovae", 1f, CVar.SERVER);
}
