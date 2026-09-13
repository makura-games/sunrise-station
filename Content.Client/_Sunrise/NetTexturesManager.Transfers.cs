using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Content.Shared._Sunrise.NetTextures;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Upload;
using Robust.Shared.Utility;
using ByteHelpers = Robust.Shared.Utility.ByteHelpers;

namespace Content.Client._Sunrise;

public sealed partial class NetTexturesManager
{
    #region Transfer Intake
    /// <summary>
    /// Запускает асинхронную обработку high-bandwidth передачи NetTextures.
    /// </summary>
    /// <remarks>
    /// Callback передачи может выполниться в чувствительное к подключению окно, поэтому stream передается
    /// фоновому worker'у до начала разбора файлов.
    /// </remarks>
    /// <param name="transfer">Полезная нагрузка передачи, полученная от сервера.</param>
    private void ReceiveNetTexturesTransfer(TransferReceivedEvent transfer)
    {
        var generation = ReadSessionGeneration();

        _ = Task.Run(() => ReceiveNetTexturesTransferWorker(transfer.DataStream, generation));
    }

#pragma warning disable CS0618
    /// <summary>
    /// Принимает legacy whole-file fallback message и публикует его через обычный клиентский pipeline.
    /// </summary>
    /// <param name="message">Legacy upload message с одним файлом ресурса.</param>
    private void ReceiveFallbackUpload(NetworkResourceUploadMessage message)
#pragma warning restore CS0618
    {
        var files = new List<(ResPath Relative, byte[] Data)>(1)
        {
            (message.RelativePath, message.Data)
        };

        PublishFiles(files);
    }

    /// <summary>
    /// Принимает один chunk из chunked fallback transport и собирает его в полный загруженный файл.
    /// </summary>
    /// <remarks>
    /// Сборки ключуются по нормализованному относительному пути и сбрасываются при сбросе сессии, поэтому частичное
    /// fallback-состояние не попадает в следующую попытку переподключения.
    /// </remarks>
    /// <param name="message">Входящий fallback chunk.</param>
    internal void ReceiveFallbackChunk(NetTextureResourceChunkMessage message)
    {
        var totalChunks = message.TotalChunks;
        var chunkIndex = message.ChunkIndex;
        var totalLength = message.TotalLength;
        var chunkOffset = message.ChunkOffset;
        var chunkLength = message.Data.Length;
        var chunkEnd = (long) chunkOffset + chunkLength;

        if (totalChunks <= 0 ||
            totalChunks > NetTextureConstants.MaxFallbackChunkCount ||
            chunkIndex < 0 ||
            chunkIndex >= totalChunks)
        {
            _sawmill.Warning(
                $"Rejected malformed NetTextures fallback chunk for {message.RelativePath}: chunk {(long) chunkIndex + 1}/{totalChunks}, limit {NetTextureConstants.MaxFallbackChunkCount}");
            return;
        }

        if (totalLength < 0 || (uint) totalLength > NetTextureConstants.MaxTransferFileSize)
        {
            _sawmill.Warning(
                $"Rejected malformed NetTextures fallback chunk length for {message.RelativePath}: total {totalLength}, limit {NetTextureConstants.MaxTransferFileSize}");
            return;
        }

        if (chunkLength > NetTextureConstants.MaxChunkSize ||
            chunkOffset < 0 ||
            chunkLength > totalLength ||
            chunkEnd > totalLength)
        {
            _sawmill.Warning(
                $"Rejected malformed NetTextures fallback chunk bounds for {message.RelativePath}: offset {chunkOffset}, length {chunkLength}, total {totalLength}");
            return;
        }

        var relativePath = new ResPath(message.RelativePath).Clean().ToRelativePath();

        if (!_fallbackChunkAssemblies.TryGetValue(relativePath, out var assembly) ||
            assembly.TotalChunks != totalChunks ||
            assembly.TotalLength != totalLength)
        {
            assembly?.Dispose();
            assembly = new FallbackChunkAssembly(totalChunks, totalLength);
            _fallbackChunkAssemblies[relativePath] = assembly;
        }

        assembly.StoreChunk(chunkIndex, chunkOffset, message.Data);

        if (!assembly.IsComplete)
            return;

        _fallbackChunkAssemblies.Remove(relativePath);
        try
        {
            var files = new List<(ResPath Relative, byte[] Data)>(1)
            {
                (relativePath, assembly.TakeCompletedData())
            };

            PublishFiles(files);
        }
        finally
        {
            assembly.Dispose();
        }
    }

    /// <summary>
    /// Публикует полученные сырые файлы в смонтированный in-memory корень загрузок и обновляет ожидающих потребителей.
    /// </summary>
    /// <param name="files">Файлы для публикации под <c>/Uploaded</c>.</param>
    internal void PublishFiles(List<(ResPath Relative, byte[] Data)> files)
    {
        PublishFiles(files, updatePendingResources: true);
    }

    /// <summary>
    /// Публикует полученные сырые файлы в смонтированный in-memory корень загрузок и опционально обновляет ожидающих потребителей.
    /// </summary>
    /// <param name="files">Файлы для публикации под <c>/Uploaded</c>.</param>
    /// <param name="updatePendingResources">Нужно ли сразу перепроверить готовность ожидающих ресурсов.</param>
    internal void PublishFiles(List<(ResPath Relative, byte[] Data)> files, bool updatePendingResources)
    {
        foreach (var (relative, data) in files)
        {
            _sawmill.Verbose($"Storing NetTexture: {relative} ({ByteHelpers.FormatBytes(data.Length)})");
            _netTexturesContentRoot.AddOrUpdateFile(relative, data);
            TrackPublishedFile(relative, data);
            _failedResources.Remove(GetUploadedResourcePath(relative));
        }

        if (updatePendingResources)
            UpdatePendingResources();
    }
    #endregion

    #region Transfer Workers
    /// <summary>
    /// Разбирает stream передачи на worker thread и возвращает публикацию на main thread.
    /// </summary>
    /// <param name="stream">Stream передачи, возвращенный HBT subsystem.</param>
    /// <param name="generation">Поколение сессии, зафиксированное при старте передачи.</param>
    private void ReceiveNetTexturesTransferWorker(Stream stream, int generation)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            using (stream)
            {
                var (fileCount, totalSize) = ReadTransferStream(stream, generation);
                if (fileCount > 0)
                    _taskManager.RunOnMainThread(() => ProcessPendingTransferBatches(0f));

                var totalTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _sawmill.Info($"[NetTextures] Received {fileCount} files ({ByteHelpers.FormatBytes(totalSize)}) via transfer in {totalTime:F0}ms");
            }
        }
        catch (Exception e)
        {
            if (generation == ReadSessionGeneration())
                _sawmill.Error($"Error while receiving NetTextures transfer: {e}");
        }
    }
    #endregion

    #region Transfer Parsing
    /// <summary>
    /// Читает stream передачи NetTextures в пакеты публикации, которые можно постепенно обработать на main thread.
    /// </summary>
    /// <param name="stream">Stream передачи для разбора.</param>
    /// <param name="generation">Поколение сессии, зафиксированное при старте передачи.</param>
    /// <returns>Общее число файлов и общий размер в байтах, прочитанные из stream.</returns>
    private (int FileCount, long TotalBytes) ReadTransferStream(Stream stream, int generation)
    {
        var files = new List<(ResPath Relative, byte[] Data)>();
        var lengthBytes = new byte[4];
        var continueByte = new byte[1];
        var totalSize = 0L;
        var totalFiles = 0;
        var batchBytes = 0;

        while (true)
        {
            ReadExactly(stream, lengthBytes);
            var pathLength = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
            if (pathLength > int.MaxValue)
                throw new InvalidDataException($"NetTextures transfer path length is too large: {pathLength}");
            if (pathLength > NetTextureConstants.MaxTransferPathLength)
                throw new InvalidDataException($"NetTextures transfer path length exceeds protocol limit: {pathLength}");

            ReadExactly(stream, lengthBytes);
            var dataLength = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
            if (dataLength > int.MaxValue)
                throw new InvalidDataException($"NetTextures transfer file length is too large: {dataLength}");
            if (dataLength > NetTextureConstants.MaxTransferFileSize)
                throw new InvalidDataException($"NetTextures transfer file length exceeds protocol limit: {dataLength}");
            if ((ulong) pathLength + dataLength > NetTextureConstants.MaxTransferPayloadLength)
            {
                throw new InvalidDataException(
                    $"NetTextures transfer entry payload exceeds protocol limit: path={pathLength}, data={dataLength}");
            }

            var pathData = new byte[(int) pathLength];
            ReadExactly(stream, pathData);

            var data = new byte[(int) dataLength];
            ReadExactly(stream, data);

            files.Add((new ResPath(Encoding.UTF8.GetString(pathData)), data));
            totalFiles++;
            totalSize += data.Length;
            batchBytes += data.Length;

            if (batchBytes >= MaxTransferPublishBudgetBytes)
            {
                EnqueueTransferPublishBatch(new TransferPublishBatch(generation, files, batchBytes));
                files = new List<(ResPath Relative, byte[] Data)>();
                batchBytes = 0;
            }

            ReadExactly(stream, continueByte);
            if (continueByte[0] == 0)
                break;
        }

        if (files.Count > 0)
            EnqueueTransferPublishBatch(new TransferPublishBatch(generation, files, batchBytes));

        return (totalFiles, totalSize);
    }

    /// <summary>
    /// Читает ровно столько байт, сколько требуется одному полю передачи.
    /// </summary>
    /// <param name="stream">Входной stream.</param>
    /// <param name="buffer">Целевой буфер, который нужно заполнить полностью.</param>
    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
                throw new InvalidDataException("Unexpected end of NetTextures transfer stream");

            offset += read;
        }
    }
    #endregion

    #region Transfer Publication
    /// <summary>
    /// Ставит один разобранный пакет передачи в очередь для последующей публикации на main thread.
    /// </summary>
    /// <param name="batch">Разобранный пакет передачи.</param>
    private void EnqueueTransferPublishBatch(TransferPublishBatch batch)
    {
        lock (_pendingTransferBatches)
        {
            _pendingTransferBatches.Enqueue(batch);
        }
    }

    /// <summary>
    /// Публикует ограниченный объем уже разобранных данных передачи на main thread.
    /// </summary>
    /// <param name="frameTime">Текущий frame time, используемый для расчета бюджета публикации на кадр.</param>
    private void ProcessPendingTransferBatches(float frameTime)
    {
        var budgetRemaining = Math.Clamp(
            (int) (frameTime * TransferPublishBytesPerSecond),
            MinTransferPublishBudgetBytes,
            MaxTransferPublishBudgetBytes);
        var publishedAny = false;

        while (true)
        {
            TransferPublishBatch? batch;
            lock (_pendingTransferBatches)
            {
                if (_pendingTransferBatches.Count == 0 || (publishedAny && budgetRemaining <= 0))
                    break;

                batch = _pendingTransferBatches.Dequeue();
            }

            if (batch.Generation != ReadSessionGeneration())
                continue;

            PublishFiles(batch.Files, updatePendingResources: false);
            budgetRemaining -= Math.Max(1, batch.TotalBytes);
            publishedAny = true;
        }

        if (publishedAny)
            UpdatePendingResources();
    }
    #endregion

    #region Completeness Tracking
    /// <summary>
    /// Обновляет инкрементальное состояние полноты загруженных RSI-ресурсов по мере прихода отдельных файлов.
    /// </summary>
    /// <param name="relativePath">Относительный путь загруженного файла.</param>
    /// <param name="data">Байты загруженного файла.</param>
    private void TrackPublishedFile(ResPath relativePath, byte[] data)
    {
        if (!TryGetRsiFile(relativePath, out var rsiRelativePath, out var fileName))
            return;

        if (!_rsiCompleteness.TryGetValue(rsiRelativePath, out var completeness))
        {
            completeness = new RsiCompletenessEntry();
            _rsiCompleteness[rsiRelativePath] = completeness;
        }

        completeness.MarkPresent(fileName);

        if (!fileName.Equals("meta.json", StringComparison.Ordinal))
            return;

        try
        {
            using var metaStream = new MemoryStream(data, writable: false);
            completeness.SetMetadata(LoadRsiMetadata(metaStream));
        }
        catch (Exception ex)
        {
            if (!IsHandledRsiMetadataException(ex))
                throw;

            var reason = $"Failed to parse RSI metadata for {rsiRelativePath}: {ex.Message}";
            _sawmill.Debug(reason);
            completeness.SetMetadataFailure(reason);
        }
    }

    /// <summary>
    /// Определяет, принадлежит ли загруженный файл к RSI-директории, и возвращает его локальное имя в директории.
    /// </summary>
    /// <param name="relativePath">Относительный путь загруженного файла.</param>
    /// <param name="rsiRelativePath">Путь загруженной RSI-директории, когда метод возвращает <see langword="true"/>.</param>
    /// <param name="fileName">Имя файла внутри RSI-директории.</param>
    /// <returns><see langword="true"/>, если файл принадлежит RSI-директории.</returns>
    private static bool TryGetRsiFile(ResPath relativePath, out ResPath rsiRelativePath, out string fileName)
    {
        fileName = relativePath.Filename;
        rsiRelativePath = relativePath.Directory.ToRelativePath();

        if (fileName == "." || !IsRsiPath(rsiRelativePath.ToRootedPath()))
        {
            rsiRelativePath = default;
            fileName = string.Empty;
            return false;
        }

        return true;
    }
    #endregion
}
