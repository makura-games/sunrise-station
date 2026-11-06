using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HalonOxygenAbsorptionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialHalon = mixture.GetMoles(Gas.Halon);
        var initialOxygen = mixture.GetMoles(Gas.Oxygen);

        var temperature = mixture.Temperature;

        var heatEfficiency = Math.Min(temperature / (Atmospherics.FireMinimumTemperatureToExist * 10f), Math.Min(initialHalon, initialOxygen * 20f));
        if (heatEfficiency <= 0f || initialHalon - heatEfficiency < 0f || initialOxygen - heatEfficiency * 20f < 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Halon, -heatEfficiency);
        mixture.AdjustMoles(Gas.Oxygen, -heatEfficiency * 20f);
        mixture.AdjustMoles(Gas.CarbonDioxide, heatEfficiency * 5f);

        var energyUsed = heatEfficiency * Atmospherics.HalonCombustionEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
