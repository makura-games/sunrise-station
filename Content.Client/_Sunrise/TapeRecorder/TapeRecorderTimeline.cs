using Content.Shared._Sunrise.TapeRecorder;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Sunrise.TapeRecorder;

public sealed class TapeRecorderTimeline : ProgressBar
{
    private const float PositionMarkerHalfWidth = 1f;
    private const float PositionMarkerVerticalBleed = 1f;

    private static readonly Color EmptyTapeColor = Color.FromHex("#29A342");
    private static readonly Color UsedTapeColor = Color.FromHex("#D12E24");
    private static readonly Color PositionColor = Color.FromHex("#9EF580");
    private static readonly Color BorderColor = Color.FromHex("#050505");

    private readonly List<TapeCassetteRecordedRange> _recordedRanges = [];
    private TimeSpan _position;
    private TimeSpan _capacity;

    public TapeRecorderTimeline()
    {
        MouseFilter = MouseFilterMode.Pass;
        MinValue = 0f;
        MaxValue = 1f;
        Value = 1f;
        BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#14171A") };
        ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = EmptyTapeColor };
    }

    public void UpdateState(TimeSpan position, TimeSpan capacity, IReadOnlyList<TapeCassetteRecordedRange> recordedRanges)
    {
        _position = position;
        _capacity = capacity;
        _recordedRanges.Clear();
        _recordedRanges.AddRange(recordedRanges);
        Value = 1f;
        InvalidateMeasure();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var capacitySeconds = (float) _capacity.TotalSeconds;
        if (capacitySeconds > 0f)
        {
            DrawRecordedRanges(handle, PixelSizeBox, capacitySeconds);
            DrawPositionMarker(handle, PixelSizeBox, capacitySeconds);
        }

        handle.DrawRect(PixelSizeBox, BorderColor, false);
    }

    private void DrawRecordedRanges(DrawingHandleScreen handle, UIBox2 trackBox, float capacitySeconds)
    {
        foreach (var range in _recordedRanges)
        {
            var start = Math.Clamp((float) range.Start.TotalSeconds / capacitySeconds, 0f, 1f);
            var end = Math.Clamp((float) range.End.TotalSeconds / capacitySeconds, 0f, 1f);

            if (end <= start)
                continue;

            var left = MathHelper.Lerp(trackBox.Left, trackBox.Right, start);
            var right = MathHelper.Lerp(trackBox.Left, trackBox.Right, end);
            handle.DrawRect(new UIBox2(left, trackBox.Top, right, trackBox.Bottom), UsedTapeColor);
        }
    }

    private void DrawPositionMarker(DrawingHandleScreen handle, UIBox2 trackBox, float capacitySeconds)
    {
        var position = Math.Clamp((float) _position.TotalSeconds / capacitySeconds, 0f, 1f);
        var positionX = MathHelper.Lerp(trackBox.Left, trackBox.Right, position);
        handle.DrawRect(new UIBox2(
            positionX - PositionMarkerHalfWidth,
            trackBox.Top - PositionMarkerVerticalBleed,
            positionX + PositionMarkerHalfWidth,
            trackBox.Bottom + PositionMarkerVerticalBleed),
            PositionColor);
    }
}
