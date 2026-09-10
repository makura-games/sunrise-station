using Content.Shared.Communications;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleMenu
{
    /*
     * Controls for additional alert levels and remote station selection.
     */

    /// <summary>
    /// Raised when an additional alert level is toggled.
    /// </summary>
    public event Action<string, bool>? OnAdditionalAlertLevel;

    /// <summary>
    /// Raised when another target station is selected.
    /// </summary>
    public event Action<NetEntity>? OnAlertStation;

    private void InitializeAdditionalAlertLevelControls()
    {
        AlertStationButton.OnItemSelected += args =>
        {
            if (AlertStationButton.GetItemMetadata(args.Id) is not NetEntity station)
                return;

            AlertStationButton.Select(args.Id);
            OnAlertStation?.Invoke(station);
        };
    }

    /// <summary>
    /// Rebuilds the independently selectable additional alert-level controls.
    /// </summary>
    public void UpdateAdditionalAlertLevels(
        List<CommunicationsConsoleAdditionalAlertLevelState> alerts,
        bool hasAccess)
    {
        AdditionalAlertLevelsContainer.RemoveAllChildren();
        foreach (var alert in alerts)
        {
            var name = alert.Level;
            if (_loc.TryGetString($"alert-level-{alert.Level}", out var localizedName))
                name = localizedName;

            var checkBox = new CheckBox
            {
                Text = name,
                ToggleMode = true,
                Pressed = alert.Enabled,
                Disabled = !hasAccess || !alert.Selectable,
                HorizontalExpand = true,
                Margin = new Thickness(2),
            };

            checkBox.OnToggled += args =>
            {
                checkBox.Disabled = true;
                OnAdditionalAlertLevel?.Invoke(alert.Level, args.Pressed);
            };
            AdditionalAlertLevelsContainer.AddChild(checkBox);
        }

        AdditionalAlertLevelsSection.Visible = alerts.Count > 0;
    }

    /// <summary>
    /// Rebuilds the list of stations available as alert-level targets.
    /// </summary>
    public void UpdateAlertStations(
        List<CommunicationsConsoleAlertStationState> stations,
        NetEntity? selectedStation,
        bool hasAccess)
    {
        AlertStationButton.Clear();
        foreach (var station in stations)
        {
            AlertStationButton.AddItem(station.Name);
            AlertStationButton.SetItemMetadata(AlertStationButton.ItemCount - 1, station.Station);

            if (station.Station == selectedStation)
                AlertStationButton.Select(AlertStationButton.ItemCount - 1);
        }

        AlertStationSelectorContainer.Visible = stations.Count > 0;
        AlertStationButton.Disabled = !hasAccess || stations.Count < 2;
    }

    /// <summary>
    /// Disables alert-level controls while a server-confirmed state is pending.
    /// </summary>
    public void DisableAlertLevelControls()
    {
        AlertLevelButton.Disabled = true;
        foreach (var child in AdditionalAlertLevelsContainer.Children)
        {
            if (child is CheckBox checkBox)
                checkBox.Disabled = true;
        }
    }
}
