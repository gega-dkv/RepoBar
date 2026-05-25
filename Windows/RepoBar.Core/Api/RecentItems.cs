namespace RepoBar.Core.Api;

public sealed record IssueSummary(
    int Number,
    string Title,
    Uri Url,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CreatedAt,
    string? AuthorLogin);

public sealed record PullRequestSummary(
    int Number,
    string Title,
    Uri Url,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CreatedAt,
    string? AuthorLogin,
    bool IsDraft,
    string? HeadRefName,
    string? BaseRefName);

public sealed record ReleaseSummary(
    string Name,
    string Tag,
    Uri Url,
    DateTimeOffset PublishedAt,
    bool IsPrerelease,
    string? AuthorLogin);

public sealed record BranchSummary(
    string Name,
    string CommitSha,
    bool IsProtected);

public sealed record TagSummary(
    string Name,
    string CommitSha);

public sealed record CommitSummary(
    string Sha,
    string Title,
    Uri? Url,
    DateTimeOffset? AuthoredAt);

public sealed record WorkflowRunSummary(
    string Name,
    Uri Url,
    DateTimeOffset UpdatedAt,
    string? Status,
    string? Conclusion,
    string? Branch,
    string? Event,
    int? RunNumber);

public sealed record DiscussionSummary(
    string Title,
    Uri Url,
    DateTimeOffset UpdatedAt,
    string? AuthorLogin,
    int CommentCount,
    string? CategoryName);

public sealed record ContributorSummary(
    string Name,
    string? Email,
    int Contributions);

public sealed record ContentItemSummary(
    string Name,
    string Path,
    string Type,
    Uri? Url,
    string? Sha);

public sealed record TrafficSummary(
    int UniqueVisitors,
    int UniqueCloners);

public sealed record RateLimitResourceSnapshot(
    string Resource,
    int? Limit,
    int? Remaining,
    DateTimeOffset? ResetAt);

public sealed record RateLimitResourcesSnapshot(
    IReadOnlyList<RateLimitResourceSnapshot> Resources);
