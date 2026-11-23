using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem
{
    private void AddSunriseStationFallbackPositions(PlayerSpawningEvent args, List<EntityCoordinates> possiblePositions)
    {
        if (possiblePositions.Count > 0 || args.DesiredSpawnPointType == SpawnPointType.Observer)
            return;

        if (args.Station is not { } station)
            return;

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (spawnPoint.SpawnType != SpawnPointType.LateJoin || _stationSystem.GetOwningStation(uid, xform) != station)
                continue;

            possiblePositions.Add(xform.Coordinates);
        }

        if (possiblePositions.Count > 0)
            Log.Error($"No spawn point for job {args.Job} on station {ToPrettyString(station)}, using station fallback spawn point. RunLevel: {_gameTicker.RunLevel}");
    }
}
