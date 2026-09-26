using Content.Server.Construction.Commands;
using Content.Shared.Tag;
using Content.Shared.Wall;

namespace Content.Server._Sunrise.Mapping;

public static class TileWallProcessingHelper
{
    public static bool IsEligibleWall(
        IEntityManager entityManager,
        TagSystem tagSystem,
        EntityUid uid,
        out TransformComponent transform)
    {
        transform = default!;

        if (!entityManager.EntityExists(uid) ||
            !entityManager.HasComponent<WallComponent>(uid) ||
            tagSystem.HasTag(uid, TileWallsCommand.ForceNoTileWallsTag) ||
            tagSystem.HasTag(uid, TileWallsCommand.DiagonalTag) ||
            !entityManager.TryGetComponent(uid, out TransformComponent? wallTransform))
        {
            return false;
        }

        transform = wallTransform;
        return transform.Anchored;
    }
}
