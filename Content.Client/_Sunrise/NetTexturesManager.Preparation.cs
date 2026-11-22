using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.NetTextures;
using Robust.Client;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise;

public sealed partial class NetTexturesManager
{
    #region Resource Tracking
    /// <summary>
    /// Повторно проверяет все ожидающие ресурсы и продвигает элементы, ставшие полными с прошлого обновления.
    /// </summary>
    private void UpdatePendingResources()
    {
        if (_pendingResources.Count == 0)
            return;

        _resourcesReadyToPrepare.Clear();

        foreach (var (resourceKey, resPath) in _pendingResources)
        {
            if (IsResourceLoaded(resourceKey) || _preparingResources.Contains(resourceKey))
                continue;

            if (!TryCheckResourceComplete(resourceKey, resPath, out var isComplete))
                continue;

            if (isComplete)
            {
                _resourcesReadyToPrepare.Add((resourceKey, resPath));
                continue;
            }

            if (!_requestedResources.Contains(resourceKey))
                RequestResource(resourceKey);
        }

        foreach (var (resourceKey, resPath) in _resourcesReadyToPrepare)
        {
            StartPreparingResource(resourceKey, resPath);
        }

        _resourcesReadyToPrepare.Clear();
    }

    /// <summary>
    /// Ставит полный ресурс в очередь декодирования и подготовки к загрузке.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса.</param>
    /// <param name="resPath">Нормализованный путь ресурса.</param>
    private void StartPreparingResource(string resourceKey, ResPath resPath)
    {
        if (IsResourceLoaded(resourceKey))
        {
            _pendingResources.Remove(resourceKey);
            return;
        }

        if (!_preparingResources.Add(resourceKey))
            return;

        _prepareRequests.Enqueue(new PreparationRequest(resourceKey, resPath, ReadSessionGeneration()));
        TryStartNextPreparation();
    }

    /// <summary>
    /// Отправляет одноразовый сетевой запрос ресурса, которого еще нет локально.
    /// </summary>
    /// <param name="resourceKey">Нормализованный путь запрошенного ресурса.</param>
    private void RequestResource(string resourceKey)
    {
        if (_requestedResources.Contains(resourceKey))
            return;

        if (!_netManager.IsConnected)
        {
            _sawmill.Debug($"Cannot request resource {resourceKey}: client not connected to server");
            return;
        }

        _requestedResources.Add(resourceKey);

        var msg = new RequestNetworkResourceMessage
        {
            ResourcePath = resourceKey
        };

        _netManager.ClientSendMessage(msg);
    }

    /// <summary>
    /// Проверяет, есть ли у ресурса полностью подготовленное представление для использования.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса.</param>
    /// <returns><see langword="true"/>, если ресурс уже загружен.</returns>
    private bool IsResourceLoaded(string resourceKey)
    {
        return _loadedTextures.ContainsKey(resourceKey) || _loadedRsis.ContainsKey(resourceKey);
    }

    /// <summary>
    /// Проверяет, присутствуют ли локально сырые загруженные файлы, необходимые ресурсу.
    /// </summary>
    /// <remarks>
    /// Для RSI-ресурсов нужна полная готовность директории, а не только наличие <c>meta.json</c>.
    /// </remarks>
    /// <param name="resourcePath">Нормализованный путь ресурса.</param>
    /// <returns><see langword="true"/>, если сырой загруженный ресурс достаточно полон для подготовки.</returns>
    private bool IsResourceComplete(ResPath resourcePath)
    {
        var relativePath = resourcePath.ToRelativePath();

        if (IsRsiPath(resourcePath))
            return CheckRsiFilesComplete(relativePath);

        var uploadedPath = (new ResPath(UploadedPrefix) / relativePath).ToRootedPath();
        return _resourceManager.ContentFileExists(uploadedPath);
    }

    /// <summary>
    /// Проверяет полноту ресурса и превращает поврежденные полезные нагрузки в терминальные сбои.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса.</param>
    /// <param name="resPath">Нормализованный путь ресурса.</param>
    /// <param name="isComplete">Достаточно ли файлы ресурса полны для подготовки.</param>
    /// <returns>
    /// <see langword="true"/>, если проверка полноты прошла; иначе <see langword="false"/> после того, как
    /// ресурс был помечен как сбойный.
    /// </returns>
    private bool TryCheckResourceComplete(string resourceKey, ResPath resPath, out bool isComplete)
    {
        try
        {
            isComplete = IsResourceComplete(resPath);
            return true;
        }
        catch (InvalidDataException ex)
        {
            MarkResourceFailed(resourceKey, ex.Message);
            isComplete = false;
            return false;
        }
    }
    #endregion

    #region Connection State
    /// <summary>
    /// Очищает все локальное для сессии состояние NetTextures, когда клиент выходит из подключенного игрового flow.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Переход run level.</param>
    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.OldLevel is not (ClientRunLevel.Connected or ClientRunLevel.InGame))
            return;

        if (e.NewLevel is ClientRunLevel.Connected or ClientRunLevel.InGame)
            return;

        _sawmill.Debug("Clearing NetTextures state on disconnect");
        ResetState();
    }

    /// <summary>
    /// Сбрасывает запросы, частичные передачи, staged uploads и загруженные ресурсы для текущей сессии.
    /// </summary>
    /// <remarks>
    /// Этот метод является границей безопасности для переподключения. Все, что может повлиять на следующую попытку подключения,
    /// должно очищаться здесь, включая сборки fallback-чанков и частично опубликованный загруженный контент.
    /// </remarks>
    private void ResetState()
    {
        ResourcesInvalidated?.Invoke();
        AdvanceSessionGeneration();

        _sessionCts.Cancel();
        _sessionCts.Dispose();
        _sessionCts = new CancellationTokenSource();

        _requestedResources.Clear();
        _pendingResources.Clear();
        _preparingResources.Clear();
        _failedResources.Clear();
        _netTexturesContentRoot.Clear();
        _prepareRequests.Clear();
        _resourcesReadyToPrepare.Clear();
        _rsiCompleteness.Clear();
        _prepareWorkerRunning = false;
        _activePrepareRequestId = 0;

        lock (_pendingTransferBatches)
        {
            _pendingTransferBatches.Clear();
        }

        while (_preparedUploads.Count > 0)
        {
            var upload = _preparedUploads.Dequeue();
            upload.EnqueueDisposals(_deferredDisposals);
        }

        foreach (var assembly in _fallbackChunkAssemblies.Values)
        {
            _deferredDisposals.Enqueue(assembly);
        }

        _fallbackChunkAssemblies.Clear();

        // Опубликованные текстуры могут всё ещё использоваться UI или параллаксом.
        // Их освободит финализатор после исчезновения последней внешней ссылки.
        _loadedTextures.Clear();
        _loadedRsis.Clear();
    }
    #endregion

    #region Preparation Queue
    /// <summary>
    /// Запускает следующую задачу подготовки из очереди, если единственный preparation worker свободен.
    /// </summary>
    private void TryStartNextPreparation()
    {
        if (_prepareWorkerRunning)
            return;

        var currentGeneration = ReadSessionGeneration();

        while (_prepareRequests.Count > 0)
        {
            var request = _prepareRequests.Dequeue();
            if (request.Generation != currentGeneration)
                continue;

            if (!_preparingResources.Contains(request.ResourceKey))
                continue;

            _prepareWorkerRunning = true;
            var requestId = ++_nextPrepareRequestId;
            _activePrepareRequestId = requestId;
            var cancellationToken = _sessionCts.Token;
            _ = Task.Run(() => PrepareResourceWorker(request, requestId, cancellationToken));
            return;
        }
    }

    /// <summary>
    /// Декодирует один ресурс из очереди в background worker и превращает его в staged upload job.
    /// </summary>
    /// <param name="request">Ресурс для подготовки.</param>
    /// <param name="requestId">Уникальный идентификатор активного worker request.</param>
    /// <param name="cancellationToken">Текущий токен отмены сессии.</param>
    private void PrepareResourceWorker(PreparationRequest request, int requestId, CancellationToken cancellationToken)
    {
        PreparedUploadJob? upload = null;
        Exception? error = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsRsiPath(request.ResPath))
            {
                var prepared = DecodeRsi(request.ResPath, cancellationToken);
                upload = new PreparedRsiUploadJob(request.ResourceKey, request.Generation, prepared);
            }
            else
            {
                var prepared = DecodeTexture(request.ResPath, cancellationToken);
                upload = new PreparedTextureUploadJob(request.ResourceKey, request.Generation, prepared);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = ex;
        }

        _taskManager.RunOnMainThread(() => FinishPreparationWorker(request, requestId, upload, error));
    }

    /// <summary>
    /// Финализирует результат worker'а на main thread и либо ставит staged upload в очередь, либо записывает сбой.
    /// </summary>
    /// <param name="request">Запрос, завершенный worker'ом.</param>
    /// <param name="requestId">Уникальный идентификатор активного worker request.</param>
    /// <param name="upload">Staged upload job, созданная worker'ом, если есть.</param>
    /// <param name="error">Сбой декодирования, если worker не создал upload.</param>
    private void FinishPreparationWorker(
        PreparationRequest request,
        int requestId,
        PreparedUploadJob? upload,
        Exception? error)
    {
        if (_activePrepareRequestId == requestId)
        {
            _prepareWorkerRunning = false;
            _activePrepareRequestId = 0;
        }

        var currentGeneration = ReadSessionGeneration();

        if (upload != null)
        {
            if (request.Generation == currentGeneration)
                _preparedUploads.Enqueue(upload);
            else
                upload.EnqueueDisposals(_deferredDisposals);
        }
        else if (error != null && request.Generation == currentGeneration)
        {
            MarkResourceFailed(request.ResourceKey, error.Message);
        }

        TryStartNextPreparation();
    }
    #endregion
}
