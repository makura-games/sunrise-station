using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private bool TryLoadSunriseGameMapGrid(
        MapId mapId,
        ResPath path,
        [NotNullWhen(true)] out Entity<MapGridComponent>? grid,
        DeserializationOptions options,
        Vector2 offset,
        Angle rotation)
    {
        if (_cfg.GetCVar(SunriseCCVars.GameMapUseUserData))
            return _loader.TryLoadGrid(mapId, path, out grid, options, offset, rotation);

        grid = null;
        var loadOptions = new MapLoadOptions
        {
            MergeMap = mapId,
            Offset = offset,
            Rotation = rotation,
            DeserializationOptions = options,
            ExpectedCategory = FileCategory.Grid,
        };

        if (!TryLoadSunriseGameMapContent(path, loadOptions, out var result))
            return false;

        if (result.Grids.Count == 1)
        {
            grid = result.Grids.Single();
            return true;
        }

        _loader.Delete(result);
        return false;
    }

    private bool TryLoadSunriseGameMap(
        ResPath path,
        [NotNullWhen(true)] out Entity<MapComponent>? map,
        [NotNullWhen(true)] out HashSet<Entity<MapGridComponent>>? grids,
        DeserializationOptions options,
        Vector2 offset,
        Angle rotation)
    {
        if (_cfg.GetCVar(SunriseCCVars.GameMapUseUserData))
            return _loader.TryLoadMap(path, out map, out grids, options, offset, rotation);

        map = null;
        grids = null;
        var loadOptions = new MapLoadOptions
        {
            Offset = offset,
            Rotation = rotation,
            DeserializationOptions = options,
            ExpectedCategory = FileCategory.Map,
        };

        if (!TryLoadSunriseGameMapContent(path, loadOptions, out var result))
            return false;

        if (result.Maps.Count == 1)
        {
            map = result.Maps.Single();
            grids = result.Grids;
            return true;
        }

        _loader.Delete(result);
        return false;
    }

    private bool TryLoadSunriseGameMapContent(
        ResPath path,
        MapLoadOptions options,
        [NotNullWhen(true)] out LoadResult? result)
    {
        result = null;
        path = path.ToRootedPath();
        if (!_resourceManager.TryContentFileRead(path, out var stream))
        {
            _sawmill.Error(
                $"Packaged game map {path} was not found while sunrise.game_map_use_user_data is disabled.");
            return false;
        }

        using (stream)
        {
            _sawmill.Info(
                $"Loading game map {path} from packaged content because sunrise.game_map_use_user_data is disabled.");
            return _loader.TryLoadGeneric(stream, path.ToString(), out result, options);
        }
    }
}
