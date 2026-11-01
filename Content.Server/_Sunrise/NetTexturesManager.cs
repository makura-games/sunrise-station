using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared._Sunrise.NetTextures;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using ByteHelpers = Robust.Shared.Utility.ByteHelpers;

namespace Content.Server._Sunrise;

/// <summary>
/// Менеджер динамической загрузки сетевых текстур с сервера на клиент.
/// Использует High Bandwidth Transfer (WebSocket), чтобы не блокировать основной игровой трафик.
/// На клиенте текстуры загружаются в MemoryContentRoot.
/// </summary>
public sealed partial class NetTexturesManager
{
    /// <summary>
    /// Ключ передачи для загрузки текстур server -> client через WebSocket.
    /// </summary>
    private const string TransferKeyNetTextures = "TransferKeyNetTextures";
    private const int MaxConcurrentTransferWorkers = 2;

    [Dependency] private IResourceManager _resourceManager = default!;
    [Dependency] private IServerNetManager _netManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ITransferManager _transferManager = default!;

    private ISawmill _sawmill = default!;
    private const string AllowedPrefix = "/NetTextures/";

    /// <summary>
    /// Динамически зарегистрированные in-memory ресурсы, которых нет на диске.
    /// Ключом служит относительный путь загрузки, используемый на клиенте (например, "NetTextures/Messenger/photo_123.png").
    /// </summary>
    private readonly Dictionary<ResPath, byte[]> _dynamicResources = new();
    private readonly Lock _dynamicResourcesLock = new();
    private readonly Queue<TransferRequest> _pendingTransferRequests = new();
    private readonly Lock _transferQueueLock = new();
    private readonly Dictionary<ResPath, StaticTransferBundle> _staticBundles = new();
    private readonly Dictionary<ResPath, Task<StaticTransferBundle?>> _staticBundleTasks = new();
    private readonly Lock _staticBundleLock = new();
    private int _activeTransferWorkers;

    /// <summary>
    /// Callback для обработки снимков. PhotoCartridgeSystem регистрирует здесь себя.
    /// </summary>
    public Action<PdaPhotoCaptureMessage>? OnPhotoCaptureMessage { get; set; }

    /// <summary>
    /// Регистрирует обработчики запросов, fallback и фото для серверного pipeline NetTextures.
    /// </summary>
    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("network.textures");
        _netManager.RegisterNetMessage<RequestNetworkResourceMessage>(OnRequestNetworkResource);
        _netManager.RegisterNetMessage<NetTextureResourceChunkMessage>();

        _netManager.RegisterNetMessage<PdaPhotoCaptureMessage>(
            msg => OnPhotoCaptureMessage?.Invoke(msg),
            accept: NetMessageAccept.Server);

        _transferManager.RegisterTransferMessage(TransferKeyNetTextures);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    /// <summary>
    /// Очищает привязанные к раунду кэши NetTextures при рестарте раунда, чтобы не росла память.
    /// </summary>
    public void ClearRoundCaches()
    {
        lock (_dynamicResourcesLock)
        {
            _dynamicResources.Clear();
        }

        lock (_staticBundleLock)
        {
            _staticBundles.Clear();
            _staticBundleTasks.Clear();
        }

        lock (_transferQueueLock)
        {
            _pendingTransferRequests.Clear();
        }

        _sawmill.Info("Cleared NetTextures round caches due to round restart.");
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var removed = 0;
        lock (_transferQueueLock)
        {
            if (_pendingTransferRequests.Count == 0)
                return;

            var retained = new Queue<TransferRequest>(_pendingTransferRequests.Count);
            while (_pendingTransferRequests.Count > 0)
            {
                var request = _pendingTransferRequests.Dequeue();
                if (ReferenceEquals(request.Session, args.Session))
                {
                    removed++;
                    continue;
                }

                retained.Enqueue(request);
            }

            foreach (var request in retained)
            {
                _pendingTransferRequests.Enqueue(request);
            }
        }

        if (removed > 0)
        {
            _sawmill.Debug(
                $"Dropped {removed} pending NetTextures transfer request(s) for disconnected session {args.Session.Name}.");
        }
    }

    /// <summary>
    /// Обрабатывает клиентский запрос ресурса после определения сессии отправителя и проверки пути.
    /// </summary>
    /// <param name="msg">Входящий запрос ресурса.</param>
    private void OnRequestNetworkResource(RequestNetworkResourceMessage msg)
    {
        if (!_playerManager.TryGetSessionByChannel(msg.MsgChannel, out var session))
            return;

        var resourcePath = msg.ResourcePath;
        ResPath resPath;

        if (resourcePath.StartsWith("/"))
        {
            resPath = new ResPath(resourcePath);
        }
        else
        {
            resPath = new ResPath("/") / resourcePath;
        }

        resPath = resPath.Clean();

        if (!ValidateResourcePath(resPath, out var errorMessage))
        {
            _sawmill.Warning($"Rejected resource request from {session.Name}: {errorMessage} (path: {msg.ResourcePath})");
            return;
        }

        if (IsCurrentConnection(session, msg.MsgChannel))
            EnqueueResourceSend(session, msg.MsgChannel, resPath);
    }

    /// <summary>
    /// Проверяет, что путь ресурса безопасен и находится внутри разрешенных директорий.
    /// Предотвращает path traversal атаки, проверяя, что путь не выходит из разрешенных директорий.
    /// </summary>
    private bool ValidateResourcePath(ResPath path, out string? errorMessage)
    {
        errorMessage = null;

        if (!path.IsRooted)
        {
            errorMessage = "Path must be rooted";
            return false;
        }

        var pathStr = path.ToString();
        if (pathStr.Contains("../") || pathStr.Contains("..\\") || pathStr.StartsWith(".."))
        {
            errorMessage = "Path contains traversal sequences";
            return false;
        }

        if (!pathStr.StartsWith(AllowedPrefix, StringComparison.Ordinal))
        {
            errorMessage = $"Path must start with {AllowedPrefix}";
            return false;
        }

        var relativePath = path.ToRelativePath();
        var relativePathStr = relativePath.ToString();

        if (!relativePathStr.StartsWith("NetTextures/", StringComparison.Ordinal))
        {
            errorMessage = "Path escapes allowed directory after normalization";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ставит один проверенный запрос отправки ресурса в ограниченный пул worker'ов серверного контекста.
    /// </summary>
    /// <param name="session">Сессия получателя.</param>
    /// <param name="channel">Канал, с которого поступил запрос.</param>
    /// <param name="resourcePath">Проверенный rooted путь ресурса.</param>
    private void EnqueueResourceSend(ICommonSession session, INetChannel channel, ResPath resourcePath)
    {
        var shouldStartWorker = false;

        lock (_transferQueueLock)
        {
            _pendingTransferRequests.Enqueue(new TransferRequest(session, channel, resourcePath));

            if (_activeTransferWorkers < MaxConcurrentTransferWorkers)
            {
                _activeTransferWorkers++;
                shouldStartWorker = true;
            }
        }

        if (shouldStartWorker)
            _ = ProcessTransferQueueWorker();
    }

    /// <summary>
    /// Обрабатывает очередь запросов передачи в небольшом пуле worker'ов фиксированного размера.
    /// </summary>
    private async Task ProcessTransferQueueWorker()
    {
        var shouldStartWorker = false;

        try
        {
            while (true)
            {
                TransferRequest request;
                lock (_transferQueueLock)
                {
                    if (_pendingTransferRequests.Count == 0)
                        return;

                    request = _pendingTransferRequests.Dequeue();
                }

                try
                {
                    await SendResourceAsync(request);
                }
                catch (Exception ex)
                {
                    if (!IsCurrentConnection(request.Session, request.Channel))
                        continue;

                    _sawmill.Error(
                        $"Unhandled exception while sending NetTextures resource {request.ResourcePath} to {request.Session.Name}: {ex}");
                }
            }
        }
        finally
        {
            lock (_transferQueueLock)
            {
                _activeTransferWorkers--;

                if (_pendingTransferRequests.Count > 0 &&
                    _activeTransferWorkers < MaxConcurrentTransferWorkers)
                {
                    _activeTransferWorkers++;
                    shouldStartWorker = true;
                }
            }

            if (shouldStartWorker)
                _ = ProcessTransferQueueWorker();
        }
    }

    /// <summary>
    /// Отправляет ресурс (файл или директорию) клиенту через High Bandwidth Transfer (WebSocket).
    /// Если путь указывает на директорию (например, .rsi), отправляются все файлы в этой директории.
    /// Если путь указывает на файл, отправляется только этот файл.
    /// </summary>
    /// <param name="request">Запрос, привязанный к исходному подключению получателя.</param>
    private async Task SendResourceAsync(TransferRequest request)
    {
        var session = request.Session;
        var channel = request.Channel;
        var resourcePath = request.ResourcePath;

        if (!IsCurrentConnection(session, channel))
            return;

        var startTime = DateTime.UtcNow;
        _sawmill.Debug($"[NetTextures] Starting transfer of {resourcePath} to {session.Name}");

        IReadOnlyList<TransferResourceEntry> filesToSend;
        try
        {
            filesToSend = await CollectFilesToSendAsync(resourcePath);
        }
        catch (Exception ex)
        {
            if (!IsCurrentConnection(session, channel))
                return;

            _sawmill.Warning($"Failed to collect NetTextures resource {resourcePath} for {session.Name}: {ex.Message}");
            return;
        }

        if (!IsCurrentConnection(session, channel))
            return;

        if (filesToSend.Count == 0)
        {
            _sawmill.Warning($"Resource not found: {resourcePath}");
            return;
        }

        try
        {
            var transferStartTime = DateTime.UtcNow;
            await using var transferStream = _transferManager.StartTransfer(channel,
                new TransferStartInfo
                {
                    MessageKey = TransferKeyNetTextures
                });

            // Открытие упакованного ресурса может синхронно распаковать весь файл.
            await Task.Run(() => WriteFileStream(transferStream, filesToSend));

            if (!IsCurrentConnection(session, channel))
                return;

            var totalTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var transferTime = (DateTime.UtcNow - transferStartTime).TotalMilliseconds;
            long totalSize = 0;
            foreach (var file in filesToSend)
            {
                totalSize += file.Length;
            }

            _sawmill.Info($"[NetTextures] Sent {filesToSend.Count} files ({ByteHelpers.FormatBytes(totalSize)}) via High Bandwidth Transfer to {session.Name} in {transferTime:F0}ms (total: {totalTime:F0}ms)");
        }
        catch (Exception ex)
        {
            if (!IsCurrentConnection(session, channel))
                return;

            _sawmill.Warning($"Failed to send resource via High Bandwidth Transfer to {session.Name}: {ex.Message}");
            var diagnostics = new List<(LogLevel Level, string Message)>();
            await Task.Run(() => SendResourceFallback(session, channel, filesToSend, diagnostics));

            if (!IsCurrentConnection(session, channel))
                return;

            foreach (var (level, message) in diagnostics)
            {
                _sawmill.Log(level, message);
            }
        }
    }

    /// <summary>
    /// Собирает файлы, которые нужно отправить для запрошенного пути ресурса.
    /// Динамические in-memory ресурсы резолвятся сразу, а статические content resources делят in-flight кэш.
    /// </summary>
    /// <param name="resourcePath">Проверенный rooted путь ресурса.</param>
    /// <returns>Упорядоченный список загруженных файлов для передачи.</returns>
    private async Task<IReadOnlyList<TransferResourceEntry>> CollectFilesToSendAsync(ResPath resourcePath)
    {
        var relativeUploadPath = resourcePath.ToRelativePath();

        lock (_dynamicResourcesLock)
        {
            if (_dynamicResources.TryGetValue(relativeUploadPath, out var dynamicData))
                return new[] { TransferResourceEntry.FromMemory(relativeUploadPath, dynamicData) };
        }

        var bundle = await GetOrCreateStaticBundleAsync(resourcePath);
        return bundle?.Files ?? [];
    }

    /// <summary>
    /// Возвращает кэшированный static transfer bundle или собирает его один раз для всех параллельных запросов.
    /// </summary>
    /// <param name="resourcePath">Rooted static content path, запрошенный клиентом.</param>
    /// <returns>Кэшированный bundle или <see langword="null"/>, если ресурс не существует.</returns>
    private async Task<StaticTransferBundle?> GetOrCreateStaticBundleAsync(ResPath resourcePath)
    {
        Task<StaticTransferBundle?> bundleTask;
        List<(LogLevel Level, string Message)>? diagnostics = null;

        lock (_staticBundleLock)
        {
            if (_staticBundles.TryGetValue(resourcePath, out var cached))
                return cached;

            if (!_staticBundleTasks.TryGetValue(resourcePath, out bundleTask!))
            {
                var buildDiagnostics = new List<(LogLevel Level, string Message)>();
                diagnostics = buildDiagnostics;
                bundleTask = Task.Run(() => BuildStaticTransferBundle(resourcePath, buildDiagnostics));
                _staticBundleTasks[resourcePath] = bundleTask;
            }
        }

        try
        {
            var bundle = await bundleTask;
            if (bundle != null)
            {
                lock (_staticBundleLock)
                {
                    _staticBundles.TryAdd(resourcePath, bundle);
                }
            }

            return bundle;
        }
        finally
        {
            lock (_staticBundleLock)
            {
                if (_staticBundleTasks.TryGetValue(resourcePath, out var existingTask) && existingTask == bundleTask)
                    _staticBundleTasks.Remove(resourcePath);
            }

            // Только создатель задачи выводит накопленные сообщения после возврата в серверный контекст.
            if (diagnostics != null)
            {
                foreach (var (level, message) in diagnostics)
                {
                    _sawmill.Log(level, message);
                }
            }
        }
    }

    /// <summary>
    /// Собирает immutable transfer manifest для статического content-файла или директории.
    /// </summary>
    /// <param name="resourcePath">Rooted static content path, запрошенный клиентом.</param>
    /// <param name="diagnostics">Сообщения для последующего вывода в серверном контексте.</param>
    /// <returns>Static transfer bundle или <see langword="null"/>, если ресурс не существует.</returns>
    private StaticTransferBundle? BuildStaticTransferBundle(
        ResPath resourcePath,
        List<(LogLevel Level, string Message)> diagnostics)
    {
        var filesToSend = new List<TransferResourceEntry>();
        var relativeUploadPath = resourcePath.ToRelativePath();
        var files = _resourceManager.ContentFindFiles(resourcePath).ToList();

        if (files.Count == 0)
        {
            if (!_resourceManager.ContentFileExists(resourcePath))
                return null;

            return TryAddContentFile(resourcePath, relativeUploadPath, filesToSend, diagnostics)
                ? new StaticTransferBundle(filesToSend.ToArray())
                : null;
        }

        foreach (var filePath in files)
        {
            if (!filePath.TryRelativeTo(resourcePath, out var relativePath))
                continue;

            TryAddContentFile(filePath, relativeUploadPath / relativePath.Value, filesToSend, diagnostics);
        }

        if (filesToSend.Count == 0)
            return null;

        diagnostics.Add((LogLevel.Debug, $"Collected resource directory {resourcePath} ({filesToSend.Count} files)"));
        return new StaticTransferBundle(filesToSend.ToArray());
    }

    /// <summary>
    /// Добавляет один static content file в transfer manifest.
    /// </summary>
    /// <param name="filePath">Rooted путь файла для добавления.</param>
    /// <param name="relativePath">Относительный путь загрузки, который клиент должен сохранить.</param>
    /// <param name="filesToSend">Целевая коллекция для payload передачи.</param>
    /// <param name="diagnostics">Сообщения для последующего вывода в серверном контексте.</param>
    /// <returns><see langword="true"/>, если файл успешно прочитан.</returns>
    private bool TryAddContentFile(
        ResPath filePath,
        ResPath relativePath,
        List<TransferResourceEntry> filesToSend,
        List<(LogLevel Level, string Message)> diagnostics)
    {
        var relativePathLength = Encoding.UTF8.GetByteCount(relativePath.CanonPath);
        if ((uint) relativePathLength > NetTextureConstants.MaxTransferPathLength)
        {
            diagnostics.Add((LogLevel.Warning,
                $"Skipping NetTexture file with a relative path longer than the supported transfer limit: {relativePath} ({relativePathLength} > {NetTextureConstants.MaxTransferPathLength})"));
            return false;
        }

        if (!_resourceManager.TryContentFileRead(filePath, out var stream))
        {
            diagnostics.Add((LogLevel.Warning, $"Failed to read file: {filePath}"));
            return false;
        }

        using (stream)
        {
            if (stream.Length < 0 || stream.Length > NetTextureConstants.MaxTransferFileSize)
            {
                diagnostics.Add((LogLevel.Warning,
                    $"Skipping NetTexture file larger than the supported transfer size: {filePath} ({stream.Length} > {NetTextureConstants.MaxTransferFileSize})"));
                return false;
            }

            filesToSend.Add(TransferResourceEntry.FromContent(relativePath, filePath, (int) stream.Length));
        }

        return true;
    }

    /// <summary>
    /// Фоновый fallback-метод: отправляет ресурсы обычными сетевыми сообщениями, если WebSocket-передача падает.
    /// </summary>
    /// <param name="session">Сессия получателя.</param>
    /// <param name="channel">Исходный канал подключения получателя.</param>
    /// <param name="files">Файлы, которые еще нужно доставить.</param>
    /// <param name="diagnostics">Сообщения для последующего вывода в серверном контексте.</param>
    private void SendResourceFallback(
        ICommonSession session,
        INetChannel channel,
        IReadOnlyList<TransferResourceEntry> files,
        List<(LogLevel Level, string Message)> diagnostics)
    {
        var chunkCount = 0;

        foreach (var file in files)
        {
            if (file.DynamicData != null)
            {
                foreach (var message in CreateFallbackChunks(file.RelativePath, file.DynamicData))
                {
                    if (!TrySendMessage(session, channel, message))
                        return;

                    chunkCount++;
                }

                continue;
            }

            if (!TrySendContentFileFallback(session, channel, file, diagnostics, out var sentChunks))
                return;

            chunkCount += sentChunks;
        }

        diagnostics.Add((LogLevel.Debug, $"Sent {files.Count} files via fallback ({chunkCount} chunk messages) to {session.Name}"));
    }

    /// <summary>
    /// Отправляет один static content file через fallback chunked transport без полной материализации файла.
    /// </summary>
    /// <param name="session">Сессия получателя.</param>
    /// <param name="channel">Исходный канал подключения получателя.</param>
    /// <param name="file">Дескриптор static file для отправки.</param>
    /// <param name="diagnostics">Сообщения для последующего вывода в серверном контексте.</param>
    /// <param name="chunkCount">Количество chunk messages, отправленных для этого файла.</param>
    /// <returns><see langword="true"/>, если отправка завершена; иначе <see langword="false"/>, если сессия отключилась.</returns>
    private bool TrySendContentFileFallback(
        ICommonSession session,
        INetChannel channel,
        TransferResourceEntry file,
        List<(LogLevel Level, string Message)> diagnostics,
        out int chunkCount)
    {
        chunkCount = 0;

        if (file.ContentPath == null)
            return true;

        if (!_resourceManager.TryContentFileRead(file.ContentPath.Value, out var stream))
        {
            diagnostics.Add((LogLevel.Warning, $"Failed to read fallback NetTexture file: {file.ContentPath.Value}"));
            return true;
        }

        using (stream)
        {
            var totalChunks = Math.Max(1, (file.Length + NetTextureConstants.MaxChunkSize - 1) / NetTextureConstants.MaxChunkSize);

            for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                var offset = chunkIndex * NetTextureConstants.MaxChunkSize;
                var chunkLength = Math.Min(NetTextureConstants.MaxChunkSize, file.Length - offset);
                var chunkData = new byte[chunkLength];
                ReadExactly(stream, chunkData, chunkLength);

                if (!TrySendMessage(session, channel, new NetTextureResourceChunkMessage
                {
                    RelativePath = file.RelativePath.ToString(),
                    ChunkIndex = chunkIndex,
                    TotalChunks = totalChunks,
                    ChunkOffset = offset,
                    TotalLength = file.Length,
                    Data = chunkData
                }))
                {
                    return false;
                }

                chunkCount++;
            }

            return true;
        }
    }

    internal static bool IsCurrentConnection(ICommonSession session, INetChannel channel)
    {
        return ReferenceEquals(session.Channel, channel) &&
               (session.Status is SessionStatus.Connected or SessionStatus.InGame) &&
               channel.IsConnected;
    }

    private static bool TrySendMessage(ICommonSession session, INetChannel channel, NetMessage message)
    {
        if (!IsCurrentConnection(session, channel))
            return false;

        try
        {
            channel.SendMessage(message);
            return true;
        }
        catch (InvalidOperationException) when (!IsCurrentConnection(session, channel))
        {
            return false;
        }
    }

    /// <summary>
    /// Делит один загруженный файл на упорядоченные chunk messages для fallback transport path.
    /// </summary>
    /// <param name="relativePath">Относительный клиентский путь загрузки файла.</param>
    /// <param name="data">Сырые байты файла для разделения.</param>
    /// <param name="chunkSize">Целевой максимальный размер chunk в байтах.</param>
    /// <returns>Последовательность chunk, необходимая для восстановления файла на клиенте.</returns>
    internal static IEnumerable<NetTextureResourceChunkMessage> CreateFallbackChunks(
        ResPath relativePath,
        byte[] data,
        int chunkSize = NetTextureConstants.MaxChunkSize)
    {
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        var totalChunks = Math.Max(1, (data.Length + chunkSize - 1) / chunkSize);

        for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            var offset = chunkIndex * chunkSize;
            var chunkLength = Math.Min(chunkSize, data.Length - offset);
            var chunkData = new byte[chunkLength];
            Buffer.BlockCopy(data, offset, chunkData, 0, chunkLength);

            yield return new NetTextureResourceChunkMessage
            {
                RelativePath = relativePath.ToString(),
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks,
                ChunkOffset = offset,
                TotalLength = data.Length,
                Data = chunkData
            };
        }
    }

    /// <summary>
    /// Записывает файлы в stream передачи в том же формате, что и SharedNetworkResourceManager.
    /// Формат: [pathLength: uint32][dataLength: uint32][path: bytes][data: bytes][continue: byte]...
    /// </summary>
    /// <param name="stream">Writable stream передачи.</param>
    /// <param name="files">Файлы для кодирования в stream передачи.</param>
    private async Task WriteFileStream(Stream stream, IReadOnlyList<TransferResourceEntry> files)
    {
        var continueByte = new byte[1];
        var buffer = ArrayPool<byte>.Shared.Rent(NetTextureConstants.MaxChunkSize);
        var headerBuffer = Array.Empty<byte>();

        try
        {
            var first = true;

            foreach (var file in files)
            {
                if (!first)
                {
                    continueByte[0] = 1;
                    await stream.WriteAsync(continueByte).ConfigureAwait(false);
                }

                first = false;

                var pathBytes = file.PathBytes;
                var requiredHeaderLength = 8 + pathBytes.Length;
                if (headerBuffer.Length < requiredHeaderLength)
                {
                    if (headerBuffer.Length != 0)
                        ArrayPool<byte>.Shared.Return(headerBuffer);

                    headerBuffer = ArrayPool<byte>.Shared.Rent(requiredHeaderLength);
                }

                BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(0, 4), (uint) pathBytes.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(4, 4), (uint) file.Length);
                Array.Copy(pathBytes, 0, headerBuffer, 8, pathBytes.Length);
                await stream.WriteAsync(headerBuffer.AsMemory(0, requiredHeaderLength)).ConfigureAwait(false);
                await WriteTransferData(stream, file, buffer).ConfigureAwait(false);
            }

            continueByte[0] = 0;
            await stream.WriteAsync(continueByte).ConfigureAwait(false);
        }
        finally
        {
            if (headerBuffer.Length != 0)
                ArrayPool<byte>.Shared.Return(headerBuffer);

            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Записывает payload одного файла передачи в HBT stream, прокидывая static content files через pooled buffer.
    /// </summary>
    /// <param name="stream">Целевой stream передачи.</param>
    /// <param name="file">Дескриптор файла передачи для записи.</param>
    /// <param name="buffer">Pooled copy buffer для static files.</param>
    private async Task WriteTransferData(Stream stream, TransferResourceEntry file, byte[] buffer)
    {
        if (file.DynamicData != null)
        {
            await stream.WriteAsync(file.DynamicData).ConfigureAwait(false);
            return;
        }

        if (file.ContentPath == null)
            throw new InvalidOperationException($"NetTextures transfer file {file.RelativePath} has no content source");

        using var contentStream = _resourceManager.ContentFileRead(file.ContentPath.Value);
        while (true)
        {
            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            if (read == 0)
                break;

            await stream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Читает из content stream ровно запрошенное количество байт.
    /// </summary>
    /// <param name="stream">Входной stream.</param>
    /// <param name="buffer">Целевой буфер.</param>
    /// <param name="count">Количество байт, которое нужно прочитать.</param>
    private static void ReadExactly(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read == 0)
                throw new InvalidDataException("Unexpected end of NetTextures content stream");

            offset += read;
        }
    }

    /// <summary>
    /// Регистрирует динамическую in-memory сетевую текстуру, которой нет на диске.
    /// resourcePath должен указывать внутрь /NetTextures/ и будет проверен через <see cref="ValidateResourcePath"/>.
    /// </summary>
    /// <param name="resourcePath">Rooted путь ресурса, например "/NetTextures/Messenger/photo_123.png".</param>
    /// <param name="data">Сырые байты файла (PNG, WEBP и т. п.).</param>
    public void RegisterDynamicResource(string resourcePath, byte[] data)
    {
        var path = resourcePath.StartsWith("/")
            ? new ResPath(resourcePath)
            : new ResPath("/") / resourcePath;

        path = path.Clean();

        if (!ValidateResourcePath(path, out var error))
        {
            _sawmill.Warning($"Failed to register dynamic NetTexture {resourcePath}: {error}");
            return;
        }

        var relativeUploadPath = path.ToRelativePath();
        lock (_dynamicResourcesLock)
        {
            _dynamicResources[relativeUploadPath] = data;
        }

        _sawmill.Debug($"Registered dynamic NetTexture resource: {relativeUploadPath}");
    }

    /// <summary>
    /// Удаляет регистрацию динамической in-memory сетевой текстуры.
    /// </summary>
    /// <param name="resourcePath">Rooted путь ресурса, например "/NetTextures/Messenger/photo_123.png".</param>
    public void UnregisterDynamicResource(string resourcePath)
    {
        var path = resourcePath.StartsWith('/')
            ? new ResPath(resourcePath)
            : new ResPath("/") / resourcePath;

        path = path.Clean();
        var relativeUploadPath = path.ToRelativePath();

        lock (_dynamicResourcesLock)
        {
            if (_dynamicResources.Remove(relativeUploadPath))
            {
                _sawmill.Debug($"Unregistered dynamic NetTexture resource: {relativeUploadPath}");
            }
        }
    }

    /// <summary>
    /// Один файл, запланированный к передаче клиенту.
    /// </summary>
    private sealed class TransferRequest(ICommonSession session, INetChannel channel, ResPath resourcePath)
    {
        public ICommonSession Session { get; } = session;
        public INetChannel Channel { get; } = channel;
        public ResPath ResourcePath { get; } = resourcePath;
    }

    /// <summary>
    /// Immutable manifest для static content resource, чтобы параллельные запросы делили один план передачи.
    /// </summary>
    private sealed class StaticTransferBundle(TransferResourceEntry[] files)
    {
        public TransferResourceEntry[] Files { get; } = files;
    }

    private sealed class TransferResourceEntry(ResPath relativePath, int length, ResPath? contentPath, byte[]? dynamicData, byte[] pathBytes)
    {
        public ResPath RelativePath { get; } = relativePath;
        public int Length { get; } = length;
        public ResPath? ContentPath { get; } = contentPath;
        public byte[]? DynamicData { get; } = dynamicData;
        public byte[] PathBytes { get; } = pathBytes;

        public static TransferResourceEntry FromContent(ResPath relativePath, ResPath contentPath, int length)
        {
            return new TransferResourceEntry(relativePath, length, contentPath, null, Encoding.UTF8.GetBytes(relativePath.CanonPath));
        }

        public static TransferResourceEntry FromMemory(ResPath relativePath, byte[] data)
        {
            return new TransferResourceEntry(relativePath, data.Length, null, data, Encoding.UTF8.GetBytes(relativePath.CanonPath));
        }
    }
}
