using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Client._Sunrise.Sheetlets.SciFiStyle;

/// <summary>
///     Button with a one-shot sci-fi shine sweep overlay.
/// </summary>
/// <remarks>
///     The regular <see cref="Button"/> style box defines the button shape and background. This control only draws
///     the animated sweep overlay, similar to a CSS button pseudo-element moving across the button surface.
/// </remarks>
[Virtual]
public class SciFiSweepButton : Button
{
    protected const int SurfaceFacetVertexCount = 4;

    private const float SurfaceInset = 2f;
    private const float SweepSkew = 0.34f;
    private const float SweepWidth = 0.16f;
    private const float SweepTravelPadding = 0.58f;
    private const float MaxPrimitiveAlpha = 0.68f;
    private const float DefaultSweepDuration = 0.7f;
    private const float MinimumSweepDuration = 0.05f;
    private const float PressedIntensityMultiplier = 1.12f;

    private static readonly SweepBand[] SweepBands =
    {
        new(-2.6f, -1.05f, 0.08f, SweepBandTone.Outer),
        new(-1.05f, -0.25f, 0.18f, SweepBandTone.Accent),
        new(-0.25f, 0.32f, 0.34f, SweepBandTone.Core),
        new(0.32f, 1.1f, 0.16f, SweepBandTone.Accent),
        new(1.1f, 2.7f, 0.07f, SweepBandTone.Outer),
    };

    private readonly Vector2[] _surfaceVertices = new Vector2[SurfaceFacetVertexCount];
    private float _sweepDuration = DefaultSweepDuration;
    private float _sweepIntensity = 1f;
    private float _sweepProgress;
    private bool _wasSweeping;

    /// <summary>
    ///     Enables the animated shine sweep overlay.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool SweepEnabled { get; set; } = true;

    /// <summary>
    ///     Time, in seconds, for the sweep to cross the button once.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float SweepDuration
    {
        get => _sweepDuration;
        set => _sweepDuration = Math.Max(MinimumSweepDuration, value);
    }

    /// <summary>
    ///     Multiplier for the sweep opacity.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float SweepIntensity
    {
        get => _sweepIntensity;
        set => _sweepIntensity = Math.Max(0f, value);
    }

    // FrameUpdate хранит время анимации отдельно от Draw: Draw не получает delta time и должен только рисовать
    // текущий кадр, иначе скорость sweep начнет зависеть от частоты перерисовки UI.
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!CanAnimateSweep())
        {
            _wasSweeping = false;
            return;
        }

        if (!_wasSweeping)
        {
            _sweepProgress = 0f;
            _wasSweeping = true;
            return;
        }

        if (_sweepProgress >= 1f)
            return;

        _sweepProgress = Math.Min(_sweepProgress + args.DeltaSeconds / SweepDuration, 1f);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        ResetSweep();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!CanDrawSweep())
            return;

        if (!TryGetSweepBox(out var sweepBox))
            return;

        DrawSweep(handle, sweepBox, GetSweepIntensity());
    }

    /// <summary>
    ///     Resets the one-shot sweep animation so the next visible state starts from the beginning.
    /// </summary>
    protected virtual void ResetSweep()
    {
        _sweepProgress = 0f;
        _wasSweeping = false;
    }

    /// <summary>
    ///     Returns the sweep opacity multiplier for the current draw mode.
    /// </summary>
    protected virtual float GetSweepIntensity()
    {
        if (DrawMode == DrawModeEnum.Disabled)
            return 0f;

        var stateIntensity = DrawMode switch
        {
            DrawModeEnum.Pressed => PressedIntensityMultiplier,
            DrawModeEnum.Hover => 1f,
            _ => 0f
        };

        return stateIntensity * SweepIntensity;
    }

    /// <summary>
    ///     Computes the inset surface box used by the sweep overlay.
    /// </summary>
    protected virtual bool TryGetSweepBox(out UIBox2 box)
    {
        var pixelBox = PixelSizeBox;
        if (pixelBox.Width <= SurfaceInset * 2f || pixelBox.Height <= SurfaceInset * 2f)
        {
            box = default;
            return false;
        }

        box = new UIBox2(
            pixelBox.Left + SurfaceInset,
            pixelBox.Top + SurfaceInset,
            pixelBox.Right - SurfaceInset,
            pixelBox.Bottom - SurfaceInset);
        return true;
    }

    /// <summary>
    ///     Draws the animated sweep bands for the current sweep progress.
    /// </summary>
    protected virtual void DrawSweep(DrawingHandleScreen handle, UIBox2 box, float intensity)
    {
        var progress = EaseOutCubic(_sweepProgress);
        var center = Lerp(-SweepTravelPadding, 1f + SweepTravelPadding, progress);

        foreach (var band in SweepBands)
        {
            DrawSurfaceFacet(
                handle,
                box,
                center + SweepWidth * band.LeftOffset,
                center + SweepWidth * band.RightOffset,
                SweepSkew,
                GetSweepBandColor(band.Tone).WithAlpha(ClampAlpha(band.Alpha * intensity)));
        }
    }

    /// <summary>
    ///     Draws a skewed quadrilateral facet in normalized horizontal surface coordinates.
    /// </summary>
    protected virtual void DrawSurfaceFacet(
        DrawingHandleScreen handle,
        UIBox2 box,
        float leftNorm,
        float rightNorm,
        float skewNorm,
        Color color)
    {
        PopulateSurfaceFacetVertices(_surfaceVertices, box, leftNorm, rightNorm, skewNorm);

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _surfaceVertices, color);
    }

    protected static void PopulateSurfaceFacetVertices(
        Span<Vector2> vertices,
        UIBox2 box,
        float leftNorm,
        float rightNorm,
        float skewNorm)
    {
        var left = Lerp(box.Left, box.Right, Clamp01(leftNorm));
        var right = Lerp(box.Left, box.Right, Clamp01(rightNorm));
        var skew = box.Height * skewNorm;

        vertices[0] = new Vector2(left, box.Top);
        vertices[1] = new Vector2(right, box.Top);
        vertices[2] = new Vector2(ClampToBox(right - skew, box.Left, box.Right), box.Bottom);
        vertices[3] = new Vector2(ClampToBox(left - skew, box.Left, box.Right), box.Bottom);
    }

    protected virtual bool CanAnimateSweep()
    {
        return DrawMode != DrawModeEnum.Disabled &&
               SweepEnabled &&
               GetSweepIntensity() > 0f;
    }

    protected virtual bool CanDrawSweep()
    {
        return CanAnimateSweep() &&
               _sweepProgress < 1f;
    }

    protected virtual float ClampAlpha(float alpha)
    {
        return Math.Clamp(alpha, 0f, MaxPrimitiveAlpha);
    }

    private static Color GetSweepBandColor(SweepBandTone tone)
    {
        return tone switch
        {
            SweepBandTone.Outer => SciFiPalette.AccentDim,
            SweepBandTone.Accent => SciFiPalette.Accent,
            SweepBandTone.Core => SciFiPalette.Text,
            _ => SciFiPalette.Accent
        };
    }

    protected static float ClampToBox(float value, float min, float max)
    {
        return Math.Clamp(value, min, max);
    }

    protected static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    protected static float EaseOutCubic(float value)
    {
        var inverse = 1f - Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    protected static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private readonly record struct SweepBand(
        float LeftOffset,
        float RightOffset,
        float Alpha,
        SweepBandTone Tone);

    private enum SweepBandTone
    {
        Outer,
        Accent,
        Core
    }
}
