using RepoBar.Core.Models;
using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class RepositoryAutocompleteTests
{
    [Fact]
    public void ScoreHandlesExactPrefixOwnerFallbackAndSubsequence()
    {
        Repository repository = Make("steipete/RepoBar");

        Assert.Equal(1000, RepositoryAutocomplete.Score(repository, "steipete/repobar"));
        Assert.Equal(700, RepositoryAutocomplete.Score(repository, "steipete/rep"));
        Assert.NotNull(RepositoryAutocomplete.Score(repository, "stp"));
        Assert.NotNull(RepositoryAutocomplete.Score(repository, "stei"));
        Assert.Null(RepositoryAutocomplete.Score(repository, "zzzz"));
        Assert.Null(RepositoryAutocomplete.Score(repository, "  "));
    }

    [Fact]
    public void SuggestionsReturnRecentsForEmptyQueryAndRankMatches()
    {
        Repository[] prefetched =
        [
            Make("steipete/RepoBar"),
            Make("steipete/clawdis"),
            Make("amantus-ai/sweetistics"),
        ];

        IReadOnlyList<Repository> recents = RepositoryAutocomplete.Suggestions("  ", prefetched, limit: 2);
        IReadOnlyList<Repository> filtered = RepositoryAutocomplete.Suggestions("sweetis", prefetched, limit: 8);

        Assert.Equal(["steipete/RepoBar", "steipete/clawdis"], recents.Select(repo => repo.FullName));
        Assert.Equal("amantus-ai/sweetistics", filtered[0].FullName);
        Assert.DoesNotContain(filtered, repo => repo.FullName == "steipete/RepoBar");
    }

    [Fact]
    public void MergeDeduplicatesCaseInsensitivelyAndPicksBestScore()
    {
        IReadOnlyList<ScoredRepository> local = RepositoryAutocomplete.ScoreRepositories([Make("me/Alpha")], "a", sourceRank: 0);
        IReadOnlyList<ScoredRepository> remote = RepositoryAutocomplete.ScoreRepositories([Make("me/alpha"), Make("me/Beta")], "a", sourceRank: 1, bonus: 5);

        IReadOnlyList<Repository> merged = RepositoryAutocomplete.Merge(local, remote, limit: 10);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, repo => repo.FullName == "me/alpha");
        Assert.Contains(merged, repo => repo.FullName == "me/Beta");
    }

    private static Repository Make(string fullName)
    {
        string[] parts = fullName.Split('/', 2);
        return new Repository(fullName, parts[1], parts[0]);
    }
}
