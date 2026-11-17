using Clipper.Core.Models;

namespace Clipper.Core.Publishing;

/// <summary>Название, описание и теги ролика по шаблонам из настроек.</summary>
public sealed record ShortsMetadata(string Title, string Description, IReadOnlyList<string> Tags)
{
    public const int MaxTitleLength = 100; // ограничение YouTube
    private const int TextWords = 8;

    public static ShortsMetadata Build(ClipJob job, ClipperOptions options)
    {
        var yt = options.YouTube;
        var channel = job.Stream.Key;
        var text = FirstWords(job.TranscriptText, TextWords);
        if (string.IsNullOrEmpty(text))
            text = $"Момент со стрима {channel}";

        string Fill(string template) => template
            .Replace("\\n", "\n")
            .Replace("{text}", text)
            .Replace("{channel}", channel)
            .Replace("{date}", job.Start.ToLocalTime().ToString("dd.MM.yyyy"))
            .Replace("{stream}", job.Stream.Url)
            .Trim();

        var title = Fill(yt.TitleTemplate).Replace("<", "").Replace(">", ""); // YouTube не принимает < >
        if (title.Length > MaxTitleLength)
            title = title[..(MaxTitleLength - 1)].TrimEnd() + "…";

        var tags = yt.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return new ShortsMetadata(title, Fill(yt.DescriptionTemplate), tags);
    }

    private static string FirstWords(string? text, int count)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join(' ', words.Take(count)).TrimEnd(',', '.', ' ');
        return words.Length > count ? result + "…" : result;
    }
}
