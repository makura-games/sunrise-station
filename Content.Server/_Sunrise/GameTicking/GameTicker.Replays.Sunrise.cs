using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using System.Threading.Channels;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private const int MaxUploadRetries = 5;

    private readonly Channel<(IWritableDirProvider Directory, ResPath Path)> _replayUploadChannel =
        Channel.CreateUnbounded<(IWritableDirProvider, ResPath)>();
    private Task? _replayUploadWorkerTask;
    private readonly CancellationTokenSource _replayUploadCancelToken = new();

    private void EnsureUploadWorkerStarted()
    {
        if (_replayUploadWorkerTask != null)
            return;

        _replayUploadWorkerTask = Task.Run(ProcessReplayUploadQueue);
    }

    private async Task ProcessReplayUploadQueue()
    {
        var reader = _replayUploadChannel.Reader;
        var cancellationToken = _replayUploadCancelToken.Token;

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var uploadSuccess = await UploadReplayWithRetry(item.Directory, item.Path, cancellationToken)
                            .ConfigureAwait(false);
                        if (uploadSuccess)
                        {
                            try
                            {
                                item.Directory.Delete(item.Path);
                                _sawmillReplays.Info($"Локальный файл реплея {item.Path.Filename} успешно удален.");
                            }
                            catch (Exception deleteEx)
                            {
                                _sawmillReplays.Error(
                                    $"Ошибка при удалении локального файла реплея {item.Path}: {deleteEx}");
                            }
                        }
                        else
                        {
                            _sawmillReplays.Error(
                                $"Не удалось загрузить реплей {item.Path} в S3 после {MaxUploadRetries} попыток. Реплей пропущен.");
                        }
                    }
                    catch (Exception itemEx)
                    {
                        if (itemEx is OperationCanceledException)
                            throw;
                        _sawmillReplays.Error(
                            $"Необработанная ошибка воркера при обработке реплея {item.Path}: {itemEx}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            //
        }
        catch (Exception e)
        {
            _sawmillReplays.Error($"Критическая ошибка в воркере загрузки реплеев: {e}");
        }
    }

    private async Task<bool> UploadReplayWithRetry(IWritableDirProvider directory, ResPath path, CancellationToken cancellationToken)
    {
        var endpoint = _cfg.GetCVar(SunriseCCVars.ReplayS3Endpoint);
        var bucket = _cfg.GetCVar(SunriseCCVars.ReplayS3Bucket);
        var accessKey = _cfg.GetCVar(SunriseCCVars.ReplayS3AccessKey);
        var secretKey = _cfg.GetCVar(SunriseCCVars.ReplayS3SecretKey);

        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(2);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _sawmillReplays.Info($"Попытка загрузки {path} в S3 (попытка #{retryCount + 1})...");

                using var fileStream = directory.OpenRead(path);
                var fileName = path.Filename;

                var config = new AmazonS3Config
                {
                    ServiceURL = endpoint,
                    ForcePathStyle = true
                };

                using var client = new AmazonS3Client(accessKey, secretKey, config);
                using var fileTransferUtility = new TransferUtility(client);

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = fileStream,
                    BucketName = bucket,
                    Key = fileName,
                    CannedACL = S3CannedACL.PublicRead
                };

                await fileTransferUtility.UploadAsync(uploadRequest, cancellationToken).ConfigureAwait(false);
                _sawmillReplays.Info($"Реплей {fileName} успешно загружен в S3.");
                return true;
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                    return false;

                retryCount++;
                _sawmillReplays.Error($"Ошибка при загрузке реплея {path} в S3 (попытка #{retryCount}): {e}");

                if (retryCount >= MaxUploadRetries)
                {
                    return false;
                }

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return false;
                }

                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(5).Ticks));
            }
        }

        return false;
    }

    private void UploadReplayToS3(IWritableDirProvider directory, ResPath path)
    {
        if (!_cfg.GetCVar(SunriseCCVars.ReplayS3UploadEnabled))
            return;

        var bucket = _cfg.GetCVar(SunriseCCVars.ReplayS3Bucket);
        var accessKey = _cfg.GetCVar(SunriseCCVars.ReplayS3AccessKey);
        var secretKey = _cfg.GetCVar(SunriseCCVars.ReplayS3SecretKey);

        if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            _sawmillReplays.Error("S3 загрузка включена, но учетные данные или бакет не настроены!");
            return;
        }

        EnsureUploadWorkerStarted();
        _replayUploadChannel.Writer.TryWrite((directory, path));
    }

    private void ShutdownReplaysSunrise()
    {
        _replayUploadChannel.Writer.Complete();

        if (_replayUploadWorkerTask != null)
        {
            try
            {
                _sawmillReplays.Info("Остановка воркера загрузки реплеев. Ожидание завершения текущих задач...");
                if (!_replayUploadWorkerTask.Wait(TimeSpan.FromSeconds(30)))
                {
                    _sawmillReplays.Warning("Воркер загрузки реплеев не завершился в течение таймаута. Принудительная отмена...");
                    _replayUploadCancelToken.Cancel();
                    _replayUploadWorkerTask.Wait(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception e)
            {
                _sawmillReplays.Error($"Ошибка при остановке воркера загрузки реплеев: {e}");
            }
        }
    }

    private void CleanupTempReplays()
    {
        try
        {
            var tempDir = _cfg.GetCVar(CCVars.ReplayAutoRecordTempDir);
            if (string.IsNullOrEmpty(tempDir))
                return;

            var tempPath = new ResPath(tempDir);
            if (!_resourceManager.UserData.Exists(tempPath))
                return;

            _sawmillReplays.Info($"Очистка временной папки реплеев: {tempPath}");
            var (files, _) = _resourceManager.UserData.Find($"{tempDir}/*", false);
            foreach (var file in files)
            {
                _sawmillReplays.Debug($"Удаление брошенного временного файла реплея: {file}");
                _resourceManager.UserData.Delete(file);
            }
        }
        catch (Exception e)
        {
            _sawmillReplays.Error($"Ошибка при очистке временной папки реплеев: {e}");
        }
    }
}
