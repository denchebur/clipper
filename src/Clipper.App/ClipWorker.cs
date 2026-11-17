using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;

namespace Clipper.App;

/// <summary>
/// Раз в пару секунд проверяет, какие клипы уже можно вырезать
/// (их конец записан на диск), и отдаёт их нарезчику.
/// </summary>
public sealed class ClipWorker(
    ClipperOptions options,
    ClipPlanner planner,
    IStreamRecorder recorder,
    IClipCutter cutter,
    ILogger<ClipWorker> logger) : BackgroundService
{
    /// <summary>Запас на то, что streamlink пишет на диск с небольшой задержкой.</summary>
    private static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MinClipLength = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var job in planner.TakeReadyJobs(DateTimeOffset.UtcNow, SafetyMargin))
                await ProcessAsync(job, stoppingToken);

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(ClipJob job, CancellationToken ct)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;

            // Берём запись, в которой есть конец клипа. Если клип начинается раньше записи — обрезаем начало.
            var session = recorder.Sessions.LastOrDefault(s => s.StartedAt < job.End && (s.EndedAt ?? now) >= job.End)
                ?? throw new InvalidOperationException("Нет записи, которая покрывает этот момент.");

            var start = job.Start > session.StartedAt ? job.Start : session.StartedAt;
            var duration = job.End - start;
            if (duration < MinClipLength)
                throw new InvalidOperationException("Слишком короткий кусок записи для клипа.");

            var platform = job.Spikes.FirstOrDefault()?.Platform ?? "clip";
            var output = Path.GetFullPath(Path.Combine(options.ClipsDir,
                $"clip_{start.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{platform}.mp4"));

            await cutter.CutAsync(session.FilePath, start - session.StartedAt, duration, output, ct);

            job.OutputPath = output;
            job.Status = ClipJobStatus.Done;
            logger.LogInformation("Клип готов: {Path}", output);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            job.Status = ClipJobStatus.Failed;
            job.Error = ex.Message;
            logger.LogError("Не удалось вырезать клип {Id}: {Error}", job.Id, ex.Message);
        }
    }
}
