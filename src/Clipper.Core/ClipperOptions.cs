namespace Clipper.Core;

/// <summary>
/// Все настройки системы. Заполняются из секции "Clipper" файла appsettings.json.
/// </summary>
public sealed class ClipperOptions
{
    public const string SectionName = "Clipper";

    /// <summary>Ссылка на стрим, например https://www.twitch.tv/имя_канала.</summary>
    public string StreamUrl { get; set; } = "";

    /// <summary>Качество для streamlink: best, 1080p60, 720p и т.д.</summary>
    public string Quality { get; set; } = "best";

    public string RecordingsDir { get; set; } = "recordings";
    public string ClipsDir { get; set; } = "clips";

    public string StreamlinkPath { get; set; } = "streamlink";
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Сколько секунд взять ДО всплеска. Чат реагирует с опозданием, поэтому берём с запасом.</summary>
    public double SecondsBefore { get; set; } = 25;

    /// <summary>Сколько секунд взять ПОСЛЕ всплеска.</summary>
    public double SecondsAfter { get; set; } = 10;

    /// <summary>Максимальная длина клипа, если несколько всплесков склеились подряд.</summary>
    public double MaxClipSeconds { get; set; } = 60;

    /// <summary>
    /// Поправка на задержку: на сколько секунд сдвинуть время всплеска назад.
    /// Нужна, если зритель смотрит стрим с другой задержкой, чем рекордер.
    /// </summary>
    public double LatencyCorrectionSeconds { get; set; } = 0;

    /// <summary>Через сколько секунд после обрыва перезапускать запись.</summary>
    public double RecorderRestartDelaySeconds { get; set; } = 15;

    public VerticalVideoOptions Vertical { get; set; } = new();
}

public sealed class VerticalVideoOptions
{
    public int Width { get; set; } = 1080;
    public int Height { get; set; } = 1920;

    /// <summary>
    /// "blur" — исходное видео по центру, сверху и снизу размытый фон;
    /// "crop" — обрезать центр кадра до вертикали.
    /// </summary>
    public string Mode { get; set; } = "blur";
}
