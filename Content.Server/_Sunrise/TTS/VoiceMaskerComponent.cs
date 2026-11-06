using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.TTS;
using Robust.Shared.Prototypes;

namespace Content.Server.VoiceMask;

[RegisterComponent]
public sealed partial class VoiceMaskerComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<TTSVoicePrototype> VoiceId = SunriseHumanoidProfileDefaults.DefaultVoice;
}
