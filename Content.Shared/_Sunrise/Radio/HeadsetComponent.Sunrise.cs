using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Radio.Components;

public sealed partial class HeadsetComponent
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, bool> EnabledChannels = new();

    [DataField, AutoNetworkedField]
    public Dictionary<string, float> ChannelVolumes = new();

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleHeadset";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}
