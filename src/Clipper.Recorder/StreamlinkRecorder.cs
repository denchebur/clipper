using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Infrastructure;
using Clipper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clipper.Recorder;

/// <summary>
/// Пишет один стрим в файлы .ts через streamlink (Twitch, YouTube и др.).
/// MPEG-TS можно читать, пока он ещё пишется, поэтому клипы режутся прямо во время эфира.
///
/// Раз в RotateRecordingMinutes запись начинается в новый файл, а старый ещё какое-то время
/// пишется параллельно (перекрытие), чтобы клип на стыке файлов целиком попал в один из них.
/// Закрытые файлы потом удаляет очистка (RecordingCleaner).
/// </summary>
public sealed class StreamlinkRecorder : IStreamRecorder
{
    private readonly ClipperOptions _options;
    private readonly ILogger _logger;
    private readonly List<RecordingSession> _sessions = new();
    private readonly object _lock = new();

    public StreamlinkRecorder(StreamSource stream, ClipperOptions options, ILogger logger)
    {
        Stream = stream;
        _options = options;
        _logger = logger;
    }

    public StreamSource Stream { get; }

    public IReadOnlyList<RecordingSession> Sessions
    {
        get { lock (_lock) return _sessions.ToList(); }
    }

    public void Forget(RecordingSession session)
    {
        lock (_lock) _sessions.Remove(session);
    }

    /// <summary>Перекрытие старого и нового файла при смене файла: самый длинный клип плюс запас.</summary>
    private TimeSpan Overlap => TimeSpan.FromSeconds(_options.MaxClipSeconds + _options.SecondsAfter + 30);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var dir = Path.Combine(_options.RecordingsDir, Stream.Key);
        Directory.CreateDirectory(dir);

        while (!cancellationToken.IsCancellationRequested)
        {
            var current = Start(dir, cancellationToken);
            try
            {
                while (true)
                {
                    var rotate = _options.RotateRecordingMinutes > 0
                        ? TimeSpan.FromMinutes(_options.RotateRecordingMinutes)
                        : Timeout.InfiniteTimeSpan;
                    var timer = Task.Delay(rotate, cancellationToken);

                    var finished = await Task.WhenAny(current.Completion, timer);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (finished == current.Completion)
                        break; // streamlink завершился сам: стрим закончился или оборвался

                    // Пора менять файл: запускаем новый, ждём, пока он заработает, и ещё перекрытие.
                    _logger.LogInformation("[{Stream}] Начинаю новый файл записи", Stream.Key);
                    var next = Start(dir, cancellationToken);
                    await Task.WhenAny(next.FirstBytes, next.Completion);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (next.Completion.IsCompleted)
                    {
                        // Новый не запустился — продолжаем писать в старый.
                        await next.StopAsync();
                        continue;
                    }
                    await Task.WhenAny(Task.Delay(Overlap, cancellationToken), current.Completion);
                    if (cancellationToken.IsCancellationRequested) await next.StopAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    await current.StopAsync();
                    current = next;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await current.StopAsync();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Stream}] Ошибка записи", Stream.Key);
                await current.StopAsync();
            }

            _logger.LogInformation("[{Stream}] Запись остановилась. Повтор через {Seconds} с (стрим мог закончиться)",
                Stream.Key, _options.RecorderRestartDelaySeconds);
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

    /// <summary>Запускает streamlink в новый файл.</summary>
    private Recording Start(string dir, CancellationToken cancellationToken)
    {
        var filePath = Path.GetFullPath(Path.Combine(dir, $"stream_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ts"));
        var args = new List<string> { "--output", filePath, "--force", Stream.Url, _options.Quality };
        _logger.LogInformation("[{Stream}] Старт записи → {File}", Stream.Key, filePath);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var firstBytes = new TaskCompletionSource<RecordingSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingSession? session = null;

        var process = ExternalProcess.RunAsync(_options.StreamlinkPath, args, cts.Token,
            line => _logger.LogDebug("[{Stream}] streamlink: {Line}", Stream.Key, line));

        // Сессия начинается, когда в файл пришли первые байты, а не когда запустился процесс:
        // streamlink может несколько секунд искать поток.
        _ = Task.Run(async () =>
        {
            while (!process.IsCompleted && !cts.IsCancellationRequested)
            {
                var info = new FileInfo(filePath);
                if (info.Exists && info.Length > 0)
                {
                    session = new RecordingSession { FilePath = filePath, StartedAt = DateTimeOffset.UtcNow };
                    lock (_lock) _sessions.Add(session);
                    _logger.LogInformation("[{Stream}] Запись пошла: {File}", Stream.Key, Path.GetFileName(filePath));
                    firstBytes.TrySetResult(session);
                    return;
                }
                try { await Task.Delay(200, cts.Token); } catch (OperationCanceledException) { break; }
            }
        });

        var completion = Task.Run(async () =>
        {
            try
            {
                var result = await process;
                _logger.LogInformation("[{Stream}] streamlink завершился с кодом {Code}. {Tail}",
                    Stream.Key, result.ExitCode, result.StdErrTail);
            }
            catch (OperationCanceledException)
            {
                // Остановили сами (смена файла или выход).
            }
            catch (Exception ex)
            {
                _logger.LogError("[{Stream}] {Error}", Stream.Key, ex.Message);
            }
            finally
            {
                if (session is not null) session.EndedAt = DateTimeOffset.UtcNow;
                firstBytes.TrySetCanceled();
            }
        });

        return new Recording(firstBytes.Task, completion, cts);
    }

    /// <summary>Один запущенный процесс streamlink.</summary>
    private sealed record Recording(Task FirstBytes, Task Completion, CancellationTokenSource Cts)
    {
        public async Task StopAsync()
        {
            await Cts.CancelAsync();
            try { await Completion; } catch { /* уже залогировано */ }
            Cts.Dispose();
        }
    }
}
