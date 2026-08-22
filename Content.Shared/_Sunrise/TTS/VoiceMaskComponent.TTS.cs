using Content.Shared._Sunrise.Humanoid;

namespace Content.Shared.VoiceMask;

public sealed partial class VoiceMaskComponent
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string VoiceId = SunriseHumanoidProfileDefaults.DefaultVoice;
}
