using Content.Shared._Sunrise.Humanoid;

namespace Content.Shared.VoiceMask;

public sealed partial class VoiceMaskBuiState
{
    public readonly string Voice = SunriseHumanoidProfileDefaults.DefaultVoice;

    public VoiceMaskBuiState(
        string name,
        string voice,
        string? verb,
        bool active,
        bool accentHide,
        LocId titleText)
        : this(name, verb, active, accentHide, titleText)
    {
        Voice = voice;
    }
}
