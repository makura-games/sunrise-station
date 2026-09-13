using Content.Shared._Sunrise;
using Content.Shared._Sunrise.TTS;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SunriseHumanoidProfileSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class SunriseHumanoidProfileComponent : Component
{
    /// <summary>
    /// TTS voice prototype used by this humanoid.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<TTSVoicePrototype> Voice = SunriseHumanoidProfileDefaults.DefaultVoice;

    /// <summary>
    /// Body type prototype used for Sunrise body-type sprites and displacements.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<BodyTypePrototype> BodyType = SunriseHumanoidProfileDefaults.DefaultBodyType;

    /// <summary>
    /// Horizontal scale selected by the character profile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Width = 1f;

    /// <summary>
    /// Vertical scale selected by the character profile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Height = 1f;
}
