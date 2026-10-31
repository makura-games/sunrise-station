namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    /// <summary>
    /// Проверяет, подавляет ли HyperNoblium газовые реакции Sunrise.
    /// </summary>
    public bool IsSunriseReactionSuppressed(GasMixture mixture)
    {
        return mixture.GetMoles(Gas.HyperNoblium) >= Atmospherics.ReactionSuppressionThreshold
            && mixture.Temperature > Atmospherics.ReactionSuppressionMinimumTemperature;
    }
}
