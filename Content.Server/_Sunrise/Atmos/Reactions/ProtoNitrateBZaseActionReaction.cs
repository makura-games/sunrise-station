using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitrateBZaseConversionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialProtoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var initialBZ = mixture.GetMoles(Gas.BZ);

        var temperature = mixture.Temperature;
        var consumedAmount = Math.Min(temperature / 2240f * initialBZ * initialProtoNitrate / (initialBZ + initialProtoNitrate), Math.Min(initialBZ, initialProtoNitrate));

        if (consumedAmount <= 0 || initialBZ - consumedAmount < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.BZ, -consumedAmount);
        mixture.AdjustMoles(Gas.Nitrogen, consumedAmount * 0.4f);
        mixture.AdjustMoles(Gas.Helium, consumedAmount * 1.6f);
        mixture.AdjustMoles(Gas.Plasma, consumedAmount * 0.8f);

        var energyReleased = consumedAmount * Atmospherics.ProtoNitrateBZaseConversionEnergy;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
