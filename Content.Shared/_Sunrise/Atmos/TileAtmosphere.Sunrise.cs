namespace Content.Shared.Atmos;

public sealed partial class TileAtmosphere
{
    /// <summary>
    /// Текущее состояние эффекта заряженного Electrovae на тайле.
    /// </summary>
    [ViewVariables]
    public ChargedElectrovaeEffect ChargedEffect;

    /// <summary>
    /// Сбрасывает тайл для повторного использования.
    /// </summary>
    public void Reset()
    {
        Air = null;
        AirArchived = null;
        Array.Clear(AdjacentTiles, 0, AdjacentTiles.Length);
        AdjacentBits = AtmosDirection.Invalid;
        ExcitedGroup = null;
        Hotspot = default;
        ChargedEffect = default;
        GridIndex = EntityUid.Invalid;
        GridIndices = default;
        MapAtmosphere = false;
        NoGridTile = false;
        Space = false;
        TrimQueued = false;
        ArchivedCycle = 0;
        CurrentCycle = 0;
        LastShare = 0f;
        MonstermosInfo = default;
        Excited = false;
        PressureDifference = 0f;
        PressureDirection = AtmosDirection.Invalid;
        LastPressureDirection = AtmosDirection.Invalid;
        PressureSpecificTarget = null;
        MaxFireTemperatureSustained = 0f;
        Temperature = Atmospherics.T20C;
        HeatCapacity = Atmospherics.MinimumHeatCapacity;
        ThermalConductivity = 0.05f;
        AirtightData = default;
    }
}

/// <summary>
/// Состояние эффекта заряженного Electrovae, хранящееся непосредственно на тайле.
/// </summary>
public struct ChargedElectrovaeEffect
{
    /// <summary>
    /// Активен ли эффект на этом тайле.
    /// </summary>
    [ViewVariables]
    public bool Active;

    /// <summary>
    /// Интенсивность эффекта от 0 до 1.
    /// </summary>
    [ViewVariables]
    public float Intensity;

    /// <summary>
    /// Состояние визуального эффекта от 0 до 3.
    /// </summary>
    [ViewVariables]
    public byte State;
}
