using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateTritiumConversionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialProtoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var initialTritium = mixture.GetMoles(Gas.Tritium);

        var temperature = mixture.Temperature; 
        var producedAmount = Math.Min(temperature / 34f * initialTritium * initialProtoNitrate/ (initialTritium + 10f * initialProtoNitrate), Math.Min(initialTritium, initialProtoNitrate * 0.01f));

        if (initialTritium - producedAmount < 0 || initialProtoNitrate - producedAmount * 0.01f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.ProtoNitrate, -producedAmount * 0.01f);
        mixture.AdjustMoles(Gas.Tritium, -producedAmount);
        mixture.AdjustMoles(Gas.Hydrogen, producedAmount);

        var energyReleased = producedAmount * Atmospherics.ProtoNitrateTritiumConversionEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
