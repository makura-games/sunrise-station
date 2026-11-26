using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.TTS;

/// <summary>
/// Component for cyborgs that allows them to change their TTS voice.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgVoiceComponent : Component
{
    /// <summary>
    /// The currently selected voice prototype ID.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public ProtoId<TTSVoicePrototype>? SelectedVoiceId { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public string VoiceEffect = "robot";

    [DataField]
    public List<ProtoId<TTSVoicePrototype>>? VoiceWhitelist;

    [DataField]
    public List<ProtoId<TTSVoicePrototype>>? VoiceBlacklist;

    /// <summary>
    /// Whether this borg may open the voice change UI and change its selected voice.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool VoiceChangeEnabled = true;

    /// <summary>
    /// Voice change action removed while <see cref="VoiceChangeEnabled"/> is false.
    /// </summary>
    [NonSerialized, ViewVariables]
    public EntityUid? VoiceChangeActionEntity;
}
