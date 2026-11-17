using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clipper.Server;

/// <summary>
/// Загрузка готового клипа в Shorts: автоматически (из ClipWorker) или вручную
/// (кнопка «В Shorts» в клиенте или POST /api/clips/{id}/upload).
/// Ошибка загрузки не портит сам клип — он остаётся на диске.
/// </summary>
public sealed class ClipUploader(IClipPublisher publisher, ILogger<ClipUploader> logger)
{
    public async Task UploadAsync(ClipJob job, CancellationToken cancellationToken)
    {
        if (job.Status != ClipJobStatus.Done || job.Upload is UploadState.Uploading or UploadState.Uploaded)
            return;

        job.Upload = UploadState.Uploading;
        job.UploadError = null;
        try
        {
            job.YouTubeUrl = await publisher.PublishAsync(job, cancellationToken);
            job.Upload = UploadState.Uploaded;
            logger.LogInformation("Клип загружен в Shorts: {Url}", job.YouTubeUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Upload = UploadState.None;
            throw;
        }
        catch (Exception ex)
        {
            job.Upload = UploadState.Failed;
            job.UploadError = ex.Message;
            logger.LogError("Не удалось загрузить клип в Shorts: {Error}", ex.Message);
        }
    }
}
