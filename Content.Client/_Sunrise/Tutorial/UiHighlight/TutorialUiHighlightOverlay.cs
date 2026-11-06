using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Tutorial.UiHighlight;

public sealed class TutorialUiHighlightOverlay : Control
{
    private const float HighlightPadding = 5f;
    private const float BorderThickness = 3f;
    private const float PulsePadding = 8f;
    private const float PulseBorderThickness = 2f;
    private const float OuterPulsePadding = 10f;
    private const float OuterPulseBorderThickness = 2f;
    private const float PulseSpeed = 3.5f;

    private static readonly Color HighlightColor = Color.FromHex("#D8A63A");
    private static readonly Color BorderColor = Color.FromHex("#FFD84D");

    private Control _root = default!;
    private readonly List<TutorialUiHighlightSelector> _selectors = [];
    private readonly TutorialUiControlResolver _resolver = new(IoCManager.Resolve<IEntityManager>());
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();

    public TutorialUiHighlightOverlay(Control root, IReadOnlyList<TutorialUiHighlightSelector> selectors, bool blockInput)
    {
        HorizontalAlignment = HAlignment.Stretch;
        VerticalAlignment = VAlignment.Stretch;

        SetTarget(root, selectors, blockInput);
    }

    public void SetTarget(Control root, IReadOnlyList<TutorialUiHighlightSelector> selectors, bool blockInput)
    {
        _root = root;
        MouseFilter = blockInput ? MouseFilterMode.Stop : MouseFilterMode.Ignore;

        _selectors.Clear();
        _selectors.AddRange(selectors);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        if (!TryGetTarget(out var target))
            return;

        if (!target.VisibleInTree || target.PixelSize.X <= 0 || target.PixelSize.Y <= 0)
            return;

        var fullRect = UIBox2.FromDimensions(0f, 0f, PixelSize.X, PixelSize.Y);
        var origin = new Vector2(GlobalPixelPosition.X, GlobalPixelPosition.Y);
        var targetRect = ((UIBox2)target.GlobalPixelRect).Translated(-origin);
        var pulse = (MathF.Sin((float)_timing.RealTime.TotalSeconds * PulseSpeed) + 1f) * 0.5f;
        var padding = (HighlightPadding + PulsePadding * pulse) * UIScale;
        targetRect = new UIBox2(
            targetRect.Left - padding,
            targetRect.Top - padding,
            targetRect.Right + padding,
            targetRect.Bottom + padding);

        var clampedRect = new UIBox2(
            Math.Clamp(targetRect.Left, fullRect.Left, fullRect.Right),
            Math.Clamp(targetRect.Top, fullRect.Top, fullRect.Bottom),
            Math.Clamp(targetRect.Right, fullRect.Left, fullRect.Right),
            Math.Clamp(targetRect.Bottom, fullRect.Top, fullRect.Bottom));

        if (clampedRect.Right <= clampedRect.Left || clampedRect.Bottom <= clampedRect.Top)
            return;

        DrawFilledRect(handle, clampedRect, HighlightColor.WithAlpha(0.12f + 0.2f * pulse));
        DrawBorder(
            handle,
            clampedRect,
            (BorderThickness + PulseBorderThickness * pulse) * UIScale,
            BorderColor.WithAlpha(0.75f + 0.25f * pulse));

        var outerPadding = OuterPulsePadding * pulse * UIScale;
        var outerRect = new UIBox2(
            Math.Clamp(clampedRect.Left - outerPadding, fullRect.Left, fullRect.Right),
            Math.Clamp(clampedRect.Top - outerPadding, fullRect.Top, fullRect.Bottom),
            Math.Clamp(clampedRect.Right + outerPadding, fullRect.Left, fullRect.Right),
            Math.Clamp(clampedRect.Bottom + outerPadding, fullRect.Top, fullRect.Bottom));

        DrawBorder(
            handle,
            outerRect,
            OuterPulseBorderThickness * UIScale,
            BorderColor.WithAlpha(0.15f + 0.7f * (1f - pulse)));
    }

    private bool TryGetTarget([NotNullWhen(true)] out Control? target)
    {
        if (_selectors.Count == 0)
        {
            target = null;
            return false;
        }

        return _resolver.TryFind(_root, _selectors, out target);
    }

    private static void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, float thickness, Color color)
    {
        DrawFilledRect(handle, new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + thickness), color);
        DrawFilledRect(handle, new UIBox2(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), color);
        DrawFilledRect(handle, new UIBox2(rect.Left, rect.Top, rect.Left + thickness, rect.Bottom), color);
        DrawFilledRect(handle, new UIBox2(rect.Right - thickness, rect.Top, rect.Right, rect.Bottom), color);
    }

    private static void DrawFilledRect(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            return;

        handle.DrawRect(rect, color);
    }

}
