using RepoBar.Core.Models;

namespace RepoBar.Core.Support;

public static class RepositoryFilter
{
    public static IReadOnlyList<Repository> Apply(
        IEnumerable<Repository> repositories,
        bool includeForks,
        bool includeArchived,
        ISet<string>? pinned = null,
        RepositoryOnlyWith? onlyWith = null,
        IEnumerable<string>? ownerFilter = null)
    {
        RepositoryOnlyWith activeOnlyWith = onlyWith ?? RepositoryOnlyWith.None;
        HashSet<string> normalizedOwners = Normalize(ownerFilter);
        HashSet<string> pinnedSet = new(pinned ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);

        return repositories
            .Where(repository =>
            {
                if (pinnedSet.Contains(repository.FullName))
                {
                    return true;
                }

                if (!includeForks && repository.IsFork)
                {
                    return false;
                }

                if (!includeArchived && repository.IsArchived)
                {
                    return false;
                }

                if (activeOnlyWith.IsActive && !activeOnlyWith.Matches(repository))
                {
                    return false;
                }

                if (normalizedOwners.Count > 0 && !normalizedOwners.Contains(repository.Owner))
                {
                    return false;
                }

                return true;
            })
            .ToArray();
    }

    private static HashSet<string> Normalize(IEnumerable<string>? values) =>
        new(
            (values ?? [])
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value.Length > 0),
            StringComparer.OrdinalIgnoreCase);
}
