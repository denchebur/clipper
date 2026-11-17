namespace Clipper.Core.Abstractions;

/// <summary>Модуль нарезки: вырезает кусок из записи и делает вертикальное видео.</summary>
public interface IClipCutter
{
    /// <param name="sourcePath">Файл записи.</param>
    /// <param name="offset">С какой секунды файла начинать.</param>
    /// <param name="duration">Длина клипа.</param>
    /// <param name="outputPath">Куда сохранить готовый .mp4.</param>
    Task CutAsync(string sourcePath, TimeSpan offset, TimeSpan duration, string outputPath,
        CancellationToken cancellationToken);
}
