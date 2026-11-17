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

    public void Update(ClipJob job)
    {
        var spike = job.Spikes.FirstOrDefault();
        Time = job.Start.ToLocalTime().ToString("HH:mm:ss");
        Source = spike is null ? "" : spike.Platform == "manual" ? "вручную" : $"{spike.Platform} ({job.Spikes.Count} всплеск.)";
        Length = $"{job.Duration.TotalSeconds:0} с";
        Status = job.Status switch
        {
            ClipJobStatus.Waiting => "ждёт записи",
            ClipJobStatus.Cutting => "режется…",
            ClipJobStatus.Done => "готов",
            ClipJobStatus.Failed => "ошибка: " + job.Error,
            _ => job.Status.ToString(),
        };
        OutputPath = job.OutputPath;
        IsDone = job.Status == ClipJobStatus.Done;
    }
}
