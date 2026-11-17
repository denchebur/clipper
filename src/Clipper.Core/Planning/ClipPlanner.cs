using Clipper.Core.Models;

namespace Clipper.Core.Planning;

/// <summary>
/// Превращает всплески в задания на нарезку и хранит список заданий.
/// Если новый всплеск попадает в ещё не нарезанный клип, клип удлиняется,
/// а не создаётся второй почти такой же.
/// </summary>
public sealed class ClipPlanner
{
    private readonly ClipperOptions _options;
    private readonly List<ClipJob> _jobs = new();
    private readonly object _lock = new();

    public ClipPlanner(ClipperOptions options) => _options = options;

    /// <summary>Снимок списка заданий (новые сверху).</summary>
    public IReadOnlyList<ClipJob> Jobs
    {
        get { lock (_lock) return _jobs.OrderByDescending(j => j.CreatedAt).ToList(); }
    }

    /// <summary>Добавляет всплеск. Возвращает задание, в которое он попал.</summary>
    public ClipJob AddSpike(ChatSpike spike)
    {
        var moment = spike.Timestamp - TimeSpan.FromSeconds(_options.LatencyCorrectionSeconds);
        var start = moment - TimeSpan.FromSeconds(_options.SecondsBefore);
        var end = moment + TimeSpan.FromSeconds(_options.SecondsAfter);
        var maxLength = TimeSpan.FromSeconds(_options.MaxClipSeconds);

        lock (_lock)
        {
            // Ищем ожидающий клип, с которым новый отрезок пересекается.
            var existing = _jobs.FirstOrDefault(j =>
                j.Status == ClipJobStatus.Waiting && start <= j.End && end >= j.Start);

            if (existing is not null)
            {
                existing.Spikes.Add(spike);
                var newStart = start < existing.Start ? start : existing.Start;
                var newEnd = end > existing.End ? end : existing.End;
                if (newEnd - newStart > maxLength)
                    newEnd = newStart + maxLength;
                existing.Start = newStart;
                existing.End = newEnd;
                return existing;
            }

            var job = new ClipJob { Start = start, End = end };
            job.Spikes.Add(spike);
            _jobs.Add(job);
            return job;
        }
    }

    /// <summary>Задания, конец которых уже записан и которые можно резать.</summary>
    public IReadOnlyList<ClipJob> TakeReadyJobs(DateTimeOffset now, TimeSpan safetyMargin)
    {
        lock (_lock)
        {
            var ready = _jobs
                .Where(j => j.Status == ClipJobStatus.Waiting && j.End + safetyMargin <= now)
                .OrderBy(j => j.Start)
                .ToList();
            foreach (var job in ready)
                job.Status = ClipJobStatus.Cutting;
            return ready;
        }
    }
}
