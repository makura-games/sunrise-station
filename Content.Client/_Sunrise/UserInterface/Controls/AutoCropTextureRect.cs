using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.UserInterface.Controls;

public sealed class AutoCropTextureRect : TextureRect
{
    private Vector2 _cropFocus = new(0.5f, 0.5f);

    public AutoCropTextureRect()
    {
        CanShrink = true;
    }

    /// <summary>
    ///     Нормализованная точка фокуса кадрирования: 0 0 сохраняет верхний левый край, 1 1 — нижний правый.
    /// </summary>
    public Vector2 CropFocus
    {
        get => _cropFocus;
        set => _cropFocus = Vector2.Clamp(value, Vector2.Zero, Vector2.One);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var texture = Texture;
        if (texture == null || PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        ShaderInstance? shader = null;
        if (ShaderOverride != null)
        {
            shader = ShaderOverride;
        }
        else if (TryGetStyleProperty(StylePropertyShader, out ShaderInstance? styleShader))
        {
            shader = styleShader;
        }

        if (shader != null)
            handle.UseShader(shader);

        Vector2 textureSize = texture.Size;
        var cropSize = Vector2.Min(textureSize, PixelSize);
        var cropOffset = (textureSize - cropSize) * CropFocus;
        var drawOffset = (PixelSize - cropSize) * CropFocus;

        handle.DrawTextureRectRegion(
            texture,
            UIBox2.FromDimensions(drawOffset, cropSize),
            UIBox2.FromDimensions(cropOffset, cropSize));
    }
}
