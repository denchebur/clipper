using Clipper.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipper.Server;

/// <summary>
/// Раз в минуту удаляет записи стримов старше KeepRecordingsMinutes, чтобы не забить диск.
/// Удаляются только закрытые файлы (в которые уже не идёт запись). Клипы не трогаем.
/// </summary>
public sealed class RecordingCleaner(ClipperOptions options, RecorderPool pool, ILogger<RecordingCleaner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.KeepRecordingsMinutes <= 0) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Clean(DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Очистка записей: {Error}", ex.Message);
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Clean(DateTimeOffset now)
    {
        var keep = TimeSpan.FromMinutes(options.KeepRecordingsMinutes);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Файлы закрытых сессий текущего запуска.
        foreach (var recorder in pool.Recorders)
        foreach (var session in recorder.Sessions)
        {
            if (session.EndedAt is null) { active.Add(session.FilePath); continue; }
            if (now - session.EndedAt.Value < keep) { active.Add(session.FilePath); continue; }
            Delete(session.FilePath);
            recorder.Forget(session);
        }

        // 2. Старые файлы, оставшиеся от прошлых запусков программы.
        var root = Path.GetFullPath(options.RecordingsDir);
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*.ts", SearchOption.AllDirectories))
        {
            if (active.Contains(Path.GetFullPath(file))) continue;
            if (now - File.GetLastWriteTimeUtc(file) > keep) Delete(file);
        }
    }

    private void Delete(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
            logger.LogInformation("Удалена старая запись {File}", Path.GetFileName(path));
        }
        catch (IOException ex)
        {
            logger.LogDebug("Не удалось удалить {File}: {Error}", path, ex.Message);
        }
    }
}
