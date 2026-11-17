using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Publishing;
using Clipper.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipper.Tests;

public class ShortsTests
{
    private static ClipJob DoneJob(string? transcript)
    {
        var job = new ClipJob { Stream = StreamSource.FromUrl("https://www.twitch.tv/fubgun"), Start = DateTimeOffset.Now, End = DateTimeOffset.Now.AddSeconds(30), Status = ClipJobStatus.Done };
        job.Spikes.Add(new ChatSpike(DateTimeOffset.Now, "twitch", "fubgun", 5, 1));
        job.TranscriptText = transcript;
        return job;
    }

    [Fact]
    public void Title_UsesBeginningOfSpeech()
    {
        var meta = ShortsMetadata.Build(DoneJob("вот это да как он это сделал вообще не понимаю что произошло"), new ClipperOptions());
        Assert.Equal("вот это да как он это сделал вообще… #Shorts", meta.Title);
    }

    [Fact]
    public void Title_FallsBackToChannel_WhenNoSpeech()
    {
        var meta = ShortsMetadata.Build(DoneJob(null), new ClipperOptions());
        Assert.Equal("Момент со стрима fubgun #Shorts", meta.Title);
    }

    [Fact]
    public void Description_ContainsStreamLink_AndTitleIsLimited()
    {
        var options = new ClipperOptions();
        options.YouTube.TitleTemplate = new string('а', 150) + " <{channel}>";

        var meta = ShortsMetadata.Build(DoneJob(null), options);

        Assert.Contains("https://www.twitch.tv/fubgun", meta.Description);
        Assert.Contains('\n', meta.Description);
        Assert.Equal(ShortsMetadata.MaxTitleLength, meta.Title.Length);
        Assert.Equal(new[] { "shorts", "stream", "twitch" }, meta.Tags);
    }

    private sealed class FakePublisher(Exception? error = null) : IClipPublisher
    {
        public int Calls { get; private set; }
        public Task<string> PublishAsync(ClipJob job, CancellationToken cancellationToken)
        {
            Calls++;
            return error is null ? Task.FromResult("https://youtube.com/shorts/abc") : Task.FromException<string>(error);
        }
    }

    [Fact]
    public async Task Uploader_UploadsOnce()
    {
        var publisher = new FakePublisher();
        var uploader = new ClipUploader(publisher, NullLogger<ClipUploader>.Instance);
        var job = DoneJob("текст");

        await uploader.UploadAsync(job, CancellationToken.None);
        await uploader.UploadAsync(job, CancellationToken.None);

        Assert.Equal(1, publisher.Calls);
        Assert.Equal(UploadState.Uploaded, job.Upload);
        Assert.Equal("https://youtube.com/shorts/abc", job.YouTubeUrl);
    }

    [Fact]
    public async Task Uploader_KeepsClip_WhenUploadFails()
    {
        var uploader = new ClipUploader(new FakePublisher(new InvalidOperationException("нет входа")), NullLogger<ClipUploader>.Instance);
        var job = DoneJob("текст");

        await uploader.UploadAsync(job, CancellationToken.None);

        Assert.Equal(ClipJobStatus.Done, job.Status);
        Assert.Equal(UploadState.Failed, job.Upload);
        Assert.Equal("нет входа", job.UploadError);
    }
}
