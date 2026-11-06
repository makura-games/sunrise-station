using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class BZProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialNitrousOxide = mixture.GetMoles(Gas.NitrousOxide);
        var initialPlasma = mixture.GetMoles(Gas.Plasma);

        var environmentEfficiency = mixture.Volume / mixture.Pressure;
        var ratioEfficiency = Math.Min(initialNitrousOxide / initialPlasma, 1);

        var BZFormed = Math.Min(0.01f * ratioEfficiency * environmentEfficiency, Math.Min(initialNitrousOxide * 0.4f, initialPlasma * 0.8f));

        if (initialNitrousOxide - BZFormed * 0.4f < 0 || initialPlasma - (0.8f - BZFormed) < 0 || BZFormed <= 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        var amountDecomposed = 0.0f;
        var nitrousOxideDecomposedFactor = Math.Max(4.0f * (initialPlasma / (initialNitrousOxide + initialPlasma) - 0.75f), 0);
        if (nitrousOxideDecomposedFactor > 0)
        {
            amountDecomposed = 0.4f * BZFormed * nitrousOxideDecomposedFactor;
            mixture.AdjustMoles(Gas.Oxygen, amountDecomposed);
            mixture.AdjustMoles(Gas.Nitrogen, 0.5f * amountDecomposed);
        }

        mixture.AdjustMoles(Gas.BZ, Math.Max(0f, BZFormed * (1.0f - nitrousOxideDecomposedFactor)));
        mixture.AdjustMoles(Gas.NitrousOxide, -0.4f * BZFormed);
        mixture.AdjustMoles(Gas.Plasma, -0.8f * BZFormed * (1.0f - nitrousOxideDecomposedFactor));

        var energyReleased = BZFormed * (Atmospherics.BZFormationEnergy + nitrousOxideDecomposedFactor * (Atmospherics.NitrousOxideDecompositionEnergy - Atmospherics.BZFormationEnergy));

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
