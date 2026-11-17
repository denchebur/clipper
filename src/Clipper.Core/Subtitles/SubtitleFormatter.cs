using System.Globalization;
using System.Text;
using Clipper.Core.Models;

namespace Clipper.Core.Subtitles;

/// <summary>Подготовка субтитров для вертикальных видео и запись в формат .srt.</summary>
public static class SubtitleFormatter
{
    /// <summary>
    /// Режет длинные фразы на короткие куски (не длиннее maxChars символов),
    /// как принято в Shorts/TikTok. Время куска делится пропорционально длине текста.
    /// </summary>
    public static List<SubtitleSegment> SplitForShorts(IEnumerable<SubtitleSegment> segments, int maxChars)
    {
        var result = new List<SubtitleSegment>();
        foreach (var segment in segments)
        {
            var text = segment.Text.Trim();
            if (text.Length == 0) continue;

            var chunks = SplitWords(text, maxChars);
            var totalChars = chunks.Sum(c => c.Length);
            var duration = segment.End - segment.Start;
            var start = segment.Start;

            for (var i = 0; i < chunks.Count; i++)
            {
                var end = i == chunks.Count - 1
                    ? segment.End
                    : start + duration * ((double)chunks[i].Length / totalChars);
                result.Add(new SubtitleSegment(start, end, chunks[i]));
                start = end;
            }
        }
        return result;
    }

    private static List<string> SplitWords(string text, int maxChars)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > maxChars)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks;
    }

    /// <summary>Текст файла .srt.</summary>
    public static string ToSrt(IReadOnlyList<SubtitleSegment> segments)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            sb.Append(i + 1).Append('\n');
            sb.Append(Time(segments[i].Start)).Append(" --> ").Append(Time(segments[i].End)).Append('\n');
            sb.Append(segments[i].Text).Append("\n\n");
        }
        return sb.ToString();
    }

    private static string Time(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}",
            (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds);
    }
}
