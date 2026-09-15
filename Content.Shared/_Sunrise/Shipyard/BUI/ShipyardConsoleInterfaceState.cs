using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Shipyard.BUI;

/// <summary>
/// Server-authoritative state displayed by a shipyard console.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public readonly ProtoId<CargoAccountPrototype> Account;
    public readonly int Balance;
    public readonly ProtoId<ShipyardVesselPrototype>? CurrentShuttle;
    public readonly int CurrentShuttleSellValue;
    public readonly bool TransactionPending;
    public readonly List<ShipyardVesselData> Vessels;

    public ShipyardConsoleInterfaceState(
        ProtoId<CargoAccountPrototype> account,
        int balance,
        ProtoId<ShipyardVesselPrototype>? currentShuttle,
        int currentShuttleSellValue,
        bool transactionPending,
        List<ShipyardVesselData> vessels)
    {
        Account = account;
        Balance = balance;
        CurrentShuttle = currentShuttle;
        CurrentShuttleSellValue = currentShuttleSellValue;
        TransactionPending = transactionPending;
        Vessels = vessels;
    }
}

/// <summary>
/// Identifies an available vessel and its purchase price.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ShipyardVesselData(ProtoId<ShipyardVesselPrototype> Id, int Price);
