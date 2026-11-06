using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialPluoxium = mixture.GetMoles(Gas.Pluoxium);
        var initialHydrogen = mixture.GetMoles(Gas.Hydrogen);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature * 0.005f, Math.Min(initialPluoxium * 0.2f, initialHydrogen * 2.0f));

        if (heatEfficiency <= 0 || initialPluoxium - heatEfficiency * 0.2f < 0 || initialHydrogen - heatEfficiency * 2f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.Hydrogen, -heatEfficiency * 2f);
        mixture.AdjustMoles(Gas.Pluoxium, -heatEfficiency * 0.2f);
        mixture.AdjustMoles(Gas.ProtoNitrate, heatEfficiency * 0.2f);

        var energyReleased = heatEfficiency * Atmospherics.ProtoNitrateFormationEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
