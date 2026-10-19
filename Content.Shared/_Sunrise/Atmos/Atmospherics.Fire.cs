namespace Content.Shared.Atmos;

public static partial class Atmospherics
{
    /// <summary>
    ///     Температура, при которой начинает работать мягкий потолок выделяемой огнём энергии.
    /// </summary>
    /// <remarks>
    ///     До этого значения огонь греет как в ваниле, чтобы обычные пожары оставались прежними.
    /// </remarks>
    public const float FireHeatReductionStartTemperature = 3000f;

    /// <summary>
    ///     Температура, при которой огонь перестаёт выделять энергию вообще.
    /// </summary>
    /// <remarks>
    ///     Ограничивает разгон температуры в замкнутом объёме, чтобы пожар не превращался в бесконечный нагрев.
    /// </remarks>
    public const float FireHeatCapTemperature = 10000f;

    /// <summary>
    ///     Коэффициент выделяемой огнём энергии в зависимости от температуры горения.
    /// </summary>
    /// <remarks>
    ///     Ниже <see cref="FireHeatReductionStartTemperature"/> равен 1 (обычное поведение),
    ///     выше — линейно падает до 0 к <see cref="FireHeatCapTemperature"/>.
    /// </remarks>
    public static float GetFireEnergyReleasedMultiplier(float temperature)
    {
        if (temperature <= FireHeatReductionStartTemperature)
            return 1f;

        if (temperature >= FireHeatCapTemperature)
            return 0f;

        return 1f - (temperature - FireHeatReductionStartTemperature)
                    / (FireHeatCapTemperature - FireHeatReductionStartTemperature);
    }
}
