using System.Globalization;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Clipper.Cutter;

/// <summary>
/// Режет клип из записи через ffmpeg и делает из него вертикальное видео 9:16
/// для TikTok / YouTube Shorts.
/// </summary>
public sealed class FfmpegClipCutter : IClipCutter
{
    private readonly ClipperOptions _options;
    private readonly ILogger<FfmpegClipCutter> _logger;

    public FfmpegClipCutter(ClipperOptions options, ILogger<FfmpegClipCutter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task CutAsync(string sourcePath, TimeSpan offset, TimeSpan duration, string outputPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        // Пишем во временный файл и переименовываем только после успеха,
        // чтобы недорезанный клип не выглядел готовым.
        var tempPath = outputPath + ".part";
        var args = BuildArguments(sourcePath, offset, duration, tempPath, _options.Vertical);
        _logger.LogInformation("ffmpeg {Args}", string.Join(' ', args));

        try
        {
            var result = await ExternalProcess.RunAsync(_options.FfmpegPath, args, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg завершился с кодом {result.ExitCode}:\n{result.StdErrTail}");
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>Собирает аргументы ffmpeg. Отдельный метод, чтобы его было легко менять и тестировать.</summary>
    public static List<string> BuildArguments(string sourcePath, TimeSpan offset, TimeSpan duration,
        string outputPath, VerticalVideoOptions vertical)
    {
        var w = vertical.Width;
        var h = vertical.Height;

        var filter = vertical.Mode.Equals("crop", StringComparison.OrdinalIgnoreCase)
            // Обрезаем центр кадра.
            ? $"[0:v]scale=-2:{h},crop={w}:{h}[v]"
            // Размытый фон на весь экран + исходное видео по центру.
            : $"[0:v]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},boxblur=20:2[bg];" +
              $"[0:v]scale={w}:-2[fg];" +
              "[bg][fg]overlay=(W-w)/2:(H-h)/2[v]";

        return new List<string>
        {
            "-hide_banner", "-loglevel", "error",
            "-y",
            "-ss", Seconds(offset),     // -ss перед -i: быстрая перемотка
            "-i", sourcePath,
            "-t", Seconds(duration),
            "-filter_complex", filter,
            "-map", "[v]",
            "-map", "0:a?",             // звук, если он есть
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
            "-c:a", "aac", "-b:a", "128k",
            "-movflags", "+faststart",
            "-f", "mp4",
            outputPath,
        };
    }

    private static string Seconds(TimeSpan t) =>
        Math.Max(0, t.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture);
}
