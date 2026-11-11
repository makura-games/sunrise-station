using Content.Shared._Sunrise.Humanoid;

// ReSharper disable once CheckNamespace
namespace Content.Shared.VoiceMask;

public sealed partial class VoiceMaskComponent
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string VoiceId = SunriseHumanoidProfileDefaults.DefaultVoice;
}
