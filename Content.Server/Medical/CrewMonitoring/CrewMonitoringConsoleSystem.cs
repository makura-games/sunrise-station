using System.Linq;
using Content.Shared.PowerCell;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map;


namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);

        InitializeAlert(); // Sunrise - Add
    }

    private void OnRemove(EntityUid uid, CrewMonitoringConsoleComponent component, ComponentRemove args)
    {
        component.ConnectedSensors.Clear();
    }

    private void OnPacketReceived(EntityUid uid, CrewMonitoringConsoleComponent component, DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;

        // Check command
        if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        if (command != DeviceNetworkConstants.CmdUpdatedState)
            return;

        if (!payload.TryGetValue(SuitSensorConstants.NET_STATUS_COLLECTION, out Dictionary<string, SuitSensorStatus>? sensorStatus))
            return;

        if (!payload.TryGetValue(SuitSensorConstants.MAP_ID, out MapId mapId))
            return;

        var consoleTransform = Transform(uid);
        var consoleMapId = consoleTransform.MapID;

        if (mapId != consoleMapId)
            return;

        component.ConnectedSensors = sensorStatus;
        UpdateUserInterface(uid, component);
    }

    private void OnUIOpened(EntityUid uid, CrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        var allSensors = component.ConnectedSensors.Values.ToList();

        // Sunrise - Start
        ApplyFilter(uid, ref allSensors);

        var corpseAlertEnabled = TryComp(uid, out CrewMonitoringCorpseAlertComponent? alert) && alert.DoCorpseAlert;
        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(allSensors, corpseAlertEnabled));
        // Sunrise - End
    }
}
