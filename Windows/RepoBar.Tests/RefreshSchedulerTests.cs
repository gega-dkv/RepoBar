using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class RefreshSchedulerTests
{
    [Fact]
    public void SchedulerUsesConfiguredIntervalAndManualRefreshOverridesDueTime()
    {
        DateTimeOffset now = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        RefreshScheduler scheduler = new(TimeSpan.FromMinutes(5), now);

        Assert.False(scheduler.IsDue(now.AddMinutes(4)));
        Assert.True(scheduler.IsDue(now.AddMinutes(5)));

        scheduler.RequestManualRefresh();
        Assert.True(scheduler.IsDue(now.AddMinutes(1)));

        scheduler.RecordSuccess(now.AddMinutes(1));
        Assert.False(scheduler.IsDue(now.AddMinutes(5)));
        Assert.True(scheduler.IsDue(now.AddMinutes(6)));
    }

    [Fact]
    public void SchedulerAppliesBackoffLongerThanNormalInterval()
    {
        DateTimeOffset now = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        RefreshScheduler scheduler = new(TimeSpan.FromMinutes(5), now);

        scheduler.RecordBackoff(now, TimeSpan.FromMinutes(30));

        Assert.Equal(now.AddMinutes(30), scheduler.NextDueAt);
        Assert.False(scheduler.IsDue(now.AddMinutes(29)));
        Assert.True(scheduler.IsDue(now.AddMinutes(30)));
    }

    [Fact]
    public void SchedulerNeverUsesSubMinuteIntervals()
    {
        DateTimeOffset now = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        RefreshScheduler scheduler = new(TimeSpan.FromSeconds(5), now);

        Assert.Equal(TimeSpan.FromMinutes(1), scheduler.Interval);
        Assert.Equal(now.AddMinutes(1), scheduler.NextDueAt);
    }
}
