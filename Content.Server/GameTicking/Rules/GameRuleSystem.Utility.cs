using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public abstract partial class GameRuleSystem<T> where T: IComponent
{
    protected EntityQueryEnumerator<ActiveGameRuleComponent, T, GameRuleComponent> QueryActiveRules()
    {
        return EntityQueryEnumerator<ActiveGameRuleComponent, T, GameRuleComponent>();
    }

    protected EntityQueryEnumerator<DelayedStartRuleComponent, T, GameRuleComponent> QueryDelayedRules()
    {
        return EntityQueryEnumerator<DelayedStartRuleComponent, T, GameRuleComponent>();
    }

    /// <summary>
    /// Queries all gamerules, regardless of if they're active or not.
    /// </summary>
    protected EntityQueryEnumerator<T, GameRuleComponent> QueryAllRules()
    {
        return EntityQueryEnumerator<T, GameRuleComponent>();
    }

    /// <summary>
    ///     Utility function for finding a random event-eligible station entity
    /// </summary>
    protected bool TryGetRandomStation([NotNullWhen(true)] out EntityUid? station, Func<EntityUid, bool>? filter = null)
    {
        var stations = new ValueList<EntityUid>(Count<StationEventEligibleComponent>());

        filter ??= _ => true;
        var query = AllEntityQuery<StationEventEligibleComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (!filter(uid))
                continue;

            stations.Add(uid);
        }

        if (stations.Count == 0)
        {
            station = null;
            return false;
        }

        // TODO: Engine PR.
        station = stations[RobustRandom.Next(stations.Count)];
        return true;
    }

    protected bool TryFindRandomTile(out Vector2i tile,
        [NotNullWhen(true)] out EntityUid? targetStation,
        out EntityUid targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetStation = EntityUid.Invalid;
        targetGrid = EntityUid.Invalid;
        targetCoords = EntityCoordinates.Invalid;
        if (TryGetRandomStation(out targetStation))
        {
            return TryFindRandomTileOnStation((targetStation.Value, Comp<StationDataComponent>(targetStation.Value)),
                out tile,
                out targetGrid,
                out targetCoords);
        }

        return false;
    }

    // Sunrise edit start - порт исправленного выбора только среди реально заполненных тайлов из Wizden.
    protected bool TryFindRandomTileOnStation(Entity<StationDataComponent> station,
        out Vector2i tile,
        out EntityUid targetGrid,
        out EntityCoordinates targetCoords,
        int numAttempts = 10)
    {
        tile = default;
        targetCoords = EntityCoordinates.Invalid;
        targetGrid = EntityUid.Invalid;

        var totalTiles = 0;
        var grids = new List<(Entity<MapGridComponent> Entity, int Count, List<TileRef> Tiles)>();
        foreach (var possibleTarget in station.Comp.Grids)
        {
            if (!TryComp<MapGridComponent>(possibleTarget, out var comp))
                continue;

            var tiles = _map.GetAllTiles(possibleTarget, comp).ToList();
            if (tiles.Count == 0)
                continue;

            grids.Add(((possibleTarget, comp), tiles.Count, tiles));
            totalTiles += tiles.Count;
        }

        if (grids.Count == 0)
        {
            targetGrid = EntityUid.Invalid;
            return false;
        }

        for (var i = 0; i < numAttempts && totalTiles > 0; i++)
        {
            var nextTileIndex = RobustRandom.Next(totalTiles);
            TileRef? randomTileRef = null;
            MapGridComponent gridComp = default!;
            var startIndex = 0;
            for (var j = 0; j < grids.Count; j++)
            {
                var grid = grids[j];
                if (nextTileIndex >= startIndex + grid.Count)
                {
                    startIndex += grid.Count;
                    continue;
                }

                (targetGrid, gridComp) = grid.Entity;
                var gridTileIndex = nextTileIndex - startIndex;
                randomTileRef = grid.Tiles[gridTileIndex];
                grid.Tiles.RemoveSwap(gridTileIndex);
                grid.Count--;
                totalTiles--;

                if (grid.Count == 0)
                    grids.RemoveSwap(j);
                else
                    grids[j] = grid;

                break;
            }

            if (randomTileRef is not { } tileRef)
                return false;

            if (_atmosphere.IsTileSpace(targetGrid, Transform(targetGrid).MapUid, tileRef.GridIndices)
                || _atmosphere.IsTileAirBlockedCached(targetGrid, tileRef.GridIndices))
                continue;

            tile = tileRef.GridIndices;
            targetCoords = _map.GridTileToLocal(targetGrid, gridComp, tile);
            return true;
        }

        return false;
    }
    // Sunrise edit end

    protected void ForceEndSelf(EntityUid uid, GameRuleComponent? component = null)
    {
        GameTicker.EndGameRule(uid, component);
    }
}
