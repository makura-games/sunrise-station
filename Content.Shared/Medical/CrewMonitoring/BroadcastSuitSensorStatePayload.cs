using Content.Shared.DeviceNetwork;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Map;

namespace Content.Shared.Medical.CrewMonitoring;

/// <summary>
/// Broadcast payoad from the crew monitoring server to all crew monitors.
/// </summary>
public partial record struct BroadcastSuitSensorStatePayload : INetworkPayload
{
    [DataField]
    public Dictionary<string, SuitSensorStatus> SensorStatus = new();

    [DataField]
    public MapId? MapId; // Sunrise-Edit - не смешиваем данные серверов мониторинга с разных карт.
}
