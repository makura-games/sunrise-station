using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._Sunrise;

public sealed partial class NetTexturesManager
{
    #region Upload Completion
    private const int MinUploadBudgetBytes = 512 * 1024;
    private const int MaxUploadBudgetBytes = 8 * 1024 * 1024;
    private const int UploadBytesPerSecond = 96 * 1024 * 1024;

    /// <summary>
    /// Коммитит полностью загруженную текстуру в карту готовых ресурсов и уведомляет слушателей.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса.</param>
    /// <param name="loadedTexture">Запись загруженной текстуры.</param>
    private void FinishPreparedTexture(string resourceKey, LoadedTextureEntry loadedTexture)
    {
        _preparingResources.Remove(resourceKey);
        _pendingResources.Remove(resourceKey);

        // Не освобождаем заменяемую запись явно: её Texture могла быть передана внешнему UI.
        _loadedTextures[resourceKey] = loadedTexture;
        ResourceLoaded?.Invoke(resourceKey);
    }

    /// <summary>
    /// Коммитит полностью загруженный набор RSI-анимаций в карту готовых ресурсов и уведомляет слушателей.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса.</param>
    /// <param name="loadedRsi">Запись загруженного RSI.</param>
    private void FinishPreparedRsi(string resourceKey, LoadedRsiEntry loadedRsi)
    {
        _preparingResources.Remove(resourceKey);
        _pendingResources.Remove(resourceKey);

        // Не освобождаем заменяемую запись явно: её состояния могли быть переданы внешнему UI.
        _loadedRsis[resourceKey] = loadedRsi;
        ResourceLoaded?.Invoke(resourceKey);
    }

    /// <summary>
    /// Обрабатывает ограниченное количество этапов staged GPU upload за текущий кадр.
    /// </summary>
    /// <remarks>
    /// Работа загрузки намеренно распределяется по кадрам, чтобы большие RSI-ресурсы не монополизировали главный поток.
    /// </remarks>
    private void ProcessPreparedUploads(float frameTime)
    {
        var budgetRemaining = Math.Clamp(
            (int) (frameTime * UploadBytesPerSecond),
            MinUploadBudgetBytes,
            MaxUploadBudgetBytes);
        var processedAny = false;

        while (_preparedUploads.Count > 0 && (!processedAny || budgetRemaining > 0))
        {
            var upload = _preparedUploads.Peek();

            if (upload.Generation != ReadSessionGeneration())
            {
                _preparedUploads.Dequeue().EnqueueDisposals(_deferredDisposals);
                continue;
            }

            try
            {
                var estimatedCost = Math.Max(1, upload.EstimateStepCostBytes(this));
                var completed = upload.ProcessStep(this, _sessionCts.Token);
                budgetRemaining -= estimatedCost;
                processedAny = true;

                if (!completed)
                    continue;

                upload.Commit(this);
                _preparedUploads.Dequeue();
            }
            catch (OperationCanceledException)
            {
                _preparedUploads.Dequeue().EnqueueDisposals(_deferredDisposals);
                _preparingResources.Remove(upload.ResourceKey);
            }
            catch (Exception ex)
            {
                _preparedUploads.Dequeue().EnqueueDisposals(_deferredDisposals);
                MarkResourceFailed(upload.ResourceKey, ex.Message);
            }
        }
    }

    /// <summary>
    /// Записывает сбой ресурса и не дает считать ресурс готовым.
    /// </summary>
    /// <param name="resourceKey">Нормализованный ключ ресурса, завершившегося с ошибкой.</param>
    /// <param name="reason">Причина сбоя для логирования.</param>
    private void MarkResourceFailed(string resourceKey, string reason)
    {
        _preparingResources.Remove(resourceKey);
        _pendingResources.Remove(resourceKey);
        _failedResources.Add(resourceKey);
        _sawmill.Warning($"Failed to prepare NetTexture {resourceKey}: {reason}");
    }
    #endregion

    #region Resource Decoding
    /// <summary>
    /// Проверяет, что загруженная RSI-директория содержит все файлы для безопасного декодирования.
    /// </summary>
    /// <param name="relativePath">Относительный путь загруженного RSI.</param>
    /// <returns><see langword="true"/>, если все требуемые RSI-файлы присутствуют.</returns>
    private bool CheckRsiFilesComplete(ResPath relativePath)
    {
        if (_rsiCompleteness.TryGetValue(relativePath, out var cached))
        {
            if (cached.MetadataFailureReason != null)
                throw new InvalidDataException(cached.MetadataFailureReason);

            if (cached.HasMetadata)
                return cached.IsComplete;
        }

        var uploadedPath = (new ResPath(UploadedPrefix) / relativePath).ToRootedPath();
        var metaPath = (uploadedPath / "meta.json").ToRootedPath();

        if (!_resourceManager.TryContentFileRead(metaPath, out var metaStream))
            return false;

        using (metaStream)
        {
            RsiMetadataData metadata;
            try
            {
                metadata = LoadRsiMetadata(metaStream);
            }
            catch (Exception ex)
            {
                if (!IsHandledRsiMetadataException(ex))
                    throw;

                _sawmill.Debug($"Failed to parse RSI metadata while checking completeness for {relativePath}: {ex}");
                throw new InvalidDataException($"Failed to parse RSI metadata for {relativePath}", ex);
            }

            if (metadata.States.Length == 0)
                return false;

            var completeness = new RsiCompletenessEntry();
            completeness.MarkPresent("meta.json");
            completeness.SetMetadata(metadata);

            foreach (var state in metadata.States)
            {
                if (string.IsNullOrWhiteSpace(state.Name))
                    return false;

                var pngPath = (uploadedPath / $"{state.Name}.png").ToRootedPath();
                if (!_resourceManager.ContentFileExists(pngPath))
                    return false;

                completeness.MarkPresent($"{state.Name}.png");
            }

            _rsiCompleteness[relativePath] = completeness;
        }

        return true;
    }

    /// <summary>
    /// Декодирует загруженное не-RSI изображение в промежуточную полезную нагрузку текстуры.
    /// </summary>
    /// <param name="resourcePath">Нормализованный путь загруженного ресурса.</param>
    /// <param name="cancellationToken">Текущий токен отмены сессии.</param>
    /// <returns>Декодированная полезная нагрузка текстуры, готовая для staged upload.</returns>
    private PreparedTexture DecodeTexture(ResPath resourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uploadedPath = GetUploadedPath(resourcePath.ToString());
        using var stream = _resourceManager.ContentFileRead(uploadedPath);
        var image = Image.Load<Rgba32>(stream);
        return new PreparedTexture(image);
    }

    /// <summary>
    /// Декодирует загруженную RSI-директорию в покадровые полезные нагрузки изображений, готовые для staged upload.
    /// </summary>
    /// <remarks>
    /// Путь декодирования проверяет metadata, размеры изображения, ссылки на кадры и количество направлений до того,
    /// как загруженное состояние становится доступно потребителям.
    /// </remarks>
    /// <param name="resourcePath">Нормализованный путь загруженного RSI.</param>
    /// <param name="cancellationToken">Текущий токен отмены сессии.</param>
    /// <returns>Подготовленная полезная нагрузка RSI, готовая для staged upload.</returns>
    private PreparedRsi DecodeRsi(ResPath resourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uploadedPath = GetUploadedPath(resourcePath.ToString());
        var metaPath = (uploadedPath / "meta.json").ToRootedPath();

        using var metaStream = _resourceManager.ContentFileRead(metaPath);
        var metadata = LoadRsiMetadata(metaStream);

        if (metadata.States.Length == 0)
            throw new InvalidDataException($"RSI metadata for {resourcePath} is incomplete");

        var frameSize = metadata.Size;
        if (frameSize.X <= 0 || frameSize.Y <= 0)
            throw new InvalidDataException($"RSI metadata for {resourcePath} has invalid frame size {frameSize}");

        var states = new List<PreparedRsiState>(metadata.States.Length);
        foreach (var state in metadata.States)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(state.Name))
                throw new InvalidDataException($"RSI metadata for {resourcePath} contains an empty state name");

            var dirCount = state.Directions ?? 1;
            if (dirCount is not (1 or 4 or 8))
                throw new InvalidDataException($"RSI state {state.Name} in {resourcePath} has invalid direction count {dirCount}");

            var delays = NormalizeDelays(state.Delays, dirCount);
            var (foldedDelays, foldedIndices) = FoldDelays(delays);

            var pngPath = (uploadedPath / $"{state.Name}.png").ToRootedPath();
            using var stateStream = _resourceManager.ContentFileRead(pngPath);
            using var image = Image.Load<Rgba32>(stateStream);

            if (image.Width % frameSize.X != 0 || image.Height % frameSize.Y != 0)
                throw new InvalidDataException($"RSI state {state.Name} in {resourcePath} has invalid image size {image.Width}x{image.Height}");

            var sourceColumns = image.Width / frameSize.X;
            var sourceRows = image.Height / frameSize.Y;
            var sourceFrameCount = sourceColumns * sourceRows;

            var uniqueIndices = new HashSet<int>();
            var frames = new List<PreparedRsiFrame>();

            foreach (var dirIndices in foldedIndices)
            {
                foreach (var index in dirIndices)
                {
                    if (!uniqueIndices.Add(index))
                        continue;

                    if (index < 0 || index >= sourceFrameCount)
                        throw new InvalidDataException($"RSI state {state.Name} in {resourcePath} references frame {index}, but only has {sourceFrameCount} frames");

                    var column = index % sourceColumns;
                    var row = index / sourceColumns;
                    var frameBounds = new Rectangle(column * frameSize.X, row * frameSize.Y, frameSize.X, frameSize.Y);
                    frames.Add(new PreparedRsiFrame(index, image.Clone(ctx => ctx.Crop(frameBounds))));
                }
            }

            states.Add(new PreparedRsiState(state.Name, dirCount, foldedDelays, foldedIndices, frames));
        }

        return new PreparedRsi(metadata.LoadParameters, states);
    }
    #endregion
}
