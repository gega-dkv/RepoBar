namespace RepoBar.Core.Models;

public sealed record RepositoryStats(
    int OpenIssues,
    int OpenPulls,
    int Stars = 0,
    int Forks = 0,
    DateTimeOffset? PushedAt = null);
