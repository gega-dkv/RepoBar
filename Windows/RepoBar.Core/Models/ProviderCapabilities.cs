namespace RepoBar.Core.Models;

public sealed record ProviderCapabilities(
    bool Repositories,
    bool Issues,
    bool PullRequests,
    bool Ci,
    bool Releases,
    bool Tags,
    bool Branches,
    bool Commits,
    bool Contributors,
    bool RepositoryContents,
    bool ContributionCalendar,
    bool TrafficStats,
    bool Discussions,
    bool RateLimitDiagnostics)
{
    public static ProviderCapabilities GitHub { get; } = new(
        Repositories: true,
        Issues: true,
        PullRequests: true,
        Ci: true,
        Releases: true,
        Tags: true,
        Branches: true,
        Commits: true,
        Contributors: true,
        RepositoryContents: true,
        ContributionCalendar: true,
        TrafficStats: true,
        Discussions: true,
        RateLimitDiagnostics: true);

    public static ProviderCapabilities GitLab { get; } = new(
        Repositories: true,
        Issues: true,
        PullRequests: true,
        Ci: true,
        Releases: true,
        Tags: true,
        Branches: true,
        Commits: true,
        Contributors: true,
        RepositoryContents: true,
        ContributionCalendar: false,
        TrafficStats: false,
        Discussions: false,
        RateLimitDiagnostics: true);

    public static ProviderCapabilities BitbucketCloud { get; } = new(
        Repositories: true,
        Issues: true,
        PullRequests: true,
        Ci: true,
        Releases: false,
        Tags: true,
        Branches: true,
        Commits: true,
        Contributors: false,
        RepositoryContents: true,
        ContributionCalendar: false,
        TrafficStats: false,
        Discussions: false,
        RateLimitDiagnostics: true);

    public static ProviderCapabilities ForgejoCompatible { get; } = new(
        Repositories: true,
        Issues: true,
        PullRequests: true,
        Ci: false,
        Releases: true,
        Tags: true,
        Branches: true,
        Commits: true,
        Contributors: true,
        RepositoryContents: true,
        ContributionCalendar: false,
        TrafficStats: false,
        Discussions: false,
        RateLimitDiagnostics: true);

    public static ProviderCapabilities CustomGit { get; } = new(
        Repositories: true,
        Issues: false,
        PullRequests: false,
        Ci: false,
        Releases: false,
        Tags: false,
        Branches: true,
        Commits: true,
        Contributors: false,
        RepositoryContents: false,
        ContributionCalendar: false,
        TrafficStats: false,
        Discussions: false,
        RateLimitDiagnostics: false);

    public static ProviderCapabilities For(SourceControlProvider provider) =>
        provider switch
        {
            SourceControlProvider.GitHub => GitHub,
            SourceControlProvider.GitLab => GitLab,
            SourceControlProvider.BitbucketCloud => BitbucketCloud,
            SourceControlProvider.Forgejo or SourceControlProvider.Gitea => ForgejoCompatible,
            SourceControlProvider.CustomGit => CustomGit,
            _ => CustomGit,
        };
}
