using System.Collections.Generic;
using System.Threading;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Sunrise;

public sealed partial class NetTexturesManager
{
    #region Loaded Resources
    private sealed class LoadedTextureEntry(OwnedTexture texture) : IDisposable
    {
        public readonly OwnedTexture Texture = texture;

        public void Dispose()
        {
            Texture.Dispose();
        }

        public void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            disposals.Enqueue(Texture);
        }
    }

    private sealed class LoadedRsiEntry(List<OwnedTexture> textures, Dictionary<string, NetTextureAnimationState> states) : IDisposable
    {
        public readonly Dictionary<string, NetTextureAnimationState> States = states;

        public void Dispose()
        {
            foreach (var texture in textures)
            {
                texture.Dispose();
            }
        }

        public void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            foreach (var texture in textures)
            {
                disposals.Enqueue(texture);
            }

            textures.Clear();
        }
    }
    #endregion

    #region Preparation Payloads
    private sealed class PreparedTexture(Image<Rgba32> image) : IDisposable
    {
        public Image<Rgba32> Image { get; private set; } = image;

        public void Dispose()
        {
            Image?.Dispose();
            Image = null!;
        }
    }

    private abstract class PreparedUploadJob(string resourceKey, int generation) : IDisposable
    {
        public string ResourceKey { get; } = resourceKey;
        public int Generation { get; } = generation;

        public abstract int EstimateStepCostBytes(NetTexturesManager manager);
        public abstract bool ProcessStep(NetTexturesManager manager, CancellationToken cancellationToken);
        public abstract void Commit(NetTexturesManager manager);
        public abstract void EnqueueDisposals(Queue<IDisposable> disposals);
        public abstract void Dispose();
    }

    private sealed class PreparedTextureUploadJob(string resourcePath, int generation, PreparedTexture prepared)
        : PreparedUploadJob(resourcePath, generation)
    {
        private const int UploadTileSize = 1024;

        private PreparedTexture? _prepared = prepared;
        private OwnedTexture? _texture;
        private LoadedTextureEntry? _loadedTexture;
        private Rgba32[]? _tileBuffer;
        private int _nextTileX;
        private int _nextTileY;

        public override int EstimateStepCostBytes(NetTexturesManager manager)
        {
            var prepared = _prepared;
            if (prepared == null)
                return 0;

            var remainingWidth = prepared.Image.Width - _nextTileX;
            var remainingHeight = prepared.Image.Height - _nextTileY;
            if (remainingWidth <= 0 || remainingHeight <= 0)
                return 0;

            var tileWidth = Math.Min(UploadTileSize, remainingWidth);
            var tileHeight = Math.Min(UploadTileSize, remainingHeight);
            return tileWidth * tileHeight * 4;
        }

        public override bool ProcessStep(NetTexturesManager manager, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prepared = _prepared ?? throw new InvalidOperationException($"Texture upload job for {ResourceKey} has no prepared image");

            _texture ??= manager._clyde.CreateBlankTexture<Rgba32>(
                (prepared.Image.Width, prepared.Image.Height),
                ResourceKey);

            var tileWidth = Math.Min(UploadTileSize, prepared.Image.Width - _nextTileX);
            var tileHeight = Math.Min(UploadTileSize, prepared.Image.Height - _nextTileY);
            var tilePixelCount = tileWidth * tileHeight;
            if (_tileBuffer == null || _tileBuffer.Length < tilePixelCount)
                _tileBuffer = new Rgba32[tilePixelCount];

            CopyTextureTile(prepared.Image, _nextTileX, _nextTileY, tileWidth, tileHeight, _tileBuffer);
            _texture.SetSubImage(
                new Vector2i(_nextTileX, _nextTileY),
                new Vector2i(tileWidth, tileHeight),
                _tileBuffer.AsSpan(0, tilePixelCount));

            _nextTileX += tileWidth;
            if (_nextTileX < prepared.Image.Width)
                return false;

            _nextTileX = 0;
            _nextTileY += tileHeight;
            if (_nextTileY < prepared.Image.Height)
                return false;

            _loadedTexture = new LoadedTextureEntry(_texture);
            _texture = null;

            prepared.Dispose();
            _prepared = null;
            return true;
        }

        public override void Commit(NetTexturesManager manager)
        {
            if (_loadedTexture == null)
                throw new InvalidOperationException($"Texture upload job for {ResourceKey} completed without a loaded texture");

            manager.FinishPreparedTexture(ResourceKey, _loadedTexture);
            _loadedTexture = null;
        }

        public override void Dispose()
        {
            _prepared?.Dispose();
            _prepared = null;

            _texture?.Dispose();
            _texture = null;
            _tileBuffer = null;

            _loadedTexture?.Dispose();
            _loadedTexture = null;
        }

        public override void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            if (_prepared != null)
            {
                disposals.Enqueue(_prepared);
                _prepared = null;
            }

            if (_texture != null)
            {
                disposals.Enqueue(_texture);
                _texture = null;
            }

            _tileBuffer = null;

            _loadedTexture?.EnqueueDisposals(disposals);
            _loadedTexture = null;
        }

        private static void CopyTextureTile(
            Image<Rgba32> source,
            int tileX,
            int tileY,
            int tileWidth,
            int tileHeight,
            Rgba32[] destination)
        {
            var sourcePixels = source.GetPixelSpan();
            var sourceWidth = source.Width;
            var destinationSpan = destination.AsSpan(0, tileWidth * tileHeight);

            for (var row = 0; row < tileHeight; row++)
            {
                var sourceOffset = (tileY + row) * sourceWidth + tileX;
                var destinationOffset = row * tileWidth;
                sourcePixels.Slice(sourceOffset, tileWidth)
                    .CopyTo(destinationSpan.Slice(destinationOffset, tileWidth));
            }
        }
    }

    private sealed class TransferPublishBatch(int generation, List<(ResPath Relative, byte[] Data)> files, int totalBytes)
    {
        public int Generation { get; } = generation;
        public List<(ResPath Relative, byte[] Data)> Files { get; } = files;
        public int TotalBytes { get; } = totalBytes;
    }

    private sealed class PreparedRsiUploadJob(string resourcePath, int generation, PreparedRsi prepared)
        : PreparedUploadJob(resourcePath, generation)
    {
        private PreparedRsi? _prepared = prepared;
        private readonly List<OwnedTexture> _textures = new();
        private LoadedRsiEntry? _loadedRsi;
        private int _stateIndex;
        private int _frameIndex;

        public override int EstimateStepCostBytes(NetTexturesManager manager)
        {
            var prepared = _prepared;
            if (prepared == null || _stateIndex >= prepared.States.Count)
                return 0;

            var state = prepared.States[_stateIndex];
            if (_frameIndex >= state.Frames.Count)
                return 0;

            var frame = state.Frames[_frameIndex];
            var image = frame.Image;
            return image == null ? 0 : image.Width * image.Height * 4;
        }

        public override bool ProcessStep(NetTexturesManager manager, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prepared = _prepared ?? throw new InvalidOperationException($"RSI upload job for {ResourceKey} has no prepared states");

            while (_stateIndex < prepared.States.Count)
            {
                var state = prepared.States[_stateIndex];
                if (_frameIndex >= state.Frames.Count)
                {
                    _stateIndex++;
                    _frameIndex = 0;
                    continue;
                }

                var frame = state.Frames[_frameIndex];
                try
                {
                    var texture = manager._clyde.LoadTextureFromImage(frame.Image!, $"{ResourceKey}:{state.StateId}:{frame.SourceIndex}", prepared.LoadParameters);
                    _textures.Add(texture);
                    state.UploadedFrames[frame.SourceIndex] = texture;
                }
                finally
                {
                    frame.Dispose();
                }

                _frameIndex++;
                return TryFinalize(prepared);
            }

            return TryFinalize(prepared);
        }

        private bool TryFinalize(PreparedRsi prepared)
        {
            if (_stateIndex < prepared.States.Count)
                return false;

            var states = new Dictionary<string, NetTextureAnimationState>(prepared.States.Count);
            foreach (var state in prepared.States)
            {
                states[state.StateId] = CreateAnimationState(state);
            }

            _loadedRsi = new LoadedRsiEntry(_textures, states);
            prepared.Dispose();
            _prepared = null;
            return true;
        }

        public override void Commit(NetTexturesManager manager)
        {
            if (_loadedRsi == null)
                throw new InvalidOperationException($"RSI upload job for {ResourceKey} completed without a loaded RSI");

            manager.FinishPreparedRsi(ResourceKey, _loadedRsi);
            _loadedRsi = null;
        }

        public override void Dispose()
        {
            _prepared?.Dispose();
            _prepared = null;

            if (_loadedRsi != null)
            {
                _loadedRsi.Dispose();
                _loadedRsi = null;
                return;
            }

            foreach (var texture in _textures)
            {
                texture.Dispose();
            }

            _textures.Clear();
        }

        public override void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            _prepared?.EnqueueDisposals(disposals);
            _prepared = null;

            if (_loadedRsi != null)
            {
                _loadedRsi.EnqueueDisposals(disposals);
                _loadedRsi = null;
                return;
            }

            foreach (var texture in _textures)
            {
                disposals.Enqueue(texture);
            }

            _textures.Clear();
        }
    }

    private sealed class PreparedRsi(TextureLoadParameters loadParameters, List<PreparedRsiState> states) : IDisposable
    {
        public readonly TextureLoadParameters LoadParameters = loadParameters;
        public readonly List<PreparedRsiState> States = states;

        public void Dispose()
        {
            foreach (var state in States)
            {
                state.Dispose();
            }
        }

        public void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            foreach (var state in States)
            {
                state.EnqueueDisposals(disposals);
            }
        }
    }

    private sealed class PreparedRsiState(
        string stateId,
        int directionCount,
        float[] foldedDelays,
        int[][] foldedIndices,
        List<PreparedRsiFrame> frames) : IDisposable
    {
        public readonly string StateId = stateId;
        public readonly int DirectionCount = directionCount;
        public readonly float[] FoldedDelays = foldedDelays;
        public readonly int[][] FoldedIndices = foldedIndices;
        public readonly List<PreparedRsiFrame> Frames = frames;
        public readonly Dictionary<int, Texture> UploadedFrames = new();

        public void Dispose()
        {
            foreach (var frame in Frames)
            {
                frame.Dispose();
            }
        }

        public void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            foreach (var frame in Frames)
            {
                frame.EnqueueDisposals(disposals);
            }
        }
    }

    private sealed class PreparedRsiFrame(int sourceIndex, Image<Rgba32> image) : IDisposable
    {
        public readonly int SourceIndex = sourceIndex;
        public Image<Rgba32>? Image { get; private set; } = image;

        public void Dispose()
        {
            Image?.Dispose();
            Image = null;
        }

        public void EnqueueDisposals(Queue<IDisposable> disposals)
        {
            if (Image == null)
                return;

            disposals.Enqueue(Image);
            Image = null;
        }
    }
    #endregion

    #region Transfer State
    private sealed class FallbackChunkAssembly(int totalChunks, int totalLength) : IDisposable
    {
        private readonly bool[] _receivedChunkFlags = new bool[totalChunks];
        private byte[]? _buffer = new byte[totalLength];

        public int TotalChunks { get; } = totalChunks;
        public int TotalLength { get; } = totalLength;
        public bool IsComplete { get; private set; }
        private int _receivedChunkCount;

        public void StoreChunk(int chunkIndex, int chunkOffset, byte[] data)
        {
            if (_receivedChunkFlags[chunkIndex])
                return;

            var buffer = _buffer ?? throw new InvalidOperationException("Cannot store a fallback chunk after the assembled NetTextures buffer was taken");
            Array.Copy(data, 0, buffer, chunkOffset, data.Length);
            _receivedChunkFlags[chunkIndex] = true;
            _receivedChunkCount++;
            IsComplete = _receivedChunkCount == TotalChunks;
        }

        public byte[] TakeCompletedData()
        {
            if (!IsComplete || _buffer == null)
                throw new InvalidOperationException("Cannot take incomplete fallback NetTextures data");

            var buffer = _buffer;
            _buffer = null;
            return buffer;
        }

        public void Dispose()
        {
            _buffer = null;
        }
    }
    #endregion

    #region Public State Views
    /// <summary>
    /// Представляет готовое к использованию состояние анимации, полученное из доставленного по сети RSI-ресурса.
    /// </summary>
    public sealed class NetTextureAnimationState(string stateId, RsiDirectionType directions, float[] delays, Texture[][] frames)
    {
        /// <summary>
        /// Возвращает идентификатор RSI state.
        /// </summary>
        public string StateId { get; } = stateId;

        /// <summary>
        /// Возвращает directional layout, используемый uploaded animation.
        /// </summary>
        public RsiDirectionType Directions { get; } = directions;

        /// <summary>
        /// Возвращает количество folded animation frames в этом состоянии.
        /// </summary>
        public int FrameCount => delays.Length;

        /// <summary>
        /// Возвращает, проходит ли состояние больше одного frame.
        /// </summary>
        public bool IsAnimated => FrameCount > 1;

        /// <summary>
        /// Возвращает первый frame анимации на юг, обычно используемый как preview frame.
        /// </summary>
        public Texture Frame0 => frames[0][0];

        /// <summary>
        /// Возвращает display delay для folded frame.
        /// </summary>
        /// <param name="frame">Индекс folded frame.</param>
        /// <returns>Задержка в секундах для запрошенного frame.</returns>
        public float GetDelay(int frame)
        {
            return delays[frame];
        }

        /// <summary>
        /// Возвращает текстуру для конкретного направления и folded frame.
        /// </summary>
        /// <param name="direction">Запрошенное RSI direction.</param>
        /// <param name="frame">Индекс folded frame.</param>
        /// <returns>Uploaded texture для запрошенного направления и frame.</returns>
        public Texture GetFrame(RsiDirection direction, int frame)
        {
            var dirIndex = Directions switch
            {
                RsiDirectionType.Dir1 => 0,
                RsiDirectionType.Dir4 => (int) direction.RoundToCardinal(),
                _ => (int) direction
            };

            return frames[dirIndex][frame];
        }
    }
    #endregion

    #region Metadata Models
    private sealed class PreparationRequest(string resourceKey, ResPath resPath, int generation)
    {
        public readonly string ResourceKey = resourceKey;
        public readonly ResPath ResPath = resPath;
        public readonly int Generation = generation;
    }

    private sealed class RsiMetadataData(Vector2i size, RsiStateMetadataData[] states, TextureLoadParameters loadParameters)
    {
        public readonly Vector2i Size = size;
        public readonly RsiStateMetadataData[] States = states;
        public readonly TextureLoadParameters LoadParameters = loadParameters;
    }

    private sealed class RsiStateMetadataData(string name, int? directions, float[][]? delays)
    {
        public readonly string Name = name;
        public readonly int? Directions = directions;
        public readonly float[][]? Delays = delays;
    }

    private sealed class RsiCompletenessEntry
    {
        private readonly HashSet<string> _presentFiles = new();
        private HashSet<string>? _requiredFiles;

        public bool HasMetadata => _requiredFiles != null;
        public bool IsComplete { get; private set; }
        public string? MetadataFailureReason { get; private set; }

        public void MarkPresent(string fileName)
        {
            if (!_presentFiles.Add(fileName))
                return;

            UpdateCompleteness();
        }

        public void SetMetadata(RsiMetadataData metadata)
        {
            var requiredFiles = new HashSet<string>
            {
                "meta.json"
            };

            foreach (var state in metadata.States)
            {
                if (string.IsNullOrWhiteSpace(state.Name))
                    continue;

                requiredFiles.Add($"{state.Name}.png");
            }

            MetadataFailureReason = null;
            _requiredFiles = requiredFiles;
            UpdateCompleteness();
        }

        public void SetMetadataFailure(string reason)
        {
            MetadataFailureReason = reason;
            _requiredFiles = null;
            IsComplete = false;
        }

        private void UpdateCompleteness()
        {
            if (_requiredFiles == null || _presentFiles.Count < _requiredFiles.Count)
            {
                IsComplete = false;
                return;
            }

            foreach (var requiredFile in _requiredFiles)
            {
                if (!_presentFiles.Contains(requiredFile))
                {
                    IsComplete = false;
                    return;
                }
            }

            IsComplete = true;
        }
    }
    #endregion
}
