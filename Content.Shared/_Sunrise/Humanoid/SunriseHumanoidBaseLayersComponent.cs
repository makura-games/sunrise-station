using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SunriseHumanoidBodySystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class SunriseHumanoidBaseLayersComponent : Component
{
    /// <summary>
    /// Base layers hidden by Sunrise mechanics independently of equipped clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();

    /// <summary>
    /// Runtime layer sprite or color overrides keyed by humanoid layer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public readonly partial struct CustomBaseLayerInfo
{
    public CustomBaseLayerInfo(PrototypeLayerData? data, Color? color = null)
    {
        Data = data;
        Color = color;
    }

    /// <summary>
    /// Optional replacement layer data.
    /// </summary>
    [DataField]
    public PrototypeLayerData? Data { get; init; }

    /// <summary>
    /// Optional replacement layer color.
    /// </summary>
    [DataField]
    public Color? Color { get; init; }
}
