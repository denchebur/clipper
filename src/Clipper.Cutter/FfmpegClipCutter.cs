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
        CancellationToken cancellationToken, string? subtitlesPath = null)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // Пишем во временный файл и переименовываем только после успеха,
        // чтобы недорезанный клип не выглядел готовым.
        var tempPath = outputPath + ".part";

        // Путь к субтитрам внутри фильтра ffmpeg ломается на "C:\..." (двоеточия и слеши),
        // поэтому запускаем ffmpeg из папки с .srt и передаём только имя файла.
        string? workingDir = null;
        SubtitleStyle? subtitles = null;
        if (subtitlesPath is not null)
        {
            workingDir = Path.GetDirectoryName(Path.GetFullPath(subtitlesPath));
            subtitles = new SubtitleStyle(Path.GetFileName(subtitlesPath), _options.Subtitles.FontSize);
        }

        var args = BuildArguments(sourcePath, offset, duration, tempPath, _options.Vertical, subtitles);
        _logger.LogDebug("ffmpeg {Args}", string.Join(' ', args));

        try
        {
            var result = await ExternalProcess.RunAsync(_options.FfmpegPath, args, cancellationToken,
                workingDirectory: workingDir);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg завершился с кодом {result.ExitCode}:\n{result.StdErrTail}");
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public async Task ExtractAudioAsync(string sourcePath, TimeSpan offset, TimeSpan duration, string wavPath,
        CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-ss", Seconds(offset),
            "-i", sourcePath,
            "-t", Seconds(duration),
            "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
            wavPath,
        };
        var result = await ExternalProcess.RunAsync(_options.FfmpegPath, args, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg (звук) завершился с кодом {result.ExitCode}:\n{result.StdErrTail}");
    }

    /// <summary>Какие субтитры вшить: имя .srt (относительно рабочей папки ffmpeg) и размер шрифта.</summary>
    public sealed record SubtitleStyle(string FileName, int FontSize);

    /// <summary>Собирает аргументы ffmpeg. Отдельный метод, чтобы его было легко менять и тестировать.</summary>
    public static List<string> BuildArguments(string sourcePath, TimeSpan offset, TimeSpan duration,
        string outputPath, VerticalVideoOptions vertical, SubtitleStyle? subtitles = null)
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

        if (subtitles is not null)
        {
            // Белый жирный текст с чёрной обводкой, по центру, выше нижнего края —
            // чтобы не перекрывался кнопками TikTok/Shorts.
            var style = $"FontName=Arial,FontSize={subtitles.FontSize},Bold=1,PrimaryColour=&H00FFFFFF," +
                        "OutlineColour=&H00000000,BorderStyle=1,Outline=3,Shadow=0,Alignment=2,MarginV=70";
            filter = filter[..^"[v]".Length] + $"[raw];[raw]subtitles=filename={subtitles.FileName}:force_style='{style}'[v]";
        }

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
