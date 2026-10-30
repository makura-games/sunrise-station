using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    // Sunrise-Edit - формы radar blips и линии laser beam
    private void DrawSunriseRadarOverlays(
        DrawingHandleScreen handle,
        MapCoordinates mapPos,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView)
    {
        // Draw radar blips (shaped markers) for large projectiles etc.
        var blipWorldToView = worldToShuttle * shuttleToView;
        foreach (var blip in _blips)
        {
            var blipCoords = EntManager.GetCoordinates(blip.Coordinates);
            var blipMapCoords = _transform.ToMapCoordinates(blipCoords);
            if (blipMapCoords.MapId != mapPos.MapId)
                continue;
            if ((blipMapCoords.Position - mapPos.Position).LengthSquared() > WorldRange * WorldRange)
                continue;

            var blipScreen = Vector2.Transform(blipMapCoords.Position, blipWorldToView);
            switch (blip.Shape)
            {
                case BlipShape.Circle:
                    DrawBlipCircle(handle, blipScreen, blip.Color, blip.Scale);
                    break;
                case BlipShape.Square:
                    DrawBlipSquare(handle, blipScreen, blip.Color, blip.Scale);
                    break;
                default:
                    DrawBlipTriangle(handle, blipScreen, blip.Color, blip.Scale);
                    break;
            }
        }

        // Draw transient laser beam lines (e.g. Apollo hitscan shots).
        foreach (var laser in _lasers)
        {
            var originCoords = EntManager.GetCoordinates(laser.Origin);
            var originMapCoords = _transform.ToMapCoordinates(originCoords);
            if (originMapCoords.MapId != mapPos.MapId)
                continue;
            if ((originMapCoords.Position - mapPos.Position).LengthSquared() > WorldRange * WorldRange)
                continue;

            var originScreen = Vector2.Transform(originMapCoords.Position, blipWorldToView);
            var endMapPos = originMapCoords.Position + laser.Direction * laser.Length;
            var endScreen = Vector2.Transform(endMapPos, blipWorldToView);
            handle.DrawLine(originScreen, endScreen, laser.Color.WithAlpha(0.9f));
            // Draw a slightly dimmer, wider glow line for visibility.
            handle.DrawLine(originScreen, endScreen, laser.Color.WithAlpha(0.35f));
        }
    }

    /// <summary>
    /// Draws a filled triangle blip on the radar screen, pointing upward.
    /// </summary>
    private static void DrawBlipTriangle(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        const float BaseSize = 7f;
        var s = BaseSize * scale;
        var v0 = center + new Vector2(0f, -s);               // tip (pointing up / forward)
        var v1 = center + new Vector2(-s * 0.65f, s * 0.5f); // bottom-left
        var v2 = center + new Vector2(s * 0.65f, s * 0.5f);  // bottom-right
        var verts = new[] { v0, v1, v2 };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, color.WithAlpha(0.85f));

        // Outline
        var outline = new[] { v0, v1, v2, v0 };
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, outline, color);
    }

    /// <summary>
    /// Draws a filled circle blip on the radar screen (used for rockets).
    /// </summary>
    private static void DrawBlipCircle(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        const float BaseRadius = 5f;
        var r = BaseRadius * scale;
        handle.DrawCircle(center, r, color.WithAlpha(0.85f));
        handle.DrawCircle(center, r, color, false); // outline
    }

    /// <summary>
    /// Draws a filled axis-aligned square blip on the radar screen (used for shells).
    /// </summary>
    private static void DrawBlipSquare(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        const float BaseHalf = 5f;
        var h = BaseHalf * scale;
        var tl = center + new Vector2(-h, -h);
        var tr = center + new Vector2(h, -h);
        var br = center + new Vector2(h, h);
        var bl = center + new Vector2(-h, h);
        var verts = new[] { tl, tr, br, bl };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, color.WithAlpha(0.85f));
        var outline = new[] { tl, tr, br, bl, tl };
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, outline, color);
    }
}
