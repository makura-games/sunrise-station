// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Pets;

[Prototype]
public sealed partial class PetSelectionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name { get; private set; }

    [DataField]
    public LocId Description { get; private set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedDescription => Loc.GetString(Description);

    [DataField(required: true)]
    public EntProtoId PetEntity { get; private set; }

    [DataField]
    public bool SponsorOnly;
}
