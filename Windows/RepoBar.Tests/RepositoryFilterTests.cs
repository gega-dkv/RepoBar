using RepoBar.Core.Models;
using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class RepositoryFilterTests
{
    [Fact]
    public void PinnedRepositoriesBypassForkAndArchivedFilters()
    {
        Repository fork = Repo("owner/fork", isFork: true);
        Repository archived = Repo("owner/archived", isArchived: true);
        Repository regular = Repo("owner/regular");

        IReadOnlyList<Repository> filtered = RepositoryFilter.Apply(
            [fork, archived, regular],
            includeForks: false,
            includeArchived: false,
            pinned: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "owner/fork",
                "owner/archived",
            });

        Assert.Equal(["owner/fork", "owner/archived", "owner/regular"], filtered.Select(repository => repository.FullName));
    }

    [Fact]
    public void OnlyWithIssuesOrPullRequestsMatchesEitherCondition()
    {
        Repository noWork = Repo("owner/quiet");
        Repository withIssues = Repo("owner/issues", openIssues: 2);
        Repository withPulls = Repo("owner/pulls", openPulls: 1);

        IReadOnlyList<Repository> filtered = RepositoryFilter.Apply(
            [noWork, withIssues, withPulls],
            includeForks: true,
            includeArchived: true,
            onlyWith: new RepositoryOnlyWith(RequireIssues: true, RequirePullRequests: true));

        Assert.Equal(["owner/issues", "owner/pulls"], filtered.Select(repository => repository.FullName));
    }

    private static Repository Repo(
        string fullName,
        bool isFork = false,
        bool isArchived = false,
        int openIssues = 0,
        int openPulls = 0)
    {
        string[] parts = fullName.Split('/');
        return new Repository(
            id: fullName,
            name: parts[1],
            owner: parts[0],
            isFork: isFork,
            isArchived: isArchived,
            stats: new RepositoryStats(openIssues, openPulls));
    }
}
