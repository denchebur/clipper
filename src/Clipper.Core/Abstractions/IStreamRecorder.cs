using Clipper.Core.Models;

namespace Clipper.Core.Abstractions;

/// <summary>Модуль записи стрима на диск.</summary>
public interface IStreamRecorder
{
    /// <summary>Какой стрим пишет этот рекордер.</summary>
    StreamSource Stream { get; }

    /// <summary>Удаляет из списка сессию, чей файл уже стёрт очисткой.</summary>
    void Forget(RecordingSession session);

    /// <summary>Все сессии записи с момента запуска (последняя может быть активной).</summary>
    IReadOnlyList<RecordingSession> Sessions { get; }

    /// <summary>Пишет стрим, пока не отменят токен. Сам перезапускается при обрывах.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
