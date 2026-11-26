using System.Numerics;
using Content.Client.Parallax.Data;
using Robust.Client.Graphics;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемой vanilla-подсистемой.
namespace Content.Client.Parallax;

/// <summary>
/// Contains shared drawing operations for world and UI parallax renderers.
/// </summary>
internal static class ParallaxDrawingHelpers
{
    /// <summary>
    /// Updates parameters whose values depend on the rendered size of a procedural layer.
    /// </summary>
    public static void UpdateShaderParameters(in ParallaxLayerPrepared layer, Vector2 renderedSize)
    {
        if (layer.Shader == null || layer.TextureSource is not ShaderParallaxTextureSource)
            return;

        var safeSize = Vector2.Max(renderedSize, Vector2.One);
        layer.Shader.SetParameter("uvPixelSpan", Vector2.One / safeSize);
    }

    /// <summary>
    /// Applies a shader and restores the previous drawing state when the returned scope is disposed.
    /// </summary>
    public static ShaderScope PushShader(DrawingHandleBase handle, ShaderInstance? shader)
    {
        return new ShaderScope(handle, shader);
    }

    public static void DrawTextureRect(
        DrawingHandleWorld handle,
        Texture texture,
        Box2 bounds,
        Angle rotation)
    {
        if (rotation == Angle.Zero)
        {
            handle.DrawTextureRect(texture, bounds);
            return;
        }

        var rotatedBounds = new Box2Rotated(bounds, rotation, bounds.Center);
        handle.DrawTextureRect(texture, in rotatedBounds);
    }

    public static void DrawTextureRect(
        DrawingHandleScreen handle,
        Texture texture,
        UIBox2 bounds,
        Angle rotation)
    {
        if (rotation == Angle.Zero)
        {
            handle.DrawTextureRect(texture, bounds);
            return;
        }

        var oldTransform = handle.GetTransform();
        var center = bounds.Center;
        var rotationTransform =
            Matrix3x2.CreateTranslation(-center) *
            Matrix3Helpers.CreateRotation(rotation.Theta) *
            Matrix3x2.CreateTranslation(center);

        handle.SetTransform(rotationTransform * oldTransform);
        try
        {
            handle.DrawTextureRect(texture, bounds);
        }
        finally
        {
            handle.SetTransform(oldTransform);
        }
    }

    public readonly ref struct ShaderScope
    {
        private readonly DrawingHandleBase _handle;
        private readonly ShaderInstance? _previousShader;

        public ShaderScope(DrawingHandleBase handle, ShaderInstance? shader)
        {
            _handle = handle;
            _previousShader = handle.GetShader();
            handle.UseShader(shader);
        }

        public void Dispose()
        {
            _handle.UseShader(_previousShader);
        }
    }
}
