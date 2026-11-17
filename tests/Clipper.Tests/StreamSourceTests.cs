using Clipper.Core;
using Clipper.Core.Models;

namespace Clipper.Tests;

public class StreamSourceTests
{
    [Theory]
    [InlineData("https://www.twitch.tv/Fubgun", "fubgun", "twitch")]
    [InlineData("https://www.twitch.tv/popout/fubgun/chat", "fubgun", "twitch")]
    [InlineData("https://www.youtube.com/watch?v=abc123", "abc123", "youtube")]
    [InlineData("https://www.youtube.com/@SomeStreamer/live", "@SomeStreamer", "youtube")]
    [InlineData("https://youtu.be/xyz", "xyz", "youtube")]
    public void FromUrl_ExtractsChannelKey(string url, string key, string platform)
    {
        var s = StreamSource.FromUrl(url);
        Assert.Equal(key, s.Key);
        Assert.Equal(platform, s.Platform);
    }

    private static readonly IReadOnlyList<StreamSource> Streams = new[]
    {
        StreamSource.FromUrl("https://www.twitch.tv/alpha"),
        StreamSource.FromUrl("https://www.twitch.tv/beta"),
        StreamSource.FromUrl("https://www.youtube.com/@Gamma/live"),
    };

    private static ChatSpike Spike(string platform, string channel) => new(DateTimeOffset.Now, platform, channel);

    [Fact]
    public void Match_ByChannelName_IgnoringCase()
    {
        Assert.Equal("beta", StreamSource.Match(Spike("twitch", "Beta"), Streams)!.Key);
    }

    [Fact]
    public void Match_YouTubeVideoId_FallsBackToTheOnlyYouTubeStream()
    {
        Assert.Equal("@Gamma", StreamSource.Match(Spike("youtube", "videoId42"), Streams)!.Key);
    }

    [Fact]
    public void Match_UnknownTwitchChannel_IsIgnored_WhenSeveralTwitchStreams()
    {
        Assert.Null(StreamSource.Match(Spike("twitch", "someone_else"), Streams));
    }

    [Fact]
    public void Options_FallBackToOldSingleUrl_AndDropDuplicates()
    {
        Assert.Equal("old", Assert.Single(new ClipperOptions { StreamUrl = "https://www.twitch.tv/old" }.GetStreams()).Key);

        var options = new ClipperOptions { Streams = { "https://www.twitch.tv/a", " ", "https://twitch.tv/A" } };
        Assert.Single(options.GetStreams());
    }
}
