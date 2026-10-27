using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;

namespace Content.Server.Atmos.EntitySystems;

public enum GasIds
{
    CarbonDioxide,
    Plasma,
    Tritium,
    WaterVapor,
    Ammonia,
    NitrousOxide,
    Frezon,
    BZ,
    Pluoxium,
    Hydrogen,
    Nitrium,
    Healium,
    HyperNoblium,
    ProtoNitrate,
    Zauker,
    Halon,
    Helium,
    AntiNoblium,
    Electrovae,
    ChargedElectrovae
}

public partial class AtmosphereSystem
{
    private float _defaultGasPriceModifier;
    private float _gasPriceModifierCarbonDioxide;
    private float _gasPriceModifierPlasma;
    private float _gasPriceModifierTritium;
    private float _gasPriceModifierWaterVapor;
    private float _gasPriceModifierAmmonia;
    private float _gasPriceModifierNitrousOxide;
    private float _gasPriceModifierFrezon;
    private float _gasPriceModifierBZ;
    private float _gasPriceModifierPluoxium;
    private float _gasPriceModifierHydrogen;
    private float _gasPriceModifierNitrium;
    private float _gasPriceModifierHealium;
    private float _gasPriceModifierHyperNoblium;
    private float _gasPriceModifierProtoNitrate;
    private float _gasPriceModifierZauker;
    private float _gasPriceModifierHalon;
    private float _gasPriceModifierHelium;
    private float _gasPriceModifierAntiNoblium;
    private float _gasPriceModifierElectrovae;
    private float _gasPriceModifierChargedElectrovae;

    private IDisposable? _configSub;

    public void InitSunriseAtmosCVars()
    {
        if (_configSub is not null)
        {
            return;
        }

        _configSub = _cfg.SubscribeMultiple()
            .OnValueChanged(SunriseCCVars.DefaultGasPriceModifier, (value) => _defaultGasPriceModifier = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierCarbonDioxide, (value) => _gasPriceModifierCarbonDioxide = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierPlasma, (value) => _gasPriceModifierPlasma = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierTritium, (value) => _gasPriceModifierTritium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierWaterVapor, (value) => _gasPriceModifierWaterVapor = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierAmmonia, (value) => _gasPriceModifierAmmonia = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierNitrousOxide, (value) => _gasPriceModifierNitrousOxide = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierFrezon, (value) => _gasPriceModifierFrezon = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierBZ, (value) => _gasPriceModifierBZ = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierPluoxium, (value) => _gasPriceModifierPluoxium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierHydrogen, (value) => _gasPriceModifierHydrogen = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierNitrium, (value) => _gasPriceModifierNitrium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierHealium, (value) => _gasPriceModifierHealium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierHyperNoblium, (value) => _gasPriceModifierHyperNoblium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierProtoNitrate, (value) => _gasPriceModifierProtoNitrate = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierZauker, (value) => _gasPriceModifierZauker = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierHalon, (value) => _gasPriceModifierHalon = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierHelium, (value) => _gasPriceModifierHelium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierAntiNoblium, (value) => _gasPriceModifierAntiNoblium = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierElectrovae, (value) => _gasPriceModifierElectrovae = value, true)
            .OnValueChanged(SunriseCCVars.GasPriceModifierChargedElectrovae, (value) => _gasPriceModifierChargedElectrovae = value, true);
    }

    public float GetModifier(string id)
    {
        if (!Enum.TryParse<GasIds>(id, out var gasId))
            return _defaultGasPriceModifier;

        return gasId switch
        {
            GasIds.CarbonDioxide => _gasPriceModifierCarbonDioxide,
            GasIds.Plasma => _gasPriceModifierPlasma,
            GasIds.Tritium => _gasPriceModifierTritium,
            GasIds.WaterVapor => _gasPriceModifierWaterVapor,
            GasIds.Ammonia => _gasPriceModifierAmmonia,
            GasIds.NitrousOxide => _gasPriceModifierNitrousOxide,
            GasIds.Frezon => _gasPriceModifierFrezon,
            GasIds.BZ => _gasPriceModifierBZ,
            GasIds.Pluoxium => _gasPriceModifierPluoxium,
            GasIds.Hydrogen => _gasPriceModifierHydrogen,
            GasIds.Nitrium => _gasPriceModifierNitrium,
            GasIds.Healium => _gasPriceModifierHealium,
            GasIds.HyperNoblium => _gasPriceModifierHyperNoblium,
            GasIds.ProtoNitrate => _gasPriceModifierProtoNitrate,
            GasIds.Zauker => _gasPriceModifierZauker,
            GasIds.Halon => _gasPriceModifierHalon,
            GasIds.Helium => _gasPriceModifierHelium,
            GasIds.AntiNoblium => _gasPriceModifierAntiNoblium,
            GasIds.Electrovae => _gasPriceModifierElectrovae,
            GasIds.ChargedElectrovae => _gasPriceModifierChargedElectrovae,
            _ => _defaultGasPriceModifier,
        };
    }

    private void ShutdownSunriseAtmosCVars()
    {
        _configSub?.Dispose();
    }
}
