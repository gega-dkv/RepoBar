using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class RateLimitStatusFormatterTests
{
    [Fact]
    public void ShowsAvailableBudget()
    {
        RateLimitDisplayState state = RateLimitStatusFormatter.Format(
            new RateLimitSnapshot("core", Limit: 5000, Remaining: 4999, ResetAt: null),
            DateTimeOffset.UtcNow);

        Assert.Equal("core: 4999/5000", state.Title);
        Assert.False(state.IsBlocked);
    }

    [Fact]
    public void ShowsResetTimeWhenExhausted()
    {
        DateTimeOffset now = new(2026, 5, 25, 0, 0, 0, TimeSpan.Zero);
        RateLimitDisplayState state = RateLimitStatusFormatter.Format(
            new RateLimitSnapshot("graphql", Limit: 5000, Remaining: 0, ResetAt: now.AddMinutes(12)),
            now);

        Assert.Equal("graphql: exhausted", state.Title);
        Assert.Equal("Resets in 12 min", state.Detail);
        Assert.True(state.IsBlocked);
    }
}
