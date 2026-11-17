using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;

namespace Clipper.Subtitles;

/// <summary>
/// Распознаёт речь локально через Whisper (whisper.cpp). Интернет нужен только один раз —
/// чтобы скачать модель в папку ModelsDir. Работает на Windows, macOS и Linux.
/// </summary>
public sealed class WhisperTranscriber : ITranscriber, IDisposable
{
    private readonly SubtitleOptions _options;
    private readonly ILogger<WhisperTranscriber> _logger;
    // Модель грузится в память один раз и переиспользуется; клипы распознаются по очереди.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private WhisperFactory? _factory;

    public WhisperTranscriber(ClipperOptions options, ILogger<WhisperTranscriber> logger)
    {
        _options = options.Subtitles;
        _logger = logger;
    }

    public async Task<Transcript> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _factory ??= WhisperFactory.FromPath(await EnsureModelAsync(cancellationToken));

            using var processor = _factory.CreateBuilder()
                .WithLanguage(string.IsNullOrWhiteSpace(_options.Language) ? "auto" : _options.Language)
                .Build();

            var segments = new List<SubtitleSegment>();
            var language = _options.Language;
            await using var audio = File.OpenRead(wavPath);
            await foreach (var s in processor.ProcessAsync(audio, cancellationToken))
            {
                language = s.Language;
                var text = s.Text.Trim();
                // Whisper помечает музыку и тишину как [Music], (смех) и т.п. — такое не показываем.
                if (text.Length == 0 || text.StartsWith('[') || text.StartsWith('(')) continue;
                segments.Add(new SubtitleSegment(s.Start, s.End, text));
            }
            return new Transcript(language, segments);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Скачивает модель при первом запуске (base ≈ 150 МБ, small ≈ 470 МБ).</summary>
    private async Task<string> EnsureModelAsync(CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GgmlType>(_options.Model, ignoreCase: true, out var type))
            throw new InvalidOperationException(
                $"Неизвестная модель Whisper '{_options.Model}'. Варианты: {string.Join(", ", Enum.GetNames<GgmlType>())}");

        var dir = Path.GetFullPath(_options.ModelsDir);
        var path = Path.Combine(dir, $"ggml-{type.ToString().ToLowerInvariant()}.bin");
        if (File.Exists(path)) return path;

        Directory.CreateDirectory(dir);
        _logger.LogInformation("Скачиваю модель Whisper '{Model}' в {Path} (один раз)…", type, path);

        var tempPath = path + ".part";
        await using (var model = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, QuantizationType.NoQuantization, cancellationToken))
        await using (var file = File.Create(tempPath))
        {
            await model.CopyToAsync(file, cancellationToken);
        }
        File.Move(tempPath, path, overwrite: true);
        _logger.LogInformation("Модель Whisper скачана");
        return path;
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _lock.Dispose();
    }
}
