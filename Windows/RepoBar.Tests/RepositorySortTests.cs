using RepoBar.Core.Models;
using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class RepositorySortTests
{
    [Fact]
    public void SortsByActivityDescendingThenName()
    {
        DateTimeOffset now = new(2026, 5, 25, 0, 0, 0, TimeSpan.Zero);
        Repository older = Repo("zeta/older", pushedAt: now.AddDays(-2));
        Repository newer = Repo("alpha/newer", pushedAt: now);
        Repository sameDateByName = Repo("alpha/also-new", pushedAt: now);

        IReadOnlyList<Repository> sorted = RepositorySort.Sorted(
            [older, newer, sameDateByName],
            RepositorySortKey.Activity);

        Assert.Equal(["alpha/also-new", "alpha/newer", "zeta/older"], sorted.Select(repository => repository.FullName));
    }

    [Fact]
    public void SortsByPullRequestPressureBeforeFallbacks()
    {
        Repository quiet = Repo("owner/quiet", openPulls: 0, stars: 100);
        Repository busy = Repo("owner/busy", openPulls: 3, stars: 0);

        IReadOnlyList<Repository> sorted = RepositorySort.Sorted(
            [quiet, busy],
            RepositorySortKey.PullRequests);

        Assert.Equal(["owner/busy", "owner/quiet"], sorted.Select(repository => repository.FullName));
    }

    private static Repository Repo(
        string fullName,
        DateTimeOffset? pushedAt = null,
        int openPulls = 0,
        int stars = 0)
    {
        string[] parts = fullName.Split('/');
        return new Repository(
            id: fullName,
            name: parts[1],
            owner: parts[0],
            stats: new RepositoryStats(
                OpenIssues: 0,
                OpenPulls: openPulls,
                Stars: stars,
                PushedAt: pushedAt));
    }
}
