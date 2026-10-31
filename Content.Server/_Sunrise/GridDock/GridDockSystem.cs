using System.Linq;
using System.Numerics;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.GridDock;

public sealed partial class GridDockSystem : EntitySystem
{
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private DockingSystem _dockSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnGridAndDockToStationComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(EntityUid uid, SpawnGridAndDockToStationComponent component, StationPostInitEvent args)
    {
        if (component.Grids.Count == 0)
            return;

        var ftlMap = _shuttles.EnsureSunriseFtlMap();
        var xformMap = Transform(ftlMap);

        if (!TryComp<StationDataComponent>(uid, out var stationData))
            return;

        var target = _station.GetLargestGrid((uid, stationData));

        if (target == null)
        {
            Log.Warning($"GridDockSystem: No target grid found for {ToPrettyString(uid)}");
            return;
        }

        var index = 0f;
        var usedGridDocks = new HashSet<EntityUid>();
        foreach (var entry in component.Grids)
        {
            if (!_loader.TryLoadGrid(xformMap.MapID,
                    entry.GridPath,
                    out var rootUid))
                continue;

            var grid = Comp<MapGridComponent>(rootUid.Value.Owner);
            var width = grid.LocalAABB.Width;
            var shuttleCenter = grid.LocalAABB.Center;

            var coordinates = new EntityCoordinates(ftlMap, new Vector2(index + width / 2f, 0f) - shuttleCenter);
            _transform.SetCoordinates(rootUid.Value.Owner, coordinates);

            index += width + 5f;

            if (!TryComp<ShuttleComponent>(rootUid.Value.Owner, out var shuttleComp))
                continue;

            var gridDocks = _dockSystem.GetDocks(target.Value);
            var shuttleDocks = _dockSystem.GetDocks(rootUid.Value.Owner);
            var configs = _dockSystem.GetDockingConfigs(rootUid.Value.Owner, target.Value, shuttleDocks, gridDocks, entry.PriorityTag, ignored: false);

            DockingConfig? chosenConfig = null;
            int maxNewDocks = 0;
            foreach (var cfg in configs)
            {
                if (cfg.Docks.Any(pair => usedGridDocks.Contains(pair.DockBUid)))
                    continue;
                var newDocks = cfg.Docks.Count(pair => !usedGridDocks.Contains(pair.DockBUid));
                if (newDocks > maxNewDocks)
                {
                    maxNewDocks = newDocks;
                    chosenConfig = cfg;
                }
            }

            if (chosenConfig != null)
            {
                foreach (var pair in chosenConfig.Docks)
                {
                    usedGridDocks.Add(pair.DockBUid);
                }

                _shuttles.FTLToDockConfig(
                    rootUid.Value.Owner,
                    shuttleComp,
                    chosenConfig,
                    0f,
                    30f,
                    priorityTag: entry.PriorityTag,
                    ignored: false);
            }
            else
            {
                if (_shuttles.TryGetSunriseFtlProximity(rootUid.Value.Owner, new EntityCoordinates(target.Value, Vector2.Zero), out var coords, out var targAngle))
                {
                    _shuttles.FTLToCoordinates(rootUid.Value.Owner,
                        shuttleComp,
                        coords,
                        targAngle,
                        0f,
                        30f);
                }
            }
        }
    }
}
