using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Antags.Abductor;

[Prototype]
[DataDefinition]
public sealed partial class AbductorListingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name;

    [DataField(required: true)]
    public EntProtoId ProductEntity;

    [DataField(required: true)]
    public int Cost;
}
