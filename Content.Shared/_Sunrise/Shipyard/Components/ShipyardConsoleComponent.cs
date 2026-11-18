using Content.Shared.Cargo.Prototypes;
using Content.Shared.Radio;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Shipyard.Components;

/// <summary>
/// Stores the configuration and the shuttle linked to a shipyard console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipyardConsoleComponent : Component
{
    /// <summary>
    /// Account charged for purchases and credited after a sale.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";

    /// <summary>
    /// Group of vessel prototypes displayed by this console.
    /// </summary>
    [DataField]
    public string VesselGroup = "station";

    /// <summary>
    /// Priority docking port tag used when selecting a vessel's docking location.
    /// </summary>
    [DataField]
    public string? PriorityTag;

    /// <summary>
    /// Explicit vessel prototypes added to this console in addition to its group.
    /// </summary>
    [DataField]
    public List<ProtoId<ShipyardVesselPrototype>> Vessels = new();

    /// <summary>
    /// Fraction of the purchase price returned when the shuttle is sold.
    /// </summary>
    [DataField]
    public float SellRate = 0.7f;

    /// <summary>
    /// Maximum distance from the station at which the linked shuttle can be sold.
    /// </summary>
    [DataField]
    public float MaxSellDistance = 300f;

    /// <summary>
    /// Delay between reserving funds and deploying a purchased shuttle.
    /// </summary>
    [DataField]
    public TimeSpan PurchaseDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Delay before a queued shuttle sale is completed.
    /// </summary>
    [DataField]
    public TimeSpan SaleDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Radio channel used for purchase and sale announcements.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> AnnouncementChannel = "Supply";

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// Shuttle currently owned by this console.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? CurrentShuttle;

    /// <summary>
    /// Price paid for the linked shuttle.
    /// </summary>
    [AutoNetworkedField]
    public int CurrentShuttlePrice;

    /// <summary>
    /// Appraised value of the shuttle immediately after purchase.
    /// Used to reduce the refund when equipment is removed from the shuttle.
    /// </summary>
    public double InitialShuttleAppraisal;

    /// <summary>
    /// Prototype of the shuttle currently linked to the console.
    /// </summary>
    [AutoNetworkedField]
    public ProtoId<ShipyardVesselPrototype>? CurrentShuttleVessel;
}
