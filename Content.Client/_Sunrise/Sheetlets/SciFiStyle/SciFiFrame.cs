using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Sheetlets.SciFiStyle;

public sealed class SciFiFrame : Control
{
    /// <summary>
    /// Grid size scale
    /// </summary>
    private const int StepScale = 56;
    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0)
            return;

        var cut = SunriseStyleClass.SciFiFramePatchMargin * UIScale;
        DrawGrid(handle, width, height, cut, UIScale);
    }

    private static void DrawGrid(DrawingHandleScreen handle, float width, float height, float cut, float scale)
    {
        var step = StepScale * scale;
        var color = SciFiPalette.BorderDim.WithAlpha(0.22f);

        for (var x = cut; x < width - cut; x += step)
            handle.DrawLine(new Vector2(x, cut), new Vector2(x, height - cut), color);

        for (var y = cut; y < height - cut; y += step)
            handle.DrawLine(new Vector2(cut, y), new Vector2(width - cut, y), color);
    }
}
