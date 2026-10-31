using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PluoxiumProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (atmosphereSystem.IsSunriseReactionSuppressed(mixture))
            return ReactionResult.NoReaction;

        var initialCarbonDioxide = mixture.GetMoles(Gas.CarbonDioxide);
        var initialOxygen = mixture.GetMoles(Gas.Oxygen);
        var initialTritium = mixture.GetMoles(Gas.Tritium);

        var producedAmount = Math.Min(Atmospherics.PluoxiumMaxRate, Math.Min(initialCarbonDioxide, Math.Min(initialOxygen * 0.5f, initialTritium * 0.01f)));

        if (producedAmount <= 0 || initialCarbonDioxide - producedAmount < 0 || initialOxygen - producedAmount * 0.5f < 0 || initialTritium - producedAmount * 0.01f < 0)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.CarbonDioxide, -producedAmount);
        mixture.AdjustMoles(Gas.Oxygen, -producedAmount * 0.5f);
        mixture.AdjustMoles(Gas.Tritium, -producedAmount * 0.01f);
        mixture.AdjustMoles(Gas.Pluoxium, producedAmount);
        mixture.AdjustMoles(Gas.Hydrogen, producedAmount * 0.01f);

        var energyReleased = producedAmount * Atmospherics.PluoxiumFormationEnergy;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
