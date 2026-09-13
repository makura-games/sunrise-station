using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class NitriumDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialNitrium = mixture.GetMoles(Gas.Nitrium);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature / Atmospherics.NitriumDecompositionTempDivisor, initialNitrium);

        if (heatEfficiency <= 0 || initialNitrium - heatEfficiency < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Nitrium, -heatEfficiency);
        mixture.AdjustMoles(Gas.Hydrogen, heatEfficiency);
        mixture.AdjustMoles(Gas.Nitrogen, heatEfficiency);

        var energyReleased = heatEfficiency * Atmospherics.NitriumDecompositionEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
