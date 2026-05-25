namespace RepoBar.Core.Models;

public enum CiStatus
{
    Passing,
    Failing,
    Pending,
    Unknown,
}

public sealed record Release(
    string Name,
    string Tag,
    DateTimeOffset PublishedAt,
    Uri Url);

public sealed record TrafficStats(
    int UniqueVisitors,
    int UniqueCloners);

public sealed record ActivityEvent(
    string Title,
    string Actor,
    DateTimeOffset Date,
    Uri Url,
    string? EventType = null,
    Uri? ActorAvatarUrl = null);

public sealed record HeatmapCell(
    DateOnly Date,
    int Count);

public sealed record Repository
{
    public Repository(
        string id,
        string name,
        string owner,
        RepositoryIdentity? identity = null,
        bool isFork = false,
        bool isArchived = false,
        bool viewerCanRead = true,
        int? sortOrder = null,
        string? error = null,
        DateTimeOffset? rateLimitedUntil = null,
        CiStatus ciStatus = CiStatus.Unknown,
        int? ciRunCount = null,
        RepositoryStats? stats = null,
        Release? latestRelease = null,
        ActivityEvent? latestActivity = null,
        IReadOnlyList<ActivityEvent>? activityEvents = null,
        TrafficStats? traffic = null,
        IReadOnlyList<HeatmapCell>? heatmap = null,
        bool? discussionsEnabled = null)
    {
        Identity = identity ?? RepositoryIdentity.GitHub(id, owner, name);
        Id = id;
        Name = name;
        Owner = owner;
        IsFork = isFork;
        IsArchived = isArchived;
        ViewerCanRead = viewerCanRead;
        SortOrder = sortOrder;
        Error = error;
        RateLimitedUntil = rateLimitedUntil;
        CiStatus = ciStatus;
        CiRunCount = ciRunCount;
        Stats = stats ?? new RepositoryStats(OpenIssues: 0, OpenPulls: 0);
        LatestRelease = latestRelease;
        LatestActivity = latestActivity;
        ActivityEvents = activityEvents ?? [];
        Traffic = traffic;
        Heatmap = heatmap ?? [];
        DiscussionsEnabled = discussionsEnabled;
    }

    public RepositoryIdentity Identity { get; }

    public string Id { get; }

    public string Name { get; }

    public string Owner { get; }

    public bool IsFork { get; }

    public bool IsArchived { get; }

    public bool ViewerCanRead { get; }

    public int? SortOrder { get; init; }

    public string? Error { get; }

    public DateTimeOffset? RateLimitedUntil { get; }

    public CiStatus CiStatus { get; }

    public int? CiRunCount { get; }

    public RepositoryStats Stats { get; }

    public Release? LatestRelease { get; }

    public ActivityEvent? LatestActivity { get; }

    public IReadOnlyList<ActivityEvent> ActivityEvents { get; }

    public TrafficStats? Traffic { get; }

    public IReadOnlyList<HeatmapCell> Heatmap { get; }

    public bool? DiscussionsEnabled { get; }

    public string FullName => Identity.PathWithNamespace;

    public SourceControlProvider Provider => Identity.Provider;

    public DateTimeOffset? ActivityDate => LatestActivity?.Date ?? Stats.PushedAt;

    public Repository WithOrder(int? order) => this with { SortOrder = order };
}
