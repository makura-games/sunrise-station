using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CrewMonitoring;

[Serializable, NetSerializable]
public enum CrewMonitoringUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class CrewMonitoringState : BoundUserInterfaceState
{
    public List<SuitSensorStatus> Sensors;
    public bool CorpseAlertEnabled;

    public CrewMonitoringState(List<SuitSensorStatus> sensors, bool corpseAlertEnabled = false)
    {
        Sensors = sensors;
        CorpseAlertEnabled = corpseAlertEnabled;
    }
}

[Serializable, NetSerializable]
public sealed class CrewMonitoringToggleCorpseAlertMessage : BoundUserInterfaceMessage
{
}
