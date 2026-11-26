using System.Numerics;
using Content.Client.Parallax.Data;
using Robust.Client.Graphics;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемой vanilla-подсистемой.
namespace Content.Client.Parallax;

public partial struct ParallaxLayerPrepared
{
    /// <summary>
    /// Actual texture source used to prepare the layer. It can replace the source stored in the prototype config.
    /// </summary>
    public IParallaxTextureSource TextureSource { get; set; }

    /// <summary>
    /// Logical source texture size in pixels. A procedural layer can use a 1x1 backing texture.
    /// </summary>
    public Vector2 TextureSize { get; set; }

    /// <summary>
    /// Shader instance prepared for this layer.
    /// </summary>
    public ShaderInstance? Shader { get; set; }

    /// <summary>
    /// Whether this layer owns its shader instance and must dispose it when unloaded.
    /// </summary>
    public bool OwnsShader { get; set; }
}
