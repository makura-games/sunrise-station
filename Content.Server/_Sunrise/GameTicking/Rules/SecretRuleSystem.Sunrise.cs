using Content.Server.GameTicking.Presets;
using Prometheus;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.GameTicking.Rules;

public sealed partial class SecretRuleSystem
{
    private readonly Counter _sunriseSecretPresetSelectedCounter = Metrics.CreateCounter(
        "secret_preset_selected",
        "Amount of times each preset was selected in secret mode",
        new CounterConfiguration
        {
            LabelNames = ["preset"]
        });

    private void TrackSunriseSecretPreset(GamePresetPrototype preset)
    {
        _sunriseSecretPresetSelectedCounter.WithLabels(preset.ID).Inc();
    }

    private bool CanPickSunrisePreset(GamePresetPrototype? preset, int players, string ruleComponentName)
    {
        return GameTicker.IsPresetEligible(preset, players, ruleComponentName);
    }
}
