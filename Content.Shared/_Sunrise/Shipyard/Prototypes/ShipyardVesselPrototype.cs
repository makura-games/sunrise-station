using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Shipyard.Prototypes;

/// <summary>
/// Defines a shuttle that can be purchased through a shipyard console.
/// </summary>
[Prototype]
public sealed partial class ShipyardVesselPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ShipyardVesselPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    /// Unique identifier of the vessel prototype.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localization key for the vessel name displayed by shipyard consoles.
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    /// <summary>
    /// Localization key for the vessel description displayed by shipyard consoles.
    /// </summary>
    [DataField]
    public LocId Description { get; private set; } = default!;

    /// <summary>
    /// Number of credits charged when the vessel is purchased.
    /// </summary>
    [DataField(required: true)]
    public int Price { get; private set; }

    /// <summary>
    /// Fraction of the purchase price returned on sale, before the condition adjustment.
    /// If omitted, the console's sell rate is used.
    /// </summary>
    [DataField]
    public float? SellRate { get; private set; }

    /// <summary>
    /// Resource path to the grid map loaded when the vessel is purchased.
    /// </summary>
    [DataField(required: true)]
    public ResPath GridPath { get; private set; } = default!;

    /// <summary>
    /// Vessel group used to determine which shipyard consoles list this vessel.
    /// </summary>
    [DataField]
    public string Group { get; private set; } = "station";

    /// <summary>
    /// Rotation applied to the grid when the vessel map is loaded.
    /// </summary>
    [DataField]
    public Angle Rotation { get; private set; } = Angle.Zero;
}
