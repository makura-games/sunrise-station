using Content.Shared.Interaction;
using Robust.Shared.Map;

namespace Content.Shared._Sunrise.Movement.Systems;

/// <summary>
/// Provides a reusable API for rotating an entity toward a target.
/// </summary>
public sealed class AutoRotateToTargetSystem : EntitySystem
{
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float MinimumDirectionLengthSquared = 0.01f;

    /// <summary>
    /// Rotates <paramref name="source"/> toward <paramref name="target"/> in world space.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the source reached the target angle or is already close enough to the target.
    /// </returns>
    public bool TryRotateToEntity(
        Entity<TransformComponent?> source,
        Entity<TransformComponent?> target,
        float frameTime,
        Angle tolerance = default,
        double rotationSpeed = float.MaxValue)
    {
        if (!Resolve(target, ref target.Comp))
            return false;

        var targetCoordinates = _transform.GetMapCoordinates((target.Owner, target.Comp));
        return TryRotateToCoordinates(source, targetCoordinates, frameTime, tolerance, rotationSpeed);
    }

    /// <summary>
    /// Rotates <paramref name="source"/> toward the specified world-space coordinates.
    /// </summary>
    public bool TryRotateToCoordinates(
        Entity<TransformComponent?> source,
        MapCoordinates target,
        float frameTime,
        Angle tolerance = default,
        double rotationSpeed = float.MaxValue)
    {
        if (!Resolve(source, ref source.Comp))
            return false;

        var sourceCoordinates = _transform.GetMapCoordinates((source.Owner, source.Comp));

        if (sourceCoordinates.MapId == MapId.Nullspace ||
            sourceCoordinates.MapId != target.MapId)
        {
            return false;
        }

        var direction = target.Position - sourceCoordinates.Position;
        if (direction.LengthSquared() <= MinimumDirectionLengthSquared)
            return true;

        return _rotate.TryRotateTo(
            source,
            Angle.FromWorldVec(direction),
            frameTime,
            tolerance,
            rotationSpeed,
            source.Comp);
    }
}
