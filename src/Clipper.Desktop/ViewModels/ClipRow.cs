using Clipper.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clipper.Desktop.ViewModels;

/// <summary>Строка в списке клипов.</summary>
public partial class ClipRow : ObservableObject
{
    public ClipRow(ClipJob job)
    {
        Id = job.Id;
        Update(job);
    }

    public Guid Id { get; }

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private string _length = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _canUpload;
    [ObservableProperty] private string? _youTubeUrl;

    /// <summary>Почему появился клип: рейд, донаты, смех в чате или просто активный чат.</summary>
    private static string Describe(ClipJob job)
    {
        var reasons = job.Spikes.Select(s => s.Reason).ToHashSet();
        var why = reasons.Contains("raid") ? "рейд"
            : reasons.Contains("events") ? "донаты/подписки"
            : reasons.Contains("emotes") ? "смех/эмоуты"
            : "чат";
        return job.Spikes.Count > 1 ? $"{why}, {job.Spikes.Count} всплеск." : why;
    }

    public void Update(ClipJob job)
    {
        var spike = job.Spikes.FirstOrDefault();
        Time = job.Start.ToLocalTime().ToString("HH:mm:ss");
        Source = spike is null ? job.Stream.Key
            : spike.Platform == "manual" ? $"{job.Stream.Key} · вручную"
            : $"{job.Stream.Key} · {Describe(job)}";
        Length = $"{job.Duration.TotalSeconds:0} с";
        Status = job.Status switch
        {
            ClipJobStatus.Waiting => "ждёт записи",
            ClipJobStatus.Transcribing => "распознаю речь…",
            ClipJobStatus.Cutting => "режется…",
            ClipJobStatus.Done => job.SubtitlesPath is null ? "готов" : "готов, с субтитрами",
            ClipJobStatus.Failed => "ошибка: " + job.Error,
            _ => job.Status.ToString(),
        };
        Status += job.Upload switch
        {
            UploadState.Uploading => " · загружаю в Shorts…",
            UploadState.Uploaded => " · в Shorts",
            UploadState.Failed => " · ошибка загрузки: " + job.UploadError,
            _ => "",
        };
        OutputPath = job.OutputPath;
        IsDone = job.Status == ClipJobStatus.Done;
        CanUpload = IsDone && job.Upload is UploadState.None or UploadState.Failed;
        YouTubeUrl = job.YouTubeUrl;
    }
}
