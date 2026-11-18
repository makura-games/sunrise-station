using System;
using System.Numerics;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Sponsor;

public sealed class SponsorWindowOpenAnimationOverlay : Control
{
    private const float MinimumProgress = 0f;
    private const float MaximumProgress = 1f;
    private const float HalfSizeFactor = 0.5f;
    private const float FadeStartProgress = 0.76f;
    private const float FadeDuration = MaximumProgress - FadeStartProgress;
    private const float BackgroundAlpha = 0.07f;
    private const float MinimumPanelTraceHeight = 8f;
    private const float MinimumCoreGlowHalfHeight = 2f;
    private const float CoreGlowHalfHeight = 5f;
    private const float CoreGlowAlpha = 0.10f;
    private const float CoreEdgeLineAlpha = 0.55f;
    private const float CoreMainLineAlpha = 0.92f;
    private const float TickAnimationDuration = 0.32f;
    private const float MinimumTickInset = 10f;
    private const float TickLineHalfHeight = 9f;
    private const float TickLineAlpha = 0.75f;
    private const float PanelEdgeAlpha = 0.44f;
    private const float PanelDimEdgeAlpha = 0.36f;
    private const float PanelScanAlpha = 0.22f;

    public float Progress { get; set; }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0)
            return;

        var alpha = MaximumProgress - ClampProgress((Progress - FadeStartProgress) / FadeDuration);
        if (alpha <= 0f)
            return;

        var size = new Vector2(width, height);
        var fullBox = UIBox2.FromDimensions(Vector2.Zero, size);
        handle.DrawRect(fullBox, SciFiPalette.AccentSoft.WithAlpha(BackgroundAlpha * alpha));

        DrawCoreLine(handle, width, height, alpha);

        if (height < MinimumPanelTraceHeight * UIScale)
            return;

        DrawPanelTrace(handle, width, height, alpha);
    }

    private void DrawCoreLine(DrawingHandleScreen handle, float width, float height, float alpha)
    {
        var centerY = height * HalfSizeFactor;
        var glowHeight = MathF.Max(MinimumCoreGlowHalfHeight, CoreGlowHalfHeight * UIScale);
        var glowBox = new UIBox2(0f, centerY - glowHeight, width, centerY + glowHeight);
        handle.DrawRect(glowBox, SciFiPalette.Accent.WithAlpha(CoreGlowAlpha * alpha));

        DrawHorizontalLine(handle, 0f, width, centerY - UIScale, SciFiPalette.AccentDim.WithAlpha(CoreEdgeLineAlpha * alpha));
        DrawHorizontalLine(handle, 0f, width, centerY, SciFiPalette.Accent.WithAlpha(CoreMainLineAlpha * alpha));
        DrawHorizontalLine(handle, 0f, width, centerY + UIScale, SciFiPalette.AccentDim.WithAlpha(CoreEdgeLineAlpha * alpha));

        var tickProgress = Easings.OutQuad(ClampProgress(Progress / TickAnimationDuration));
        var inset = MathF.Max(MinimumTickInset * UIScale, width * (MaximumProgress - tickProgress) * HalfSizeFactor);
        var tickTop = centerY - TickLineHalfHeight * UIScale;
        var tickBottom = centerY + TickLineHalfHeight * UIScale;
        var tickColor = SciFiPalette.Accent.WithAlpha(TickLineAlpha * alpha);
        DrawVerticalLine(handle, inset, tickTop, tickBottom, tickColor);
        DrawVerticalLine(handle, width - inset, tickTop, tickBottom, tickColor);
    }

    private void DrawPanelTrace(DrawingHandleScreen handle, float width, float height, float alpha)
    {
        var edge = SciFiPalette.Accent.WithAlpha(PanelEdgeAlpha * alpha);
        var dim = SciFiPalette.BorderDim.WithAlpha(PanelDimEdgeAlpha * alpha);

        DrawHorizontalLine(handle, 0f, width, 0f, edge);
        DrawHorizontalLine(handle, 0f, width, height - UIScale, edge);
        DrawVerticalLine(handle, 0f, 0f, height, dim);
        DrawVerticalLine(handle, width - UIScale, 0f, height, dim);

        var scanY = Math.Clamp(height * Easings.OutSine(ClampProgress(Progress)), 0f, height);
        DrawHorizontalLine(handle, 0f, width, scanY, SciFiPalette.Accent.WithAlpha(PanelScanAlpha * alpha));
    }

    private static float ClampProgress(float progress)
    {
        return Math.Clamp(progress, MinimumProgress, MaximumProgress);
    }

    private static void DrawHorizontalLine(DrawingHandleScreen handle, float left, float right, float y, Color color)
    {
        handle.DrawLine(new Vector2(left, y), new Vector2(right, y), color);
    }

    private static void DrawVerticalLine(DrawingHandleScreen handle, float x, float top, float bottom, Color color)
    {
        handle.DrawLine(new Vector2(x, top), new Vector2(x, bottom), color);
    }
}
