using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Sunrise.Paint.UI;

public sealed class SaturationValueSelector : PanelContainer
{
    public const string StyleIdentifierSelector = "PaintSaturationValueSelector";
    public const string StylePropertyBackgroundTexture = "paint-saturation-value-background-texture";
    public const string StylePropertyMarkerTexture = "paint-saturation-value-marker-texture";

    public event Action? OnValueChanged;

    public float Saturation { get; private set; }
    public float Value { get; private set; } = 1f;

    private readonly ColorSelectorStyleBox _style;
    private Texture? _markerTexture;
    private float _hue;
    private bool _dragging;

    public SaturationValueSelector()
    {
        MouseFilter = MouseFilterMode.Stop;
        PanelOverride = _style = new ColorSelectorStyleBox
        {
            Hsv = true,
            XAxis = new Vector4(0f, 1f, 0f, 0f),
            YAxis = new Vector4(0f, 0f, 1f, 0f),
        };

        SetHue(0f);
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();

        if (TryGetStyleProperty(StylePropertyBackgroundTexture, out Texture? backgroundTexture))
            _style.Texture = backgroundTexture;

        if (TryGetStyleProperty(StylePropertyMarkerTexture, out Texture? markerTexture))
            _markerTexture = markerTexture;
    }

    public void SetColor(Vector4 hsv)
    {
        _hue = MathHelper.Clamp(hsv.X, 0f, 1f);
        Saturation = MathHelper.Clamp(hsv.Y, 0f, 1f);
        Value = MathHelper.Clamp(hsv.Z, 0f, 1f);
        _style.BaseColor = new Vector4(_hue, 0f, 0f, 1f);
    }

    public void SetHue(float hue)
    {
        _hue = MathHelper.Clamp(hue, 0f, 1f);
        _style.BaseColor = new Vector4(_hue, 0f, 0f, 1f);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        UpdateFromPosition(args.RelativePosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        if (!_dragging)
            return;

        UpdateFromPosition(args.RelativePosition);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_markerTexture == null)
            return;

        var position = new Vector2(Saturation * Width, (1f - Value) * Height);
        var markerSize = new Vector2(16f);
        handle.DrawTextureRect(
            _markerTexture,
            UIBox2.FromDimensions(position - markerSize / 2f, markerSize));
    }

    private void UpdateFromPosition(Vector2 position)
    {
        if (Width <= 0f || Height <= 0f)
            return;

        Saturation = MathHelper.Clamp(position.X / Width, 0f, 1f);
        Value = 1f - MathHelper.Clamp(position.Y / Height, 0f, 1f);
        OnValueChanged?.Invoke();
    }
}
