using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HyperNobliumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var initialTritium = mixture.GetMoles(Gas.Tritium);
        var initialBZ = mixture.GetMoles(Gas.BZ);

        var nobFormed = Math.Min((initialNitrogen + initialTritium) * 0.01f, Math.Min(initialTritium * 5f, initialNitrogen * 10f));
        if (nobFormed <= 0 || (initialTritium - 5f) * nobFormed < 0 || (initialNitrogen - 10f) * nobFormed < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        var reductionFactor = Math.Clamp(initialTritium / (initialTritium + initialBZ), 0.001f, 1f);

        mixture.AdjustMoles(Gas.Tritium, -5f * nobFormed * reductionFactor);
        mixture.AdjustMoles(Gas.Nitrogen, -10f * nobFormed);
        mixture.AdjustMoles(Gas.HyperNoblium, nobFormed);

        var energyReleased = nobFormed * (Atmospherics.NobliumFormationEnergy / Math.Max(initialBZ, 1));

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
