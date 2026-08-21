using System.Threading;
using System.Threading.Tasks;
using Content.Client._Sunrise;
using Robust.Client.Graphics;
using Robust.Shared.Network;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемой vanilla-подсистемой.
namespace Content.Client.Parallax.Data;

public sealed partial class ImageParallaxTextureSource
{
    private const string NetworkTexturePrefix = "/NetTextures/";

    private static bool TryGenerateNetworkTexture(
        ResPath path,
        CancellationToken cancel,
        out Task<Texture> texture)
    {
        var resourcePath = path.ToString();
        if (!resourcePath.StartsWith(NetworkTexturePrefix, StringComparison.Ordinal))
        {
            texture = default!;
            return false;
        }

        texture = GenerateNetworkTexture(resourcePath, cancel);
        return true;
    }

    private static async Task<Texture> GenerateNetworkTexture(string resourcePath, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var netManager = IoCManager.Resolve<IClientNetManager>();
        await WaitForConnection(netManager, cancel);
        cancel.ThrowIfCancellationRequested();

        var textureManager = IoCManager.Resolve<NetTexturesManager>();
        if (textureManager.TryGetTexture(resourcePath, out var cachedTexture) && cachedTexture != null)
            return cachedTexture;

        var completion = new TaskCompletionSource<Texture>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnResourceLoaded(string loadedPath)
        {
            if (loadedPath == resourcePath &&
                textureManager.TryGetTexture(resourcePath, out var loadedTexture) &&
                loadedTexture != null)
            {
                completion.TrySetResult(loadedTexture);
            }
        }

        void OnResourceLoadFailed(string failedPath)
        {
            if (failedPath == resourcePath)
                completion.TrySetResult(Texture.Transparent);
        }

        textureManager.ResourceLoaded += OnResourceLoaded;
        textureManager.ResourceLoadFailed += OnResourceLoadFailed;
        try
        {
            textureManager.EnsureResource(resourcePath);

            if (textureManager.TryGetTexture(resourcePath, out cachedTexture) && cachedTexture != null)
                return cachedTexture;

            if (textureManager.IsResourceLoadFailed(resourcePath))
                return Texture.Transparent;

            return await completion.Task.WaitAsync(cancel);
        }
        finally
        {
            textureManager.ResourceLoaded -= OnResourceLoaded;
            textureManager.ResourceLoadFailed -= OnResourceLoadFailed;
        }
    }

    private static async Task WaitForConnection(IClientNetManager netManager, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if (netManager.IsConnected)
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnConnected(object? _, NetChannelArgs __)
        {
            completion.TrySetResult();
        }

        netManager.Connected += OnConnected;
        try
        {
            if (!netManager.IsConnected)
                await completion.Task.WaitAsync(cancel);
        }
        finally
        {
            netManager.Connected -= OnConnected;
        }
    }
}
