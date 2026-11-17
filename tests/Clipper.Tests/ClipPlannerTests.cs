using Clipper.Core;
using Clipper.Core.Models;
using Clipper.Core.Planning;

namespace Clipper.Tests;

public class ClipPlannerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ClipPlanner CreatePlanner() => new(new ClipperOptions
    {
        SecondsBefore = 20,
        SecondsAfter = 10,
        MaxClipSeconds = 45,
    });

    private static ChatSpike Spike(double secondsFromT0) =>
        new(T0.AddSeconds(secondsFromT0), "twitch", "test", 5, 1);

    [Fact]
    public void Spike_CreatesWindowAroundMoment()
    {
        var job = CreatePlanner().AddSpike(Spike(0));

        Assert.Equal(T0.AddSeconds(-20), job.Start);
        Assert.Equal(T0.AddSeconds(10), job.End);
    }

    [Fact]
    public void CloseSpikes_AreMergedIntoOneClip()
    {
        var planner = CreatePlanner();
        var first = planner.AddSpike(Spike(0));
        var second = planner.AddSpike(Spike(15));

        Assert.Same(first, second);
        Assert.Single(planner.Jobs);
        Assert.Equal(T0.AddSeconds(25), first.End);
    }

    [Fact]
    public void MergedClip_IsLimitedByMaxLength()
    {
        var planner = CreatePlanner();
        planner.AddSpike(Spike(0));
        var job = planner.AddSpike(Spike(25));

        Assert.Equal(TimeSpan.FromSeconds(45), job.Duration);
    }

    [Fact]
    public void FarSpikes_CreateSeparateClips()
    {
        var planner = CreatePlanner();
        planner.AddSpike(Spike(0));
        planner.AddSpike(Spike(120));

        Assert.Equal(2, planner.Jobs.Count);
    }

    [Fact]
    public void TakeReadyJobs_ReturnsOnlyRecordedClips()
    {
        var planner = CreatePlanner();
        planner.AddSpike(Spike(0));
        planner.AddSpike(Spike(120));

        var ready = planner.TakeReadyJobs(T0.AddSeconds(30), TimeSpan.FromSeconds(5));

        Assert.Single(ready);
        Assert.Equal(ClipJobStatus.Cutting, ready[0].Status);
        Assert.Empty(planner.TakeReadyJobs(T0.AddSeconds(30), TimeSpan.FromSeconds(5)));
    }
}
