using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class NitriumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialTritium = mixture.GetMoles(Gas.Tritium);
        var initialNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var initialBZ = mixture.GetMoles(Gas.BZ);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature / Atmospherics.NitriumFormationTempDivisor, Math.Min(initialTritium, Math.Min(initialNitrogen, initialBZ * 0.05f)));

        if (heatEfficiency <= 0 || initialTritium - heatEfficiency < 0 || initialNitrogen - heatEfficiency < 0 || initialBZ - heatEfficiency * 0.05f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Tritium, -heatEfficiency);
        mixture.AdjustMoles(Gas.Nitrogen, -heatEfficiency);
        mixture.AdjustMoles(Gas.BZ, -heatEfficiency * 0.05f);
        mixture.AdjustMoles(Gas.Nitrium, heatEfficiency);

        var energyUsed = heatEfficiency * Atmospherics.NitriumFormationEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity - energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
