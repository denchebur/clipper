using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Publishing;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace Clipper.YouTube;

/// <summary>
/// Загружает клип на YouTube. Вертикальное видео короче 3 минут YouTube сам
/// относит к Shorts, а #Shorts в названии и описании помогает.
/// </summary>
public sealed class YouTubeShortsPublisher(
    ClipperOptions options,
    YouTubeAuth auth,
    ILogger<YouTubeShortsPublisher> logger) : IClipPublisher
{
    private const string GamingCategoryId = "20";

    public async Task<string> PublishAsync(ClipJob job, CancellationToken cancellationToken)
    {
        if (job.OutputPath is null || !File.Exists(job.OutputPath))
            throw new InvalidOperationException("Файл клипа не найден.");

        var meta = ShortsMetadata.Build(job, options);
        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = meta.Title,
                Description = meta.Description,
                Tags = meta.Tags.ToList(),
                CategoryId = GamingCategoryId,
            },
            Status = new VideoStatus
            {
                PrivacyStatus = options.YouTube.Privacy,
                SelfDeclaredMadeForKids = false,
            },
        };

        using var service = await auth.CreateServiceAsync(cancellationToken);
        await using var file = File.OpenRead(job.OutputPath);

        var insert = service.Videos.Insert(video, "snippet,status", file, "video/mp4");
        insert.ProgressChanged += p =>
        {
            if (p.Status == UploadStatus.Uploading)
                logger.LogDebug("YouTube: загружено {Bytes} байт", p.BytesSent);
        };

        logger.LogInformation("Загружаю на YouTube: «{Title}»", meta.Title);
        var progress = await insert.UploadAsync(cancellationToken);
        if (progress.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"YouTube отклонил загрузку: {progress.Exception?.Message}");

        return $"https://youtube.com/shorts/{insert.ResponseBody.Id}";
    }
}
