using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-контракту.
namespace Content.Shared.Communications;

public sealed partial class CommunicationsConsoleInterfaceState
{
    /// <summary>
    /// Additional alert levels available through this console.
    /// </summary>
    public readonly List<CommunicationsConsoleAdditionalAlertLevelState> AdditionalAlertLevels;

    /// <summary>
    /// Stations available as alert-level targets.
    /// </summary>
    public readonly List<CommunicationsConsoleAlertStationState> AlertStations;

    /// <summary>
    /// Station currently selected as the alert-level target.
    /// </summary>
    public readonly NetEntity? SelectedAlertStation;
}

/// <summary>
/// Describes an additional alert level shown by a communications console.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunicationsConsoleAdditionalAlertLevelState
{
    /// <summary>
    /// Prototype identifier of the alert level.
    /// </summary>
    public readonly string Level;

    /// <summary>
    /// Whether the alert level is currently active.
    /// </summary>
    public readonly bool Enabled;

    /// <summary>
    /// Whether the current console state permits changing this level.
    /// </summary>
    public readonly bool Selectable;

    /// <summary>
    /// Creates a state entry for an additional alert level.
    /// </summary>
    public CommunicationsConsoleAdditionalAlertLevelState(string level, bool enabled, bool selectable)
    {
        Level = level;
        Enabled = enabled;
        Selectable = selectable;
    }
}

/// <summary>
/// Requests an explicit additional alert-level state.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunicationsConsoleSetAdditionalAlertLevelMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Prototype identifier of the requested alert level.
    /// </summary>
    public readonly string Level;

    /// <summary>
    /// Requested active state.
    /// </summary>
    public readonly bool Enabled;

    /// <summary>
    /// Creates a request to explicitly enable or disable an additional alert level.
    /// </summary>
    public CommunicationsConsoleSetAdditionalAlertLevelMessage(string level, bool enabled)
    {
        Level = level;
        Enabled = enabled;
    }
}

/// <summary>
/// Describes a station available as an alert-level target.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunicationsConsoleAlertStationState
{
    /// <summary>
    /// Network entity of the station.
    /// </summary>
    public readonly NetEntity Station;

    /// <summary>
    /// Display name of the station.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Creates a selectable station entry.
    /// </summary>
    public CommunicationsConsoleAlertStationState(NetEntity station, string name)
    {
        Station = station;
        Name = name;
    }
}

/// <summary>
/// Requests a new alert-level target station.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommunicationsConsoleSelectAlertStationMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Network entity of the requested station.
    /// </summary>
    public readonly NetEntity Station;

    /// <summary>
    /// Creates a request to change the target station.
    /// </summary>
    public CommunicationsConsoleSelectAlertStationMessage(NetEntity station)
    {
        Station = station;
    }
}
