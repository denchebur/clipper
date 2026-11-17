using Avalonia;

namespace Clipper.Desktop;

// Точка входа десктопного клиента (Windows / macOS / Linux).
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Этот метод нужен и дизайнеру XAML в IDE — не удалять.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
