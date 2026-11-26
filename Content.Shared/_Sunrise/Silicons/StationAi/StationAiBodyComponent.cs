using Content.Shared._Sunrise.TTS;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Silicons.StationAi;

/// <summary>
/// Marks a borg chassis prepared as a station AI body through an AI communication board.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiBodyComponent : Component
{
    /// <summary>
    /// Round-local visible number of this AI body.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public int BodyNumber;

    /// <summary>
    /// Communication board currently installed into the chassis brain slot.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public EntityUid? Board;

    /// <summary>
    /// Station AI brain currently controlling this body, or null while the body is free.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public EntityUid? LinkedAi;

    /// <summary>
    /// Action prototypes granted to the borg chassis while it is controlled as a station AI body.
    /// </summary>
    [DataField]
    public List<EntProtoId> ControlledBodyActions = new()
    {
        "ActionStationAiBodyMenu",
        "ActionStationAiBodyExit",
    };

    /// <summary>
    /// Action entities granted from <see cref="ControlledBodyActions"/> during active control.
    /// </summary>
    [NonSerialized, ViewVariables]
    public List<EntityUid> ControlledBodyActionEntities = [];

    /// <summary>
    /// Body voice before the linked AI voice was copied to it.
    /// </summary>
    [NonSerialized, ViewVariables]
    public ProtoId<TTSVoicePrototype>? CachedBodyVoiceId;

    /// <summary>
    /// Whether this system added <see cref="Content.Shared.StatusIcon.Components.StatusIconComponent"/> for the AI-body HUD icon.
    /// </summary>
    [NonSerialized, ViewVariables]
    public bool AddedStatusIconComponent;

    /// <summary>
    /// Original radio channels of the body before station AI channels were inherited.
    /// </summary>
    [NonSerialized, ViewVariables]
    public Dictionary<string, HashSet<ProtoId<RadioChannelPrototype>>> CachedRadioChannels = new();
}
