using System;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private const float OpenAnimationDuration = 0.6f;
    private const float OpenAnimationWidthPhase = 0.32f;
    private const float OpenAnimationHeightDelay = 0.16f;
    private const float OpenAnimationInitialHeight = 2f;
    private const float OpenAnimationInitialWidthRatio = 0.18f;

    private bool _openAnimationQueued;
    private bool _openAnimationActive;
    private bool _openAnimationHasRestore;
    private float _openAnimationTimer;
    private Vector2 _openAnimationRestoreMinSize;
    private Vector2 _openAnimationRestoreSetSize;
    private Vector2 _openAnimationTargetPosition;
    private Vector2 _openAnimationTargetSize;

    protected override void Opened()
    {
        base.Opened();

        QueueOpenAnimation();
    }

    public override void Close()
    {
        ResetOpenAnimation();
        base.Close();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        ProcessSponsorTierDetailsBuild();
        UpdateSponsorTierPreviewDirections(args.DeltaSeconds);

        if (_openAnimationQueued)
            StartOpenAnimation();

        if (!_openAnimationActive)
            return;

        _openAnimationTimer += args.DeltaSeconds;
        var progress = Math.Clamp(_openAnimationTimer / OpenAnimationDuration, 0f, 1f);
        ApplyOpenAnimation(progress);

        if (progress >= 1f)
            FinishOpenAnimation();
    }

    private void QueueOpenAnimation()
    {
        _openAnimationQueued = true;
        _openAnimationActive = false;
        _openAnimationTimer = 0f;
        Modulate = Color.Transparent;
        OpenAnimationOverlay.Visible = false;
    }

    private void StartOpenAnimation()
    {
        _openAnimationQueued = false;
        _openAnimationActive = true;
        _openAnimationTimer = 0f;
        _openAnimationRestoreMinSize = MinSize;
        _openAnimationRestoreSetSize = SetSize;
        _openAnimationTargetSize = ResolveOpenAnimationTargetSize();
        _openAnimationTargetPosition = Position;
        _openAnimationHasRestore = true;

        MinSize = Vector2.Zero;
        OpenAnimationOverlay.Visible = true;
        ApplyOpenAnimation(0f);
    }

    private Vector2 ResolveOpenAnimationTargetSize()
    {
        var width = ResolveOpenAnimationAxis(_openAnimationRestoreSetSize.X, Size.X, DesiredSize.X, _openAnimationRestoreMinSize.X);
        var height = ResolveOpenAnimationAxis(_openAnimationRestoreSetSize.Y, Size.Y, DesiredSize.Y, _openAnimationRestoreMinSize.Y);
        return new Vector2(width, height);
    }

    private static float ResolveOpenAnimationAxis(float setValue, float currentValue, float desiredValue, float minValue)
    {
        if (float.IsFinite(setValue) && setValue > 0f)
            return setValue;

        if (float.IsFinite(currentValue) && currentValue > 0f)
            return currentValue;

        if (float.IsFinite(desiredValue) && desiredValue > 0f)
            return desiredValue;

        return Math.Max(minValue, 1f);
    }

    private void ApplyOpenAnimation(float progress)
    {
        var widthProgress = Easings.OutQuad(Math.Clamp(progress / OpenAnimationWidthPhase, 0f, 1f));
        var heightProgress = progress <= OpenAnimationHeightDelay
            ? 0f
            : Easings.InOutCubic(Math.Clamp((progress - OpenAnimationHeightDelay) / (1f - OpenAnimationHeightDelay), 0f, 1f));

        var initialWidth = Math.Max(_openAnimationTargetSize.X * OpenAnimationInitialWidthRatio, 1f);
        var currentSize = new Vector2(
            Lerp(initialWidth, _openAnimationTargetSize.X, widthProgress),
            Lerp(OpenAnimationInitialHeight, _openAnimationTargetSize.Y, heightProgress));

        SetSize = currentSize;
        LayoutContainer.SetPosition(this, _openAnimationTargetPosition + (_openAnimationTargetSize - currentSize) / 2f);
        Modulate = Color.White.WithAlpha(Lerp(0.35f, 1f, Easings.InOutSine(progress)));
        OpenAnimationOverlay.Progress = progress;
    }

    private void FinishOpenAnimation()
    {
        _openAnimationActive = false;
        RestoreOpenAnimationWindowState();
        Modulate = Color.White;
        OpenAnimationOverlay.Visible = false;
        OpenAnimationOverlay.Progress = 1f;
    }

    private void ResetOpenAnimation()
    {
        _openAnimationQueued = false;
        _openAnimationActive = false;
        RestoreOpenAnimationWindowState();
        Modulate = Color.White;
        OpenAnimationOverlay.Visible = false;
        OpenAnimationOverlay.Progress = 1f;
    }

    private void RestoreOpenAnimationWindowState()
    {
        if (!_openAnimationHasRestore)
            return;

        MinSize = _openAnimationRestoreMinSize;
        SetSize = _openAnimationRestoreSetSize;
        LayoutContainer.SetPosition(this, _openAnimationTargetPosition);
        _openAnimationHasRestore = false;
    }

    private static float Lerp(float from, float to, float progress)
    {
        return from + (to - from) * progress;
    }
}
