using Clipper.Core.Models;

namespace Clipper.Core.Abstractions;

/// <summary>Модуль записи стрима на диск.</summary>
public interface IStreamRecorder
{
    /// <summary>Все сессии записи с момента запуска (последняя может быть активной).</summary>
    IReadOnlyList<RecordingSession> Sessions { get; }

    /// <summary>Пишет стрим, пока не отменят токен. Сам перезапускается при обрывах.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
