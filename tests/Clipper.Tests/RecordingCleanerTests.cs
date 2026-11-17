using Clipper.Core;
using Clipper.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipper.Tests;

public class RecordingCleanerTests
{
    [Fact]
    public void DeletesOnlyOldRecordings_AndNeverClips()
    {
        var root = Directory.CreateTempSubdirectory("clipper-clean").FullName;
        var recordings = Path.Combine(root, "recordings", "alpha");
        var clips = Path.Combine(root, "clips", "alpha");
        Directory.CreateDirectory(recordings);
        Directory.CreateDirectory(clips);

        var oldTs = Path.Combine(recordings, "old.ts");
        var freshTs = Path.Combine(recordings, "fresh.ts");
        var oldClip = Path.Combine(clips, "clip.mp4");
        foreach (var f in new[] { oldTs, freshTs, oldClip }) File.WriteAllText(f, "x");
        File.SetLastWriteTimeUtc(oldTs, DateTime.UtcNow.AddHours(-5));
        File.SetLastWriteTimeUtc(oldClip, DateTime.UtcNow.AddHours(-5));

        var options = new ClipperOptions
        {
            RecordingsDir = Path.Combine(root, "recordings"),
            ClipsDir = Path.Combine(root, "clips"),
            KeepRecordingsMinutes = 120,
        };
        var pool = new RecorderPool(options, NullLoggerFactory.Instance);
        new RecordingCleaner(options, pool, NullLogger<RecordingCleaner>.Instance).Clean(DateTimeOffset.UtcNow);

        Assert.False(File.Exists(oldTs));
        Assert.True(File.Exists(freshTs));
        Assert.True(File.Exists(oldClip));
        Directory.Delete(root, recursive: true);
    }
}
