using Content.Shared._Sunrise.Shipyard.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Shipyard.Events;

/// <summary>
/// Requests purchase of a vessel available to the current shipyard console.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage : BoundUserInterfaceMessage
{
    public ProtoId<ShipyardVesselPrototype> VesselId;

    public ShipyardConsolePurchaseMessage(ProtoId<ShipyardVesselPrototype> vesselId)
    {
        VesselId = vesselId;
    }
}
