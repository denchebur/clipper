using Microsoft.Extensions.Logging;

namespace Clipper.Desktop.Services;

/// <summary>Отдаёт строки логов ядра в окно клиента (вкладка «Лог»).</summary>
public sealed class UiLoggerProvider(Action<string> write) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new UiLogger(ShortName(categoryName), write);

    public void Dispose() { }

    // "Clipper.Recorder.StreamlinkRecorder" → "StreamlinkRecorder"
    private static string ShortName(string category) => category[(category.LastIndexOf('.') + 1)..];

    private sealed class UiLogger(string category, Action<string> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var level = logLevel switch
            {
                LogLevel.Warning => "ВНИМАНИЕ ",
                LogLevel.Error or LogLevel.Critical => "ОШИБКА ",
                _ => "",
            };
            var text = $"{DateTime.Now:HH:mm:ss}  {level}{category}: {formatter(state, exception)}";
            if (exception is not null) text += $" ({exception.Message})";
            write(text);
        }
    }
}
