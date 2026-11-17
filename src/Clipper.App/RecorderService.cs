using Clipper.Core.Abstractions;

namespace Clipper.App;

/// <summary>Запускает рекордер в фоне вместе с приложением.</summary>
public sealed class RecorderService(IStreamRecorder recorder) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => recorder.RunAsync(stoppingToken);
}
