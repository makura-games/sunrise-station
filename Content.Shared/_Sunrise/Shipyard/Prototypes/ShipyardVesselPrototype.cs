using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Shipyard.Prototypes;

/// <summary>
/// Defines a shuttle that can be purchased through a shipyard console.
/// </summary>
[Prototype]
public sealed partial class ShipyardVesselPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    [DataField]
    public LocId Description { get; private set; } = default!;

    [DataField(required: true)]
    public int Price { get; private set; }

    [DataField(required: true)]
    public ResPath GridPath { get; private set; } = default!;

    [DataField]
    public string Group { get; private set; } = "station";

    [DataField]
    public Angle Rotation { get; private set; } = Angle.Zero;
}
