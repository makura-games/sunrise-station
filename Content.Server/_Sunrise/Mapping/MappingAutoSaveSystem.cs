using Content.Server._Sunrise.Decals;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction.Commands;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Console;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Mapping;

/// <summary>
/// Runs mapper cleanup commands automatically right before maps are serialized.
/// </summary>
public sealed partial class MappingAutoSaveSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private WalledDecalRemovalSystem _walledDecal = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IConsoleHost _console = default!;

    private PendingAutoSaveConsoleContext? _pendingAutoSaveConsoleContext;

    private const string SaveMapCommandName = "savemap";
    private const string CleanDeviceLinkCommandName = "cleandevicelinks";
    private const string FixGridAtmosCommandName = "fixgridatmos";
    private const string TileWallsCommandName = "tilewalls";
    private const string RemoveWalledDecalsCommandName = "removewalleddecals";
    private const string VariantizeCommandName = "variantize";

    /// <summary>
    /// Hooks the system into save-command tracking and pre-serialization events.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        _console.AnyCommandExecuted += OnAnyCommandExecuted;
        SubscribeLocalEvent<BeforeSerializationEvent>(OnBeforeSerialization);
    }

    /// <summary>
    /// Detaches save-command tracking listeners.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        _console.AnyCommandExecuted -= OnAnyCommandExecuted;
    }

    /// <summary>
    /// Returns a comma-separated list of auto-save commands that will run for the specified map.
    /// </summary>
    public string? GetMapSaveAutoCommandsSummary(EntityUid mapUid)
    {
        if (!HasComp<MapComponent>(mapUid))
            return null;

        return GetEnabledAutoCommandsSummary();
    }

    private void OnAnyCommandExecuted(IConsoleShell shell, string name, string argStr, string[] args)
    {
        if (!string.Equals(name, SaveMapCommandName, StringComparison.OrdinalIgnoreCase))
            return;

        _pendingAutoSaveConsoleContext = null;

        var autoCommands = GetEnabledAutoCommandsSummary();
        if (autoCommands is null ||
            args.Length < 2 ||
            !int.TryParse(args[0], out var mapInt))
        {
            return;
        }

        var mapId = new MapId(mapInt);
        if (mapId == MapId.Nullspace ||
            !_map.MapExists(mapId) ||
            _map.IsInitialized(mapId) && (args.Length < 3 || !bool.TryParse(args[2], out var force) || !force))
        {
            return;
        }

        _pendingAutoSaveConsoleContext = new PendingAutoSaveConsoleContext(shell, mapId, _timing.CurTick, autoCommands);
    }

    private void OnBeforeSerialization(BeforeSerializationEvent ev)
    {
        foreach (var entity in ev.Entities)
        {
            if (!TryComp<MapComponent>(entity, out var map))
                continue;

            RunMapSaveAutoCommands(map);
            TryWritePendingAutoSaveConsoleMessage(map.MapId);
        }
    }

    private void RunMapSaveAutoCommands(MapComponent map)
    {
        var runCleanDeviceLinks = _cfg.GetCVar(SunriseCCVars.MappingAutoCleanDeviceLinks);
        var runFixGridAtmos = _cfg.GetCVar(SunriseCCVars.MappingAutoFixGridAtmos);
        var runTileWalls = _cfg.GetCVar(SunriseCCVars.MappingAutoTileWalls);
        var runRemoveWalledDecals = _cfg.GetCVar(SunriseCCVars.MappingAutoRemoveWalledDecals);
        var runVariantize = _cfg.GetCVar(SunriseCCVars.MappingAutoVariantize);

        if (!runCleanDeviceLinks && !runFixGridAtmos && !runTileWalls && !runRemoveWalledDecals && !runVariantize)
            return;

        if (runCleanDeviceLinks)
            _deviceLink.CleanupLinksForMapSave(map.MapId);

        foreach (var grid in _mapManager.GetAllGrids(map.MapId))
        {
            if (runFixGridAtmos)
                RunMapSaveAutoFixGridAtmos(grid);

            if (runTileWalls)
                RunMapSaveAutoTileWalls(grid);

            if (runRemoveWalledDecals)
                _walledDecal.RemoveWalledDecals(grid.Owner, grid.Comp);

            if (runVariantize)
                RunMapSaveAutoVariantize(grid);
        }
    }

    private string? GetEnabledAutoCommandsSummary()
    {
        List<string>? executedCommands = null;

        AddEnabledCommand(SunriseCCVars.MappingAutoCleanDeviceLinks, CleanDeviceLinkCommandName);
        AddEnabledCommand(SunriseCCVars.MappingAutoFixGridAtmos, FixGridAtmosCommandName);
        AddEnabledCommand(SunriseCCVars.MappingAutoTileWalls, TileWallsCommandName);
        AddEnabledCommand(SunriseCCVars.MappingAutoRemoveWalledDecals, RemoveWalledDecalsCommandName);
        AddEnabledCommand(SunriseCCVars.MappingAutoVariantize, VariantizeCommandName);

        return executedCommands is null
            ? null
            : string.Join(", ", executedCommands);

        void AddEnabledCommand(CVarDef<bool> cvar, string command)
        {
            if (!_cfg.GetCVar(cvar))
                return;

            executedCommands ??= new List<string>(3);
            executedCommands.Add(command);
        }
    }

    private void TryWritePendingAutoSaveConsoleMessage(MapId mapId)
    {
        if (_pendingAutoSaveConsoleContext is not { } pending)
            return;

        if (pending.Tick != _timing.CurTick)
        {
            _pendingAutoSaveConsoleContext = null;
            return;
        }

        if (pending.MapId != mapId)
            return;

        pending.Shell.WriteLine(Loc.GetString("mapping-save-auto-commands", ("commands", pending.AutoCommands)));
        _pendingAutoSaveConsoleContext = null;
    }

    private void RunMapSaveAutoFixGridAtmos(Entity<MapGridComponent> grid)
    {
        if (!TryComp<GridAtmosphereComponent>(grid, out var gridAtmosphere))
            return;

        _atmosphere.RebuildGridAtmosphere((grid.Owner, gridAtmosphere, grid.Comp));
    }

    private void RunMapSaveAutoTileWalls(Entity<MapGridComponent> grid)
    {
        if (HasComp<BiomeComponent>(grid))
            return;

        var underplating = _tileDefinition[TileWallsCommand.TilePrototypeId];
        var underplatingTile = new Tile(underplating.TileId);
        var childEnumerator = Transform(grid.Owner).ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (!TileWallProcessingHelper.IsEligibleWall(EntityManager, _tag, child, out var childTransform))
                continue;

            var tile = _map.GetTileRef(grid.Owner, grid.Comp, childTransform.Coordinates);
            var tileDefinition = (ContentTileDefinition) _tileDefinition[tile.Tile.TypeId];

            if (tileDefinition.ID == TileWallsCommand.TilePrototypeId)
                continue;

            _map.SetTile(grid.Owner, grid.Comp, childTransform.Coordinates, underplatingTile);
        }
    }

    private void RunMapSaveAutoVariantize(Entity<MapGridComponent> grid)
    {
        foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
        {
            var tileDefinition = _turf.GetContentTileDefinition(tile);
            var variantTile = new Tile(
                tile.Tile.TypeId,
                tile.Tile.Flags,
                _tile.PickVariant(tileDefinition),
                tile.Tile.RotationMirroring);

            _map.SetTile(grid.Owner, grid.Comp, tile.GridIndices, variantTile);
        }
    }

    private sealed record PendingAutoSaveConsoleContext(
        IConsoleShell Shell,
        MapId MapId,
        GameTick Tick,
        string AutoCommands);
}
