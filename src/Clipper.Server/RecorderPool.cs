using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Recorder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clipper.Server;

/// <summary>
/// Все рекордеры: по одному на каждый стрим из настроек. Пишут одновременно и независимо —
/// если один стрим оффлайн или оборвался, остальные продолжают.
/// </summary>
public sealed class RecorderPool : BackgroundService
{
    private readonly ILogger<RecorderPool> _logger;

    public RecorderPool(ClipperOptions options, ILoggerFactory loggers)
    {
        _logger = loggers.CreateLogger<RecorderPool>();
        Recorders = options.GetStreams()
            .Select(s => (IStreamRecorder)new StreamlinkRecorder(s, options, loggers.CreateLogger<StreamlinkRecorder>()))
            .ToList();
    }

    public IReadOnlyList<IStreamRecorder> Recorders { get; }

    public IReadOnlyList<StreamSource> Streams => Recorders.Select(r => r.Stream).ToList();

    public IStreamRecorder? Find(StreamSource stream) =>
        Recorders.FirstOrDefault(r => r.Stream.Key.Equals(stream.Key, StringComparison.OrdinalIgnoreCase));

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Recorders.Count == 0)
        {
            _logger.LogWarning("Не задано ни одного стрима (Streams в настройках) — запись не запущена.");
            return Task.CompletedTask;
        }
        _logger.LogInformation("Пишу стримы: {Streams}", string.Join(", ", Streams.Select(s => s.Key)));
        return Task.WhenAll(Recorders.Select(r => r.RunAsync(stoppingToken)));
    }
}
