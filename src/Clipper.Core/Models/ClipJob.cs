namespace Clipper.Core.Models;

public enum ClipJobStatus
{
    /// <summary>Ждём, пока рекордер запишет нужный кусок (конец клипа ещё в будущем).</summary>
    Waiting,
    /// <summary>ffmpeg режет клип.</summary>
    Cutting,
    Done,
    Failed,
}

/// <summary>
/// Задание на нарезку одного клипа: «вырезать отрезок [Start; End] из записи».
/// </summary>
public sealed class ClipJob
{
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Всплески, из-за которых появился клип (близкие всплески склеиваются в один клип).</summary>
    public List<ChatSpike> Spikes { get; } = new();

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public TimeSpan Duration => End - Start;

    public ClipJobStatus Status { get; set; } = ClipJobStatus.Waiting;
    public string? OutputPath { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}
