using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Sheetlets.SciFiStyle;

/// <summary>
///     Sci-fi shine sweep button variant clipped to a horizontal capsule shape.
/// </summary>
[Virtual]
public class SciFiPillSweepButton : SciFiSweepButton
{
    private const int CapsuleArcSegments = 8;
    private const int MinimumPolygonVertices = 3;
    private const int MaxClipVertices = 64;
    private const float Half = 0.5f;
    private const float ClipEdgeEpsilon = 0.01f;
    private const float ParallelLineEpsilon = 0.0001f;

    private readonly Vector2[] _surfaceVertices = new Vector2[SurfaceFacetVertexCount];
    private readonly Vector2[] _clipInputVertices = new Vector2[MaxClipVertices];
    private readonly Vector2[] _clipOutputVertices = new Vector2[MaxClipVertices];
    private readonly Vector2[] _capsuleVertices = new Vector2[MaxClipVertices];

    protected override void DrawSurfaceFacet(
        DrawingHandleScreen handle,
        UIBox2 box,
        float leftNorm,
        float rightNorm,
        float skewNorm,
        Color color)
    {
        PopulateSurfaceFacetVertices(_surfaceVertices, box, leftNorm, rightNorm, skewNorm);
        DrawCapsuleClippedFacet(handle, box, color);
    }

    private void DrawCapsuleClippedFacet(DrawingHandleScreen handle, UIBox2 box, Color color)
    {
        var clipCount = BuildCapsuleClipPolygon(box);
        if (clipCount < MinimumPolygonVertices)
            return;

        for (var i = 0; i < _surfaceVertices.Length; i++)
            _clipInputVertices[i] = _surfaceVertices[i];

        var input = _clipInputVertices;
        var output = _clipOutputVertices;
        var inputCount = _surfaceVertices.Length;

        // Clip the shine facet against every edge of the convex capsule.
        // This is the Sutherland-Hodgman algorithm: after each edge, the output
        // polygon becomes the input polygon for the next clip edge.
        for (var i = 0; i < clipCount; i++)
        {
            var clipStart = _capsuleVertices[i];
            var clipEnd = _capsuleVertices[(i + 1) % clipCount];
            var outputCount = ClipPolygonAgainstEdge(input, inputCount, output, clipStart, clipEnd);

            if (outputCount < MinimumPolygonVertices)
                return;

            (input, output) = (output, input);
            inputCount = outputCount;
        }

        if (inputCount < MinimumPolygonVertices)
            return;

        handle.DrawPrimitives(
            DrawPrimitiveTopology.TriangleFan,
            new ReadOnlySpan<Vector2>(input, 0, inputCount),
            color);
    }

    private int BuildCapsuleClipPolygon(UIBox2 box)
    {
        var radius = Math.Min(box.Width, box.Height) * Half;
        if (radius <= 0f)
            return 0;

        var centerY = (box.Top + box.Bottom) * Half;
        var leftCenterX = box.Left + radius;
        var rightCenterX = box.Right - radius;
        var count = 0;

        // Build the convex capsule polygon clockwise in screen coordinates
        // where the Y axis points down: top edge, right arc, bottom edge, left arc.
        // Keeping this winding consistent is required by IsInsideClipEdge.
        count = AddClipVertex(_capsuleVertices, count, new Vector2(leftCenterX, box.Top));
        count = AddClipVertex(_capsuleVertices, count, new Vector2(rightCenterX, box.Top));

        for (var i = 1; i <= CapsuleArcSegments; i++)
        {
            var angle = -MathF.PI * Half + MathF.PI * i / CapsuleArcSegments;
            count = AddClipVertex(
                _capsuleVertices,
                count,
                new Vector2(
                    rightCenterX + MathF.Cos(angle) * radius,
                    centerY + MathF.Sin(angle) * radius));
        }

        count = AddClipVertex(_capsuleVertices, count, new Vector2(leftCenterX, box.Bottom));

        for (var i = 1; i <= CapsuleArcSegments; i++)
        {
            var angle = MathF.PI * Half + MathF.PI * i / CapsuleArcSegments;
            count = AddClipVertex(
                _capsuleVertices,
                count,
                new Vector2(
                    leftCenterX + MathF.Cos(angle) * radius,
                    centerY + MathF.Sin(angle) * radius));
        }

        return count;
    }

    private static int ClipPolygonAgainstEdge(
        Vector2[] input,
        int inputCount,
        Vector2[] output,
        Vector2 clipStart,
        Vector2 clipEnd)
    {
        if (inputCount == 0)
            return 0;

        var outputCount = 0;
        var previous = input[inputCount - 1];
        var previousInside = IsInsideClipEdge(previous, clipStart, clipEnd);

        // One Sutherland-Hodgman step: test every edge of the current polygon,
        // previous -> current, against a single clipping boundary.
        for (var i = 0; i < inputCount; i++)
        {
            var current = input[i];
            var currentInside = IsInsideClipEdge(current, clipStart, clipEnd);

            outputCount = AddClippedSegmentVertices(
                output,
                outputCount,
                previous,
                previousInside,
                current,
                currentInside,
                clipStart,
                clipEnd);

            previous = current;
            previousInside = currentInside;
        }

        return outputCount;
    }

    private static int AddClippedSegmentVertices(
        Vector2[] output,
        int outputCount,
        Vector2 previous,
        bool previousInside,
        Vector2 current,
        bool currentInside,
        Vector2 clipStart,
        Vector2 clipEnd)
    {
        // The four standard segment clipping cases:
        // outside->outside adds nothing, inside->outside adds the intersection,
        // outside->inside adds the intersection and current point, inside->inside adds the current point.
        return (previousInside, currentInside) switch
        {
            (false, false) => outputCount,
            (true, false) => AddClipVertex(
                output,
                outputCount,
                GetLineIntersection(previous, current, clipStart, clipEnd)),
            (false, true) => AddClipVertex(
                output,
                AddClipVertex(
                    output,
                    outputCount,
                    GetLineIntersection(previous, current, clipStart, clipEnd)),
                current),
            (true, true) => AddClipVertex(output, outputCount, current)
        };
    }

    private static int AddClipVertex(Vector2[] vertices, int count, Vector2 vertex)
    {
        if (count >= vertices.Length)
            return count;

        vertices[count++] = vertex;
        return count;
    }

    private static bool IsInsideClipEdge(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd)
    {
        // The capsule polygon is visually clockwise, but screen-space Y points down.
        // Because of that, the inside half-plane for an edge corresponds to a
        // non-negative edge x point cross product.
        return Cross(edgeEnd - edgeStart, point - edgeStart) >= -ClipEdgeEpsilon;
    }

    private static Vector2 GetLineIntersection(Vector2 lineStart, Vector2 lineEnd, Vector2 clipStart, Vector2 clipEnd)
    {
        // The intersection is only needed when an input polygon edge crosses the
        // clipping boundary. Nearly parallel lines fall back to the endpoint to
        // avoid inflated coordinates from division by a very small value.
        var line = lineEnd - lineStart;
        var clip = clipEnd - clipStart;
        var denominator = Cross(line, clip);

        if (MathF.Abs(denominator) <= ParallelLineEpsilon)
            return lineEnd;

        var amount = Cross(clipStart - lineStart, clip) / denominator;
        return lineStart + line * amount;
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.X * right.Y - left.Y * right.X;
    }
}
