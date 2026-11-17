using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Infrastructure;
using Clipper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clipper.Recorder;

/// <summary>
/// Пишет стрим в файл .ts через streamlink (работает и с Twitch, и с YouTube).
/// Формат MPEG-TS можно читать, пока он ещё пишется, поэтому клипы режутся
/// прямо во время эфира.
/// </summary>
public sealed class StreamlinkRecorder : IStreamRecorder
{
    private readonly ClipperOptions _options;
    private readonly ILogger<StreamlinkRecorder> _logger;
    private readonly List<RecordingSession> _sessions = new();
    private readonly object _lock = new();

    public StreamlinkRecorder(ClipperOptions options, ILogger<StreamlinkRecorder> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IReadOnlyList<RecordingSession> Sessions
    {
        get { lock (_lock) return _sessions.ToList(); }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.StreamUrl))
        {
            _logger.LogWarning("StreamUrl не задан в appsettings.json — запись не запущена.");
            return;
        }

        Directory.CreateDirectory(_options.RecordingsDir);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RecordOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка записи");
            }

            _logger.LogInformation("Перезапуск записи через {Seconds} с", _options.RecorderRestartDelaySeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.RecorderRestartDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RecordOnceAsync(CancellationToken cancellationToken)
    {
        var fileName = $"stream_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ts";
        var filePath = Path.GetFullPath(Path.Combine(_options.RecordingsDir, fileName));

        var args = new List<string>
        {
            "--output", filePath,
            "--force",
            _options.StreamUrl,
            _options.Quality,
        };

        _logger.LogInformation("Старт записи {Url} → {File}", _options.StreamUrl, filePath);

        using var stopWatching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processTask = ExternalProcess.RunAsync(_options.StreamlinkPath, args, cancellationToken,
            line => _logger.LogDebug("streamlink: {Line}", line));

        // Сессия начинается, когда в файл пришли первые байты, а не когда запустился процесс:
        // streamlink может несколько секунд искать поток.
        var session = await WaitForFirstBytesAsync(filePath, processTask, stopWatching.Token);

        try
        {
            var result = await processTask;
            _logger.LogInformation("streamlink завершился с кодом {Code}. {Tail}", result.ExitCode, result.StdErrTail);
        }
        finally
        {
            stopWatching.Cancel();
            if (session is not null)
                session.EndedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<RecordingSession?> WaitForFirstBytesAsync(
        string filePath, Task processTask, CancellationToken cancellationToken)
    {
        while (!processTask.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            var info = new FileInfo(filePath);
            if (info.Exists && info.Length > 0)
            {
                var session = new RecordingSession { FilePath = filePath, StartedAt = DateTimeOffset.UtcNow };
                lock (_lock) _sessions.Add(session);
                _logger.LogInformation("Запись пошла: {File}", filePath);
                return session;
            }

            try { await Task.Delay(200, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
        return null;
    }
}
