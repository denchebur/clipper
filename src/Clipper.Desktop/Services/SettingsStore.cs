using System.Text.Json;
using Clipper.Core;

namespace Clipper.Desktop.Services;

/// <summary>
/// Хранит настройки клиента в settings.json:
/// Windows — %APPDATA%\Clipper, macOS — ~/Library/Application Support/Clipper, Linux — ~/.config/Clipper.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipper", "settings.json");

    public static ClipperOptions Load()
    {
        ClipperOptions? options = null;
        try
        {
            if (File.Exists(FilePath))
                options = JsonSerializer.Deserialize<ClipperOptions>(File.ReadAllText(FilePath), Json);
        }
        catch (Exception)
        {
            // Повреждённый файл — начинаем с настроек по умолчанию.
        }
        options ??= CreateDefault();

        // Модели Whisper храним рядом с настройками, если путь не задан явно.
        if (!Path.IsPathRooted(options.Subtitles.ModelsDir))
            options.Subtitles.ModelsDir = Path.Combine(Path.GetDirectoryName(FilePath)!, "models");
        return options;
    }

    public static void Save(ClipperOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(options, Json));
    }

    /// <summary>Копия настроек: запущенное ядро не должно меняться, пока пользователь редактирует форму.</summary>
    public static ClipperOptions Clone(ClipperOptions options) =>
        JsonSerializer.Deserialize<ClipperOptions>(JsonSerializer.Serialize(options, Json), Json)!;

    /// <summary>По умолчанию записи и клипы кладём в «Видео/Clipper» (на macOS — ~/Movies/Clipper).</summary>
    private static ClipperOptions CreateDefault()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrEmpty(videos))
            videos = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(videos, "Clipper");

        return new ClipperOptions
        {
            StreamUrl = "",
            RecordingsDir = Path.Combine(root, "recordings"),
            ClipsDir = Path.Combine(root, "clips"),
        };
    }
}
