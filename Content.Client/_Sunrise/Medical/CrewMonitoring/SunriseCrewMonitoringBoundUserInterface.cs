using Content.Shared.Medical.CrewMonitoring;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Medical.CrewMonitoring;

public sealed class SunriseCrewMonitoringBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SunriseCrewMonitoringWindow? _menu;

    public SunriseCrewMonitoringBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
                stationName = metaData.EntityName;
        }

        _menu = this.CreateWindow<SunriseCrewMonitoringWindow>();
        _menu.SetBoundUserInterface(this);
        _menu.Set(stationName, gridUid);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CrewMonitoringState st)
            return;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.ShowSensors(st.Sensors, Owner, xform?.Coordinates);
        _menu?.UpdateCorpseAlertToggle(st.CorpseAlertEnabled);
    }
}
