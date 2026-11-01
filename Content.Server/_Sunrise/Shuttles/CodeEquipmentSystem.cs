using Content.Server._Sunrise.ImmortalGrid;
using Content.Server.Chat.Systems;
using Content.Server.Pinpointer;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.AlertLevel;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Shuttles;

public sealed partial class CodeEquipmentSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private NavMapSystem _nav = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CodeEquipmentComponent, StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<CodeEquipmentShuttleComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<CodeEquipmentShuttleComponent, FTLTagEvent>(OnFTLShuttleTag);
        SubscribeLocalEvent<CodeEquipmentShuttleComponent, FTLStartedEvent>(OnFTLStartedEvent);
        SubscribeLocalEvent<CodeEquipmentShuttleComponent, FTLCompletedEvent>(OnFTLCompletedEvent);
        SubscribeLocalEvent<SunriseAlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnStationPostInit(EntityUid uid, CodeEquipmentComponent comp, StationPostInitEvent ev)
    {
        _map.CreateMap(out var mapId);
        var loadOptions = new DeserializationOptions();
        loadOptions.InitializeMaps = true;
        loadOptions.StoreYamlUids = true;
        _loader.TryLoadGrid(mapId, comp.ShuttlePath, out var shuttleUid, loadOptions);
        if (shuttleUid is null)
            return;
        comp.Shuttles.Add(shuttleUid.Value.Owner);
        var gammaArmoryComp = EnsureComp<CodeEquipmentShuttleComponent>(shuttleUid.Value.Owner);
        gammaArmoryComp.Station = uid;
    }

    private void OnFTLShuttleTag(EntityUid uid, CodeEquipmentShuttleComponent comp, ref FTLTagEvent ev)
    {
        if (ev.Handled)
            return;

        ev.Handled = true;
        ev.Tag = comp.PriorityTag;
    }

    private void OnComponentStartup(EntityUid uid, CodeEquipmentShuttleComponent comp, ComponentStartup ev)
    {
        EnsureComp<PreventPilotComponent>(uid);
    }

    private void OnFTLStartedEvent(EntityUid uid, CodeEquipmentShuttleComponent comp, ref FTLStartedEvent ev)
    {

    }

    private void OnFTLCompletedEvent(EntityUid uid, CodeEquipmentShuttleComponent comp, ref FTLCompletedEvent ev)
    {
        if (comp.EnableDockAnnouncement)
        {
            var xform = Transform(uid);
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString(comp.DockAnnounceMessage, ("location", FormattedMessage.RemoveMarkup(_nav.GetNearestBeaconString((uid, xform))))),
                colorOverride: Color.PaleVioletRed,
                announceVoice: "Azir");
        }
    }

    private void OnAlertLevelChanged(ref SunriseAlertLevelChangedEvent ev)
    {
        if (!TryComp<CodeEquipmentComponent>(ev.Station, out var comp))
            return;

        if (!ev.AlertLevel.Id.Equals(comp.TargetCode, StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryComp<StationDataComponent>(ev.Station, out var stationData))
            return;

        var target = _station.GetLargestGrid((ev.Station, stationData));

        if (target == null)
            return;

        _shuttles.FTLToDockSunrise(
            comp.Shuttles[0],
            Comp<ShuttleComponent>(comp.Shuttles[0]),
            target.Value,
            priorityTag: comp.PriorityTag,
            ignored: true);
    }
}
