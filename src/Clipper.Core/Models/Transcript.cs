namespace Clipper.Core.Models;

/// <summary>Одна строка субтитров. Время считается от начала клипа.</summary>
public sealed record SubtitleSegment(TimeSpan Start, TimeSpan End, string Text);

/// <summary>Результат распознавания речи: язык и строки субтитров.</summary>
public sealed record Transcript(string Language, IReadOnlyList<SubtitleSegment> Segments);
