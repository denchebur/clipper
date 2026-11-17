using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Core.Subtitles;

namespace Clipper.Server;

/// <summary>
/// Раз в пару секунд проверяет, какие клипы уже можно вырезать
/// (их конец записан на диск), и отдаёт их нарезчику.
/// </summary>
public sealed class ClipWorker(
    ClipperOptions options,
    ClipPlanner planner,
    RecorderPool recorders,
    IClipCutter cutter,
    ITranscriber transcriber,
    ClipUploader uploader,
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

            var recorder = recorders.Find(job.Stream)
                ?? throw new InvalidOperationException($"Стрим {job.Stream.Key} не записывается.");

            // Лучше всего — файл, где есть весь клип целиком. Если такого нет (запись началась
            // позже начала клипа), берём файл с концом клипа и обрезаем начало.
            var sessions = recorder.Sessions;
            var session = sessions.LastOrDefault(s => s.Covers(job.Start, job.End, now))
                ?? sessions.LastOrDefault(s => s.StartedAt < job.End && (s.EndedAt ?? now) >= job.End)
                ?? throw new InvalidOperationException("Нет записи, которая покрывает этот момент.");

            var start = job.Start > session.StartedAt ? job.Start : session.StartedAt;
            var duration = job.End - start;
            if (duration < MinClipLength)
                throw new InvalidOperationException("Слишком короткий кусок записи для клипа.");

            // clips/имя_канала/2026-11-17/clip_18-12-13.mp4
            var local = start.ToLocalTime();
            var output = Path.GetFullPath(Path.Combine(options.ClipsDir, job.Stream.Key,
                local.ToString("yyyy-MM-dd"), $"clip_{local:HH-mm-ss}.mp4"));

            var offset = start - session.StartedAt;

            string? srtPath = null;
            if (options.Subtitles.Enabled)
            {
                job.Status = ClipJobStatus.Transcribing;
                srtPath = await CreateSubtitlesAsync(session.FilePath, offset, duration, output, job, ct);
                job.SubtitlesPath = srtPath;
            }

            job.Status = ClipJobStatus.Cutting;
            await cutter.CutAsync(session.FilePath, offset, duration, output, ct,
                options.Subtitles.BurnIn ? srtPath : null);

            job.OutputPath = output;
            job.Status = ClipJobStatus.Done;
            logger.LogInformation("Клип готов: {Path}", output);

            // Загрузка идёт в фоне, чтобы не задерживать нарезку следующих клипов.
            if (options.YouTube.AutoUpload)
                _ = Task.Run(() => uploader.UploadAsync(job, ct), ct);
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

    /// <summary>
    /// Распознаёт речь клипа и сохраняет субтитры рядом с ним: clip_….ru.srt.
    /// Возвращает путь к .srt или null, если речи нет или распознать не вышло
    /// (тогда клип всё равно режется, просто без субтитров).
    /// </summary>
    private async Task<string?> CreateSubtitlesAsync(string source, TimeSpan offset, TimeSpan duration,
        string clipPath, ClipJob job, CancellationToken ct)
    {
        var wavPath = Path.ChangeExtension(clipPath, ".wav");
        try
        {
            await cutter.ExtractAudioAsync(source, offset, duration, wavPath, ct);
            var transcript = await transcriber.TranscribeAsync(wavPath, ct);
            job.TranscriptText = string.Join(' ', transcript.Segments.Select(s => s.Text));

            var lines = SubtitleFormatter.SplitForShorts(transcript.Segments, options.Subtitles.MaxLineChars);
            if (lines.Count == 0)
            {
                logger.LogInformation("В клипе не найдено речи — без субтитров");
                return null;
            }

            var language = string.IsNullOrWhiteSpace(transcript.Language) || transcript.Language == "auto"
                ? "sub" : transcript.Language;
            var srtPath = Path.ChangeExtension(clipPath, $".{language}.srt");
            await File.WriteAllTextAsync(srtPath, SubtitleFormatter.ToSrt(lines), ct);
            logger.LogInformation("Субтитры ({Language}, {Count} строк): {Path}", transcript.Language, lines.Count, srtPath);
            return srtPath;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Не удалось сделать субтитры: {Error}. Клип будет без них.", ex.Message);
            return null;
        }
        finally
        {
            if (File.Exists(wavPath)) File.Delete(wavPath);
        }
    }
}
