using Content.Shared._Sunrise.CartridgeLoader.Cartridges;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise;

public sealed partial class NetTexturesManager
{
    #region Public API
    /// <summary>
    /// Гарантирует запрос сетевого ресурса и возвращает, готов ли он уже к использованию.
    /// </summary>
    /// <remarks>
    /// Вызывающий код должен повторно дергать метод из путей обновления UI, пока он не вернет true или
    /// <see cref="ResourceLoaded"/> не сообщит, что ресурс стал доступен.
    /// </remarks>
    /// <param name="resourcePath">Rooted или relative путь ресурса, запрошенный потребителем.</param>
    /// <returns>
    /// <see langword="true"/>, если ресурс уже готов к использованию; иначе <see langword="false"/>.
    /// </returns>
    public bool EnsureResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return false;

        var resPath = ToResPath(resourcePath);
        var resourceKey = resPath.ToString();

        if (IsResourceLoaded(resourceKey))
            return true;

        if (_failedResources.Contains(resourceKey))
            return false;

        if (!TryCheckResourceComplete(resourceKey, resPath, out var isComplete))
            return false;

        if (isComplete)
        {
            StartPreparingResource(resourceKey, resPath);
            return IsResourceLoaded(resourceKey);
        }

        _pendingResources[resourceKey] = resPath;

        if (!_requestedResources.Contains(resourceKey))
            RequestResource(resourceKey);

        return false;
    }

    /// <summary>
    /// Возвращает готовую к использованию текстуру, загруженную через pipeline NetTextures.
    /// </summary>
    /// <param name="resourcePath">Путь ресурса, ранее запрошенный через <see cref="EnsureResource"/>.</param>
    /// <param name="texture">Готовая текстура, когда метод возвращает <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если текстура готова; иначе <see langword="false"/>.
    /// </returns>
    public bool TryGetTexture(string resourcePath, out Texture? texture)
    {
        texture = null;

        if (string.IsNullOrWhiteSpace(resourcePath))
            return false;

        var resourceKey = ToResPath(resourcePath).ToString();
        if (_loadedTextures.TryGetValue(resourceKey, out var loaded))
        {
            texture = loaded.Texture;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether preparation of a requested resource has failed in the current session.
    /// </summary>
    /// <param name="resourcePath">Rooted or relative path passed to <see cref="EnsureResource"/>.</param>
    /// <returns><see langword="true"/> when the resource is in the failed set.</returns>
    public bool IsResourceLoadFailed(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return true;

        return _failedResources.Contains(ToResPath(resourcePath).ToString());
    }

    /// <summary>
    /// Возвращает готовое к использованию состояние RSI-анимации, загруженное через pipeline NetTextures.
    /// </summary>
    /// <param name="resourcePath">Путь RSI-ресурса, ранее запрошенный через <see cref="EnsureResource"/>.</param>
    /// <param name="stateId">Идентификатор RSI-состояния внутри загруженного ресурса.</param>
    /// <param name="state">Готовое состояние анимации, когда метод возвращает <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если запрошенное состояние готово; иначе <see langword="false"/>.
    /// </returns>
    public bool TryGetAnimationState(string resourcePath, string stateId, out NetTextureAnimationState? state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(resourcePath))
            return false;

        var resourceKey = ToResPath(resourcePath).ToString();
        if (!_loadedRsis.TryGetValue(resourceKey, out var loaded))
            return false;

        return loaded.States.TryGetValue(stateId, out state);
    }

    /// <summary>
    /// Определяет in-memory путь загруженного сетевого ресурса.
    /// </summary>
    /// <param name="resourcePath">Rooted или relative путь ресурса, используемый потребителями.</param>
    /// <returns>Rooted путь внутри смонтированного <c>/Uploaded</c> content root.</returns>
    public ResPath GetUploadedPath(string resourcePath)
    {
        var relativePath = ToResPath(resourcePath).ToRelativePath();
        return ((new ResPath(UploadedPrefix) / relativePath).ToRootedPath());
    }

    /// <summary>
    /// Отправляет снимок PDA на сервер для динамической регистрации NetTexture.
    /// </summary>
    /// <param name="loaderUid">Net entity, инициировавшая снимок.</param>
    /// <param name="imageData">Кодированные байты изображения для регистрации на сервере.</param>
    /// <param name="width">Ширина снимка в пикселях.</param>
    /// <param name="height">Высота снимка в пикселях.</param>
    public void SendPhotoToServer(NetEntity loaderUid, byte[] imageData, int width, int height)
    {
        if (!_netManager.IsConnected)
        {
            _sawmill.Warning("Cannot send photo: client not connected to server");
            return;
        }

        var message = new PdaPhotoCaptureMessage
        {
            LoaderUid = loaderUid,
            ImageData = imageData,
            Width = width,
            Height = height
        };

        _netManager.ClientSendMessage(message);
        _sawmill.Debug($"Sent photo to server: {width}x{height}, {imageData.Length} bytes, loader: {loaderUid}");
    }
    #endregion
}
