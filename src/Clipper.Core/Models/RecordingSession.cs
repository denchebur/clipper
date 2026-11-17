namespace Clipper.Core.Models;

/// <summary>
/// Один непрерывный файл записи стрима. Если стрим оборвался и рекордер
/// перезапустился, появляется новая сессия (новый файл).
/// </summary>
public sealed class RecordingSession
{
    public required string FilePath { get; init; }

    /// <summary>Когда в файл попали первые байты. Нулевая секунда файла ≈ этот момент.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Когда запись закончилась (null — пишется прямо сейчас).</summary>
    public DateTimeOffset? EndedAt { get; set; }

    public bool IsActive => EndedAt is null;

    /// <summary>Покрывает ли файл отрезок времени целиком (с учётом «сейчас» для активной записи).</summary>
    public bool Covers(DateTimeOffset from, DateTimeOffset to, DateTimeOffset now) =>
        from >= StartedAt && to <= (EndedAt ?? now);
}
