using Clipper.Core.Models;

namespace Clipper.Core.Abstractions;

/// <summary>Модуль публикации готового клипа (сейчас — YouTube Shorts).</summary>
public interface IClipPublisher
{
    /// <summary>Загружает клип и возвращает ссылку на опубликованное видео.</summary>
    Task<string> PublishAsync(ClipJob job, CancellationToken cancellationToken);
}
