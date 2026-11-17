namespace Clipper.Core.Abstractions;

/// <summary>Модуль нарезки: вырезает кусок из записи и делает вертикальное видео.</summary>
public interface IClipCutter
{
    /// <param name="sourcePath">Файл записи.</param>
    /// <param name="offset">С какой секунды файла начинать.</param>
    /// <param name="duration">Длина клипа.</param>
    /// <param name="outputPath">Куда сохранить готовый .mp4.</param>
    /// <param name="subtitlesPath">Файл .srt, который надо вшить в видео (null — без субтитров).</param>
    Task CutAsync(string sourcePath, TimeSpan offset, TimeSpan duration, string outputPath,
        CancellationToken cancellationToken, string? subtitlesPath = null);

    /// <summary>Вырезает звук отрезка в WAV 16 кГц моно — в таком виде его принимает Whisper.</summary>
    Task ExtractAudioAsync(string sourcePath, TimeSpan offset, TimeSpan duration, string wavPath,
        CancellationToken cancellationToken);
}
