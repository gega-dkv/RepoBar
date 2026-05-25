using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public interface IRepositoryService
{
    SourceControlProvider Provider { get; }

    ProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<Repository>> RepositoryListAsync(int? limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Repository>> CachedRepositoryListAsync(int? limit, CancellationToken cancellationToken = default);

    Task<UserIdentity> CurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Repository>> SearchRepositoriesAsync(string query, CancellationToken cancellationToken = default);

    Task<Repository> FullRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequestSummary>> RecentPullRequestsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunSummary>> RecentWorkflowRunsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscussionSummary>> RecentDiscussionsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItemSummary>> RepositoryContentsAsync(string owner, string name, string? path = null, CancellationToken cancellationToken = default);

    Task<byte[]> RepositoryFileContentsAsync(string owner, string name, string path, CancellationToken cancellationToken = default);

    Task<TrafficSummary?> TrafficAsync(string owner, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeatmapCell>> RepositoryHeatmapAsync(string owner, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeatmapCell>> UserContributionHeatmapAsync(string login, CancellationToken cancellationToken = default);

    Task<RateLimitResourcesSnapshot> RefreshRateLimitResourcesAsync(CancellationToken cancellationToken = default);
}

public sealed record UserIdentity(
    string Username,
    Uri Host);
