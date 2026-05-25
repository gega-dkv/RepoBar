using RepoBar.Core.Models;

namespace RepoBar.Core.LocalProjects;

public sealed record LocalRepoStatus(
    string Path,
    string Name,
    string? FullName,
    string Branch,
    bool IsClean,
    int? AheadCount,
    int? BehindCount,
    LocalSyncState SyncState,
    LocalDirtyCounts? DirtyCounts = null,
    IReadOnlyList<string>? DirtyFiles = null,
    string? WorktreeName = null,
    string? UpstreamBranch = null,
    DateTimeOffset? LastFetchAt = null)
{
    public IReadOnlyList<string> DirtyFiles { get; init; } = DirtyFiles ?? [];

    public string DisplayName => FullName ?? Name;

    public string SyncDetail =>
        SyncState switch
        {
            LocalSyncState.Synced => "Up to date",
            LocalSyncState.Behind => BehindCount is { } behind ? $"Behind {behind}" : "Behind",
            LocalSyncState.Ahead => AheadCount is { } ahead ? $"Ahead {ahead}" : "Ahead",
            LocalSyncState.Diverged => "Diverged",
            LocalSyncState.Dirty => DirtyCounts is { IsEmpty: false } counts ? $"Dirty ({counts.Summary})" : "Dirty",
            _ => "No upstream",
        };

    public bool CanAutoSync =>
        IsClean
        && SyncState == LocalSyncState.Behind
        && (AheadCount ?? 0) == 0
        && !Branch.Equals("detached", StringComparison.OrdinalIgnoreCase);
}

public sealed record LocalDirtyCounts(int Added, int Modified, int Deleted)
{
    public bool IsEmpty => Added == 0 && Modified == 0 && Deleted == 0;

    public string Summary
    {
        get
        {
            List<string> parts = [];
            if (Added > 0)
            {
                parts.Add($"+{Added}");
            }

            if (Deleted > 0)
            {
                parts.Add($"-{Deleted}");
            }

            if (Modified > 0)
            {
                parts.Add($"~{Modified}");
            }

            return string.Join(" ", parts);
        }
    }
}

public enum LocalSyncState
{
    Synced,
    Behind,
    Ahead,
    Diverged,
    Dirty,
    Unknown,
}

public static class LocalSyncStateResolver
{
    public static LocalSyncState Resolve(bool isClean, int? ahead, int? behind)
    {
        if (!isClean)
        {
            return LocalSyncState.Dirty;
        }

        if (ahead is null || behind is null)
        {
            return LocalSyncState.Unknown;
        }

        return (ahead.Value, behind.Value) switch
        {
            (0, 0) => LocalSyncState.Synced,
            (0, > 0) => LocalSyncState.Behind,
            (> 0, 0) => LocalSyncState.Ahead,
            (> 0, > 0) => LocalSyncState.Diverged,
            _ => LocalSyncState.Unknown,
        };
    }
}

public sealed class LocalRepoIndex
{
    private readonly Dictionary<string, LocalRepoStatus> byFullName;
    private readonly Dictionary<string, LocalRepoStatus> byPath;
    private readonly Dictionary<string, IReadOnlyList<LocalRepoStatus>> byFullNameLowercase;
    private readonly Dictionary<string, IReadOnlyList<LocalRepoStatus>> byNameLowercase;
    private readonly IReadOnlyDictionary<string, string> preferredPathsByFullName;

    public LocalRepoIndex(
        IEnumerable<LocalRepoStatus> statuses,
        IReadOnlyDictionary<string, string>? preferredPathsByFullName = null)
    {
        All = statuses.ToList();
        this.preferredPathsByFullName = preferredPathsByFullName ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        byFullName = All
            .Where(status => status.FullName is not null)
            .GroupBy(status => status.FullName!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => PreferredStatus(group), StringComparer.Ordinal);
        byPath = All
            .GroupBy(status => NormalizePath(status.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => PreferredStatus(group), StringComparer.OrdinalIgnoreCase);
        byFullNameLowercase = All
            .Where(status => status.FullName is not null)
            .GroupBy(status => status.FullName!.ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LocalRepoStatus>)group.ToList(), StringComparer.Ordinal);
        byNameLowercase = All
            .GroupBy(status => status.Name.ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LocalRepoStatus>)group.ToList(), StringComparer.Ordinal);
    }

    public static LocalRepoIndex Empty { get; } = new([]);

    public IReadOnlyList<LocalRepoStatus> All { get; }

    public LocalRepoStatus? StatusFor(Repository repository) => StatusForFullName(repository.FullName) ?? UniqueStatusForName(repository.Name);

    public LocalRepoStatus? StatusForFullName(string fullName)
    {
        if (preferredPathsByFullName.TryGetValue(fullName, out string? preferredPath)
            && byPath.TryGetValue(NormalizePath(preferredPath), out LocalRepoStatus? preferred))
        {
            return preferred;
        }

        if (byFullName.TryGetValue(fullName, out LocalRepoStatus? exact))
        {
            return exact;
        }

        if (byFullNameLowercase.TryGetValue(fullName.ToLowerInvariant(), out IReadOnlyList<LocalRepoStatus>? matches))
        {
            return PreferredStatus(matches);
        }

        string? name = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return name is null ? null : UniqueStatusForName(name);
    }

    public LocalRepoStatus? StatusContainingPath(string path)
    {
        string normalized = NormalizePath(path);
        if (byPath.TryGetValue(normalized, out LocalRepoStatus? exact))
        {
            return exact;
        }

        return All
            .Where(status =>
            {
                string repoPath = NormalizePath(status.Path);
                return normalized.Equals(repoPath, StringComparison.OrdinalIgnoreCase)
                       || normalized.StartsWith(repoPath + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(status => status.Path.Length)
            .FirstOrDefault();
    }

    private LocalRepoStatus? UniqueStatusForName(string name)
    {
        if (!byNameLowercase.TryGetValue(name.ToLowerInvariant(), out IReadOnlyList<LocalRepoStatus>? matches) || matches.Count != 1)
        {
            return null;
        }

        return matches[0];
    }

    private static LocalRepoStatus PreferredStatus(IEnumerable<LocalRepoStatus> statuses) =>
        statuses
            .OrderBy(status => status.WorktreeName is null ? 0 : 1)
            .ThenBy(status => status.Path, StringComparer.OrdinalIgnoreCase)
            .First();

    private static string NormalizePath(string path) => System.IO.Path.GetFullPath(path);
}
