namespace Clipper.Core.Models;

public enum UploadState
{
    None,
    Uploading,
    Uploaded,
    Failed,
}

public enum ClipJobStatus
{
    /// <summary>Ждём, пока рекордер запишет нужный кусок (конец клипа ещё в будущем).</summary>
    Waiting,
    /// <summary>Распознаём речь для субтитров.</summary>
    Transcribing,
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

    /// <summary>Стрим, из записи которого режется клип.</summary>
    public required StreamSource Stream { get; init; }

    /// <summary>Всплески, из-за которых появился клип (близкие всплески склеиваются в один клип).</summary>
    public List<ChatSpike> Spikes { get; } = new();

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public TimeSpan Duration => End - Start;

    public ClipJobStatus Status { get; set; } = ClipJobStatus.Waiting;
    public string? OutputPath { get; set; }

    /// <summary>Файл субтитров .srt (если включены и в клипе была речь).</summary>
    public string? SubtitlesPath { get; set; }

    /// <summary>Распознанный текст речи (для названия ролика).</summary>
    public string? TranscriptText { get; set; }

    public UploadState Upload { get; set; } = UploadState.None;
    public string? YouTubeUrl { get; set; }
    public string? UploadError { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}
