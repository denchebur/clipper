using Clipper.Core.Models;

namespace Clipper.Core.Abstractions;

/// <summary>Модуль распознавания речи (речь клипа → текст с таймкодами).</summary>
public interface ITranscriber
{
    /// <param name="wavPath">Звук клипа: WAV, 16 кГц, моно.</param>
    Task<Transcript> TranscribeAsync(string wavPath, CancellationToken cancellationToken);
}
