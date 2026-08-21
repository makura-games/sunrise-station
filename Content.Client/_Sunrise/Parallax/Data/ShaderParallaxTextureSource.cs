using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Robust.Client.Graphics;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемой vanilla-подсистемой.
namespace Content.Client.Parallax.Data;

/// <summary>
/// Provides a white backing texture and a logical canvas size for a procedural parallax layer.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class ShaderParallaxTextureSource : IParallaxTextureSource
{
    /// <summary>
    /// Logical size of the procedural canvas in pixels.
    /// </summary>
    [DataField]
    public Vector2 Size { get; private set; } = new(1920f, 1080f);

    Task<Texture> IParallaxTextureSource.GenerateTexture(CancellationToken cancel)
    {
        return Task.FromResult(Texture.White);
    }
}
