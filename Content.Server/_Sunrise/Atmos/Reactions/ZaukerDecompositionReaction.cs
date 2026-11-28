using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ZaukerDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialZauker = mixture.GetMoles(Gas.Zauker);
        var initialNitrogen = mixture.GetMoles(Gas.Nitrogen);

        var burnedFuel = Math.Min(Atmospherics.ZaukerDecompositionMaxRate, Math.Min(initialNitrogen, initialZauker));

        if (burnedFuel <= 0 || initialZauker - burnedFuel < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Zauker, -burnedFuel);
        mixture.AdjustMoles(Gas.Oxygen, burnedFuel * 0.3f);
        mixture.AdjustMoles(Gas.Nitrogen, burnedFuel * 0.7f);

        var energyReleased = burnedFuel * Atmospherics.ZaukerDecompositionEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
