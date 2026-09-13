using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ZaukerProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialHypernoblium = mixture.GetMoles(Gas.HyperNoblium);
        var initialNitrium = mixture.GetMoles(Gas.Nitrium);

        var temperature = mixture.Temperature;
        var heatEfficiency = Math.Min(temperature * Atmospherics.ZaukerFormationTemperatureScale, Math.Min(initialHypernoblium * 0.01f, initialNitrium * 0.5f));

        if (heatEfficiency <= 0 || initialHypernoblium - heatEfficiency * 0.01f < 0 || initialNitrium - heatEfficiency * 0.5f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.HyperNoblium, -heatEfficiency * 0.01f);
        mixture.AdjustMoles(Gas.Nitrium, -heatEfficiency * 0.5f);
        mixture.AdjustMoles(Gas.Zauker, heatEfficiency * 0.5f);

        var energyUsed = heatEfficiency * Atmospherics.ZaukerFormationEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity - energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
