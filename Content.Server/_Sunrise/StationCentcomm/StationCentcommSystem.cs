using Content.Server._Sunrise.StationCentComm;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Shuttles.Systems;
using Content.Shared._Sunrise.AlwaysPoweredMap;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationCentComm;

public sealed partial class StationCentCommSystem : EntitySystem
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private MapSystem _map = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("station.centcomm");
        SubscribeLocalEvent<StationCentCommComponent, ComponentShutdown>(OnCentcommShutdown);
        SubscribeLocalEvent<StationCentCommComponent, ComponentInit>(OnCentcommInit);
    }

    private void OnCentcommShutdown(EntityUid uid, StationCentCommComponent component, ComponentShutdown args)
    {
        QueueDel(component.Entity);
        component.Entity = EntityUid.Invalid;

        if (_mapManager.MapExists(component.MapId))
            _mapManager.DeleteMap(component.MapId);

        component.MapId = MapId.Nullspace;
    }

    private void OnCentcommInit(EntityUid uid, StationCentCommComponent component, ComponentInit args)
    {
        // Post mapinit? fancy
        if (TryComp(component.Entity, out TransformComponent? xform))
        {
            component.MapId = xform.MapID;
            return;
        }

        AddCentcomm(component);
    }

    private void AddCentcomm(StationCentCommComponent component)
    {
        var query = AllEntityQuery<StationCentCommComponent>();

        while (query.MoveNext(out var otherComp))
        {
            if (otherComp == component)
                continue;

            component.MapId = otherComp.MapId;
            return;
        }

        if (component.Station != null)
        {
            if (_prototypeManager.TryIndex<GameMapPrototype>(component.Station, out var gameMap))
            {
                _gameTicker.LoadGameMap(gameMap, out var mapId);
                component.MapId = mapId;

                var mapEnt = _map.GetMapOrInvalid(mapId);

                if (_shuttle.TryAddFTLDestination(mapId, true, out var ftlDestination))
                    ftlDestination.Whitelist = component.ShuttleWhitelist;

                EnsureComp<AlwaysPoweredMapComponent>(mapEnt);

                _map.InitializeMap(mapId);
            }
            else
            {
                _sawmill.Warning("No Centcomm map found, skipping setup.");
            }
        }
    }
}
