using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HealiumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialBZ = mixture.GetMoles(Gas.BZ);
        var initialFrezon = mixture.GetMoles(Gas.Frezon);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature * 0.3f, Math.Min(initialFrezon * 2.75f, initialBZ * 0.25f));

        if (heatEfficiency <= 0 || initialFrezon - heatEfficiency * 2.75f < 0 || initialBZ - heatEfficiency * 0.25f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Frezon, -heatEfficiency * 2.75f);
        mixture.AdjustMoles(Gas.BZ, -heatEfficiency * 0.25f);
        mixture.AdjustMoles(Gas.Healium, heatEfficiency * 3);

        var energyReleased = heatEfficiency * Atmospherics.HealiumFormationEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
