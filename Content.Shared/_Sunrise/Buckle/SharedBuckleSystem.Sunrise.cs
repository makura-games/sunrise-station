using System.Numerics;
using Content.Shared.Buckle.Components;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private const float BuckleOffsetToleranceSquared = 1e-5f;

    private static bool IsBuckleOffsetValid(StrapComponent component, Vector2 position)
    {
        if (component.BuckleOffsets.Count == 0)
            return (position - component.BuckleOffset).LengthSquared() <= BuckleOffsetToleranceSquared;

        foreach (var offset in component.BuckleOffsets)
        {
            if ((position - offset).LengthSquared() <= BuckleOffsetToleranceSquared)
                return true;
        }

        return false;
    }

    private static Vector2 AssignBuckleOffset(EntityUid buckle, StrapComponent component)
    {
        var offset = component.BuckleOffset;

        if (component.BuckleOffsets.Count != 0)
        {
            offset = Vector2.Zero;

            foreach (var candidate in component.BuckleOffsets)
            {
                if (component.CurrentOffsets.ContainsValue(candidate))
                    continue;

                offset = candidate;
                break;
            }
        }

        component.CurrentOffsets[buckle] = offset;
        return offset;
    }

    private static Vector2 GetAssignedBuckleOffset(EntityUid buckle, StrapComponent component)
    {
        if (component.CurrentOffsets.TryGetValue(buckle, out var offset))
            return offset;

        return component.BuckleOffsets.Count == 0
            ? component.BuckleOffset
            : Vector2.Zero;
    }

    private static void RemoveAssignedBuckleOffset(EntityUid buckle, StrapComponent component)
    {
        component.CurrentOffsets.Remove(buckle);
    }
}
