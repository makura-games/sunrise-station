using Content.Shared.Humanoid;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise;

[Prototype]
public sealed partial class BodyTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// User-visible localization key for this body type.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = default!;

    /// <summary>
    /// Stable key used by clothing states and displacement maps.
    /// </summary>
    [DataField(required: true)]
    public string VisualKey = default!;

    /// <summary>
    /// Base visual layer overrides shared by every sex.
    /// </summary>
    [DataField]
    public Dictionary<HumanoidVisualLayers, PrototypeLayerData> Layers = new();

    /// <summary>
    /// Sex-specific base visual layer overrides.
    /// </summary>
    [DataField]
    public Dictionary<Sex, Dictionary<HumanoidVisualLayers, PrototypeLayerData>> SexLayers = new();

    [DataField]
    public List<Sex> SexRestrictions = new();

    /// <summary>
    /// Gets the most specific layer data for the requested sex.
    /// </summary>
    public bool TryGetLayer(HumanoidVisualLayers layer, Sex sex, out PrototypeLayerData data)
    {
        if (SexLayers.TryGetValue(sex, out var sexLayers) &&
            sexLayers.TryGetValue(layer, out data!))
        {
            return true;
        }

        return Layers.TryGetValue(layer, out data!);
    }
}
