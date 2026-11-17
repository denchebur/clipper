namespace Clipper.Core;

/// <summary>
/// Все настройки системы. Заполняются из секции "Clipper" файла appsettings.json.
/// </summary>
public sealed class ClipperOptions
{
    public const string SectionName = "Clipper";

    /// <summary>Адрес HTTP API ядра. Его же указывает расширение в своих настройках.</summary>
    public string ListenUrl { get; set; } = "http://127.0.0.1:5055";

    /// <summary>
    /// Ссылки на стримы, которые пишем одновременно, например https://www.twitch.tv/имя_канала.
    /// Клипы каждого стрима попадают в свою папку: clips/имя_канала/дата/.
    /// </summary>
    public List<string> Streams { get; set; } = new();

    /// <summary>Старая настройка (одна ссылка). Если Streams пуст, используется она.</summary>
    public string StreamUrl { get; set; } = "";

    /// <summary>Все стримы из настроек в разобранном виде (без пустых и повторов).</summary>
    public IReadOnlyList<Models.StreamSource> GetStreams() =>
        (Streams.Count > 0 ? Streams : new List<string> { StreamUrl })
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(Models.StreamSource.FromUrl)
            .DistinctBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

    /// <summary>
    /// Раз в столько минут запись начинается в новый файл (старый дописывается
    /// ещё немного, чтобы клип на стыке не потерялся). Нужно, чтобы старые куски можно было удалять.
    /// </summary>
    public double RotateRecordingMinutes { get; set; } = 30;

    /// <summary>Сколько минут хранить записи стрима. 0 — не удалять. Готовые клипы не удаляются никогда.</summary>
    public double KeepRecordingsMinutes { get; set; } = 120;

    public VerticalVideoOptions Vertical { get; set; } = new();

    public SubtitleOptions Subtitles { get; set; } = new();

    public YouTubeOptions YouTube { get; set; } = new();
}

/// <summary>Загрузка клипов в YouTube Shorts через YouTube Data API.</summary>
public sealed class YouTubeOptions
{
    /// <summary>true — загружать каждый готовый клип сразу; false — только кнопкой «В Shorts».</summary>
    public bool AutoUpload { get; set; } = false;

    /// <summary>OAuth-ключ «Desktop app» из Google Cloud Console (см. README).</summary>
    public string ClientSecretsPath { get; set; } = "client_secret.json";

    /// <summary>Где хранится токен входа в Google (чтобы не входить каждый раз).</summary>
    public string TokenDir { get; set; } = "youtube-token";

    /// <summary>private, unlisted или public.</summary>
    public string Privacy { get; set; } = "private";

    /// <summary>Шаблон названия. Подстановки: {text} — начало речи из субтитров, {channel}, {date}.</summary>
    public string TitleTemplate { get; set; } = "{text} #Shorts";

    /// <summary>Шаблон описания. Подстановки как в названии плюс {stream} — ссылка на стрим.</summary>
    public string DescriptionTemplate { get; set; } = "Смотри стрим прямо сейчас: {stream}\n\n#Shorts #stream";

    /// <summary>Теги через запятую.</summary>
    public string Tags { get; set; } = "shorts,stream,twitch";
}

/// <summary>Субтитры: речь распознаётся локально моделью Whisper.</summary>
public sealed class SubtitleOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Вшивать субтитры в видео. Файл .srt рядом с клипом сохраняется в любом случае.</summary>
    public bool BurnIn { get; set; } = true;

    /// <summary>
    /// Модель Whisper: tiny, base, small, medium, largev3turbo.
    /// Чем больше, тем точнее, но медленнее. Скачивается автоматически при первом клипе.
    /// </summary>
    public string Model { get; set; } = "base";

    /// <summary>Язык речи: "auto" (определить сам) или код: ru, en, uk, de...</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Папка для скачанных моделей.</summary>
    public string ModelsDir { get; set; } = "models";

    /// <summary>Максимум символов в одной строке субтитров.</summary>
    public int MaxLineChars { get; set; } = 32;

    /// <summary>Размер шрифта (в единицах libass, для 1080×1920 значение 14 ≈ 90 px).</summary>
    public int FontSize { get; set; } = 14;
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
