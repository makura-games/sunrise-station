using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Parallax;
using Content.Server.Shuttles.Systems;
using Content.Shared._Sunrise.Shuttles;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.PlanetPrison;

// TODO: Рефактор с целью устранения варнингов и перехода системы на более современное API
public sealed partial class PlanetPrisonStationSystem : EntitySystem
{
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private BiomeSystem _biomeSystem = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlanetPrisonStationComponent, ComponentInit>(OnPlanetPrisonStationInit);
        SubscribeLocalEvent<PlanetPrisonStationComponent, ComponentShutdown>(OnPrisonShutdown);

        Log.Level = LogLevel.Info;
    }

    private void OnPrisonShutdown(EntityUid uid, PlanetPrisonStationComponent component, ComponentShutdown args)
    {
        QueueDel(component.Entity);
        component.Entity = EntityUid.Invalid;

        if (_mapManager.MapExists(component.MapId))
            _mapManager.DeleteMap(component.MapId);

        component.MapId = MapId.Nullspace;
    }

    private void OnPlanetPrisonStationInit(EntityUid uid, PlanetPrisonStationComponent component, ComponentInit args)
    {
        var enable = _cfg.GetCVar(SunriseCCVars.MinPlayersEnable);
        if (!enable)
            return;

        var minPlayers = _cfg.GetCVar(SunriseCCVars.MinPlayersPlanetPrison);
        if (_player.PlayerCount <= minPlayers)
        {
            _chat.DispatchServerAnnouncement(Loc.GetString("planet-prison-not-enough-players", ("minimumPlayers", minPlayers)), Color.OrangeRed);
            return;
        }

        if (component.MapId != MapId.Nullspace)
            return;

        AddPlanetPrison(component);
    }

    private void AddPlanetPrison(PlanetPrisonStationComponent component)
    {
        var query = AllEntityQuery<PlanetPrisonStationComponent>();

        while (query.MoveNext(out var otherComp))
        {
            if (otherComp == component)
                continue;

            component.MapId = otherComp.MapId;
            return;
        }

        var station = ChooseType(component);

        if (!_protoManager.TryIndex(_random.Pick(component.Biomes), out var biome))
        {
            Log.Warning("No Prison map found, skipping setup.");
            return;
        }

        if (!_protoManager.TryIndex(station, out var gameMap))
        {
            Log.Warning("No Prison map found, skipping setup.");
            return;
        }

        _chat.DispatchServerAnnouncement(Loc.GetString("planet-prison-select-map", ("stationName", gameMap.MapName)), Color.LightBlue);
        _chat.DispatchServerAnnouncement(Loc.GetString("planet-prison-select-biome", ("biomeName", biome.ID)), Color.LightBlue);

        var opts = DeserializationOptions.Default with {InitializeMaps = true};
        var uids = _gameTicker.LoadGameMap(gameMap, out var mapId, opts, rot: Angle.Zero);

        component.MapId = mapId;

        if (uids.Count != 1)
        {
            Log.Warning("Prison station have more 1 grid.");
            QueueDel(component.Entity);
            component.Entity = EntityUid.Invalid;

            if (_mapManager.MapExists(component.MapId))
                _mapManager.DeleteMap(component.MapId);

            component.MapId = MapId.Nullspace;
            return;
        }

        EnsureComp<IgnoreFtlCheckComponent>(uids[0]);
        component.PrisonGrid = uids[0];

        var mapUid = _mapManager.GetMapEntityId(mapId);
        _biomeSystem.EnsurePlanet(mapUid, biome);

        var restricted = new RestrictedRangeComponent
        {
            Origin = new Vector2(0, 0),
            Range = 200,
        };
        AddComp(mapUid, restricted);

        EnsureComp<LightCycleComponent>(mapUid);

        var destComp = _entManager.EnsureComponent<FTLDestinationComponent>(mapUid);
        destComp.BeaconsOnly = true;
        _shuttle.SetFTLWhitelist(mapUid, component.ShuttleWhitelist);
    }

    private ProtoId<GameMapPrototype> ChooseType(PlanetPrisonStationComponent component)
    {
        return _cfg.GetCVar(SunriseCCVars.PlanetPrisonModern)
            ? _random.Pick(component.StationsModern)
            : _random.Pick(component.StationsOld);
    }
}
