using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Shared.Utility;
using Content.Client.IoC;

namespace Content.Client.Parallax.Data;

[UsedImplicitly]
[DataDefinition]
public sealed partial class ImageParallaxTextureSource : IParallaxTextureSource
{
    /// <summary>
    /// Texture path.
    /// </summary>
    [DataField("path", required: true)]
    public ResPath Path { get; private set; } = default!;

    async Task<Texture> IParallaxTextureSource.GenerateTexture(CancellationToken cancel)
    {
        // Sunrise added start - NetTextures загружаются сервером и не подменяются несвязанным локальным фоном.
        if (TryGenerateNetworkTexture(Path, cancel, out var networkTexture))
            return await networkTexture;
        // Sunrise added end

        return StaticIoC.ResC.GetTexture(Path);
    }
}

