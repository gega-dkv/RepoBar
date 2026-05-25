using RepoBar.Core.Models;

namespace RepoBar.Core.Support;

public enum RepositorySortKey
{
    Activity,
    Issues,
    PullRequests,
    Stars,
    Name,
    Event,
}

public static class RepositorySort
{
    public static IReadOnlyList<Repository> Sorted(
        IEnumerable<Repository> repositories,
        RepositorySortKey sortKey = RepositorySortKey.Activity) =>
        repositories.Order(new RepositoryComparer(sortKey)).ToArray();

    private sealed class RepositoryComparer(RepositorySortKey sortKey) : IComparer<Repository>
    {
        public int Compare(Repository? x, Repository? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            foreach (RepositorySortKey key in OrderedKeys(sortKey))
            {
                int result = CompareByKey(x, y, key);
                if (result != 0)
                {
                    return result;
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(x.FullName, y.FullName);
        }

        private static IEnumerable<RepositorySortKey> OrderedKeys(RepositorySortKey preferred)
        {
            yield return preferred;

            foreach (RepositorySortKey fallback in new[]
            {
                RepositorySortKey.Activity,
                RepositorySortKey.Issues,
                RepositorySortKey.PullRequests,
                RepositorySortKey.Stars,
            })
            {
                if (fallback != preferred)
                {
                    yield return fallback;
                }
            }
        }

        private static int CompareByKey(Repository left, Repository right, RepositorySortKey key) =>
            key switch
            {
                RepositorySortKey.Activity => CompareDescending(left.ActivityDate, right.ActivityDate),
                RepositorySortKey.Issues => CompareDescendingValue(left.Stats.OpenIssues, right.Stats.OpenIssues),
                RepositorySortKey.PullRequests => CompareDescendingValue(left.Stats.OpenPulls, right.Stats.OpenPulls),
                RepositorySortKey.Stars => CompareDescendingValue(left.Stats.Stars, right.Stats.Stars),
                RepositorySortKey.Name => StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName),
                RepositorySortKey.Event => StringComparer.OrdinalIgnoreCase.Compare(left.LatestActivity?.Title ?? "", right.LatestActivity?.Title ?? ""),
                _ => 0,
            };

        private static int CompareDescending<T>(T? left, T? right)
            where T : struct, IComparable<T>
        {
            if (left is null && right is null)
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            return right.Value.CompareTo(left.Value);
        }

        private static int CompareDescendingValue<T>(T left, T right)
            where T : struct, IComparable<T> =>
            right.CompareTo(left);
    }
}
