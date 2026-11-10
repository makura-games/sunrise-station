using System.Numerics;
using Content.Server._Sunrise.Mapping;
using Content.Server.Decals;
using Content.Shared.Decals;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.Decals;

/// <summary>
/// Removes decals that now overlap wall tiles on a grid.
/// </summary>
public sealed partial class WalledDecalRemovalSystem : EntitySystem
{
    [Dependency] private DecalSystem _decal = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TagSystem _tag = default!;

    /// <summary>
    /// Removes decals that overlap wall tiles on the specified grid.
    /// </summary>
    public int RemoveWalledDecals(EntityUid gridUid, MapGridComponent? grid = null)
    {
        if (!Resolve(gridUid, ref grid))
            return 0;

        return RemoveWalledDecals((gridUid, grid));
    }

    /// <summary>
    /// Removes decals that overlap wall tiles on the specified grid.
    /// </summary>
    public int RemoveWalledDecals(Entity<MapGridComponent> grid)
    {
        var wallTiles = new HashSet<Vector2i>();
        var decalsToRemove = new HashSet<DecalIndex>();
        var childEnumerator = Transform(grid.Owner).ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (!TileWallProcessingHelper.IsEligibleWall(EntityManager, _tag, child, out var childTransform))
                continue;

            wallTiles.Add(_map.GetTileRef(grid.Owner, grid.Comp, childTransform.Coordinates).GridIndices);
        }

        if (wallTiles.Count == 0)
            return 0;

        var half = new Vector2(0.5f, 0.5f);

        foreach (var wallTile in wallTiles)
        {
            // Decal.Coordinates - левый нижний угол декали, а GetDecalsIntersecting проверяет замкнутый Box2.
            // Бокс самого тайла захватывал декали соседних тайлов (справа/сверху) на его границе,
            // поэтому определяем тайл декали по её центру и сравниваем точно.
            var tilePosition = (Vector2) wallTile;
            var bounds = new Box2(tilePosition - half, tilePosition + half);

            foreach (var (decalId, decal) in _decal.GetDecalsIntersecting(grid.Owner, bounds))
            {
                if ((decal.Coordinates + half).Floored() != wallTile)
                    continue;

                decalsToRemove.Add(decalId);
            }
        }

        var removed = 0;
        foreach (var decalId in decalsToRemove)
        {
            if (_decal.RemoveDecal(grid.Owner, decalId))
                removed++;
        }

        return removed;
    }
}
