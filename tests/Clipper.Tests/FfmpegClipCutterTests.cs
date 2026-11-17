using System.Diagnostics;
using Clipper.Core;
using Clipper.Cutter;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clipper.Tests;

public class FfmpegClipCutterTests
{
    [Fact]
    public void Arguments_UseInvariantNumbers()
    {
        var args = FfmpegClipCutter.BuildArguments("in.ts", TimeSpan.FromSeconds(12.5), TimeSpan.FromSeconds(30),
            "out.mp4", new VerticalVideoOptions());

        Assert.Equal("12.5", args[args.IndexOf("-ss") + 1]);
        Assert.Equal("30", args[args.IndexOf("-t") + 1]);
        Assert.Equal("out.mp4", args[^1]);
    }

    /// <summary>
    /// Настоящая нарезка: генерируем тестовое видео 16:9 и режем из него вертикальный клип.
    /// Если ffmpeg не установлен, тест ничего не делает.
    /// </summary>
    [Theory]
    [InlineData("blur")]
    [InlineData("crop")]
    public async Task CutsVerticalClip_WhenFfmpegInstalled(string mode)
    {
        if (!FfmpegAvailable()) return;

        var dir = Directory.CreateTempSubdirectory("clipper-test").FullName;
        var source = Path.Combine(dir, "source.ts");
        var output = Path.Combine(dir, "clip.mp4");

        await RunFfmpeg("-f", "lavfi", "-i", "testsrc=size=1280x720:rate=30:duration=10",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=10",
            "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-f", "mpegts", source);

        var options = new ClipperOptions { Vertical = new VerticalVideoOptions { Width = 360, Height = 640, Mode = mode } };
        var cutter = new FfmpegClipCutter(options, NullLogger<FfmpegClipCutter>.Instance);

        await cutter.CutAsync(source, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), output, CancellationToken.None);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
        Directory.Delete(dir, recursive: true);
    }

    private static bool FfmpegAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                { RedirectStandardOutput = true, UseShellExecute = false });
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunFfmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
        Assert.Equal(0, p.ExitCode);
    }
}
