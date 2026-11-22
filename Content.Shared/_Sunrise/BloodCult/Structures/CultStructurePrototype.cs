using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.BloodCult.Structures;

[Prototype]
public sealed partial class CultStructurePrototype : IPrototype
{
    [DataField(required: true)]
    public EntProtoId StructureId;

    [DataField("name", required: true)]
    public string StructureName = string.Empty;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public BloodCultType? CultType;
}
