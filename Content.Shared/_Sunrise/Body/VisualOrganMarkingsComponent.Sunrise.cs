using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Robust.Shared.Enums;

namespace Content.Shared.Body;

public sealed partial class VisualOrganMarkingsComponent
{
    /// <summary>
    /// Body-type VisualKey-specific displacement map data for markings on this organ.
    /// </summary>
    [DataField]
    public Dictionary<string, Dictionary<HumanoidVisualLayers, DisplacementData>> BodyTypeMarkingsDisplacement = new();

    /// <summary>
    /// Sex-specific displacement map data for markings on this organ.
    /// </summary>
    [DataField]
    public Dictionary<Sex, Dictionary<HumanoidVisualLayers, DisplacementData>> SexMarkingsDisplacement = new();

    /// <summary>
    /// Body-type VisualKey and sex specific displacement map data for markings on this organ.
    /// </summary>
    [DataField]
    public Dictionary<string, Dictionary<Sex, Dictionary<HumanoidVisualLayers, DisplacementData>>> BodyTypeSexMarkingsDisplacement = new();
}
