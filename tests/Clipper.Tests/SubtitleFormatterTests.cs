using Clipper.Core.Models;
using Clipper.Core.Subtitles;

namespace Clipper.Tests;

public class SubtitleFormatterTests
{
    private static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void LongSegment_IsSplitIntoShortLines_KeepingTotalTime()
    {
        var segments = new[] { new SubtitleSegment(S(1), S(7), "это очень длинная фраза которую надо разбить на несколько строк") };

        var lines = SubtitleFormatter.SplitForShorts(segments, maxChars: 20);

        Assert.True(lines.Count > 1);
        Assert.All(lines, l => Assert.True(l.Text.Length <= 20));
        Assert.Equal(S(1), lines[0].Start);
        Assert.Equal(S(7), lines[^1].End);
        for (var i = 1; i < lines.Count; i++)
            Assert.Equal(lines[i - 1].End, lines[i].Start);
    }

    [Fact]
    public void EmptySegments_AreDropped()
    {
        var lines = SubtitleFormatter.SplitForShorts(new[] { new SubtitleSegment(S(0), S(1), "  ") }, 20);
        Assert.Empty(lines);
    }

    [Fact]
    public void Srt_HasNumberedBlocksAndCommaMilliseconds()
    {
        var srt = SubtitleFormatter.ToSrt(new[]
        {
            new SubtitleSegment(S(0.5), S(2.25), "привет"),
            new SubtitleSegment(TimeSpan.FromMinutes(1) + S(3), TimeSpan.FromMinutes(1) + S(4), "мир"),
        });

        Assert.Equal("1\n00:00:00,500 --> 00:00:02,250\nпривет\n\n2\n00:01:03,000 --> 00:01:04,000\nмир\n\n", srt);
    }
}
