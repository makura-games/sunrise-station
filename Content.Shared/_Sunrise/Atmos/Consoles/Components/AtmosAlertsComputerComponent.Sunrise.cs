// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.Components;

public sealed partial class AtmosAlertsComputerBoundInterfaceState
{
    /// <summary>
    ///     Управление пищалкой
    /// </summary>
    public bool DoAtmosAlert;

    public AtmosAlertsComputerBoundInterfaceState(
        AtmosAlertsComputerEntry[] airAlarms,
        AtmosAlertsComputerEntry[] fireAlarms,
        AtmosAlertsFocusDeviceData? focusData,
        bool doAtmosAlert)
        : this(airAlarms, fireAlarms, focusData)
    {
        DoAtmosAlert = doAtmosAlert;
    }
}
