using RepoBar.Core.Models;

namespace RepoBar.Core.Support;

public sealed record ScoredRepository(Repository Repository, int Score, int SourceRank);

public static class RepositoryAutocomplete
{
    public static IReadOnlyList<ScoredRepository> ScoreRepositories(
        IEnumerable<Repository> repositories,
        string query,
        int sourceRank,
        int bonus = 0) =>
        repositories
            .Select(repository => (Repository: repository, Score: Score(repository, query)))
            .Where(item => item.Score is not null)
            .Select(item => new ScoredRepository(item.Repository, item.Score!.Value + bonus, sourceRank))
            .ToList();

    public static IReadOnlyList<ScoredRepository> Sort(IEnumerable<ScoredRepository> scored) =>
        scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.SourceRank)
            .ThenBy(item => item.Repository.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<Repository> Merge(
        IEnumerable<ScoredRepository> local,
        IEnumerable<ScoredRepository> remote,
        int limit)
    {
        Dictionary<string, ScoredRepository> best = new(StringComparer.OrdinalIgnoreCase);
        foreach (ScoredRepository item in local.Concat(remote))
        {
            if (!best.TryGetValue(item.Repository.FullName, out ScoredRepository? existing) || item.Score > existing.Score)
            {
                best[item.Repository.FullName] = item;
            }
        }

        return Sort(best.Values).Take(Math.Max(0, limit)).Select(item => item.Repository).ToList();
    }

    public static IReadOnlyList<Repository> Suggestions(
        string query,
        IReadOnlyList<Repository> prefetched,
        int limit,
        int localBonus = 30,
        bool showRecentsForEmptyQuery = true)
    {
        string trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return showRecentsForEmptyQuery ? prefetched.Take(Math.Max(0, limit)).ToList() : [];
        }

        return Sort(ScoreRepositories(prefetched, trimmed, sourceRank: 0, bonus: localBonus))
            .Take(Math.Max(0, limit))
            .Select(item => item.Repository)
            .ToList();
    }

    public static int? Score(Repository repository, string query)
    {
        string trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        string lowerQuery = trimmed.ToLowerInvariant();
        string fullName = repository.FullName.ToLowerInvariant();
        bool hasSlash = lowerQuery.Contains('/', StringComparison.Ordinal);
        if (hasSlash)
        {
            if (fullName == lowerQuery)
            {
                return 1000;
            }

            if (fullName.StartsWith(lowerQuery, StringComparison.Ordinal))
            {
                return 700;
            }
        }

        string[] parts = lowerQuery.Split('/', 2);
        string? ownerQuery = parts.Length > 1 ? parts[0] : null;
        string repoQuery = parts.Length > 1 ? parts[1] : lowerQuery;
        int? ownerScore = ComponentScore(ownerQuery ?? string.Empty, repository.Owner, new ComponentScoreWeights(200, 120, 80, 40));
        int? repoScore = ComponentScore(repoQuery, repository.Name, new ComponentScoreWeights(600, 420, 260, 160));

        int score = 0;
        if (ownerScore is not null && ownerQuery is not null)
        {
            score += ownerScore.Value;
        }

        if (repoScore is not null)
        {
            score += repoScore.Value;
        }

        if (ownerQuery is null)
        {
            if (repoScore is null)
            {
                int? ownerFallback = ComponentScore(lowerQuery, repository.Owner, new ComponentScoreWeights(120, 80, 60, 30));
                if (ownerFallback is null)
                {
                    return null;
                }

                score += ownerFallback.Value;
            }
        }
        else if (ownerScore is null && repoScore is null)
        {
            return null;
        }

        if (ownerScore is not null && repoScore is not null)
        {
            score += 40;
        }

        return score == 0 ? null : score;
    }

    private static int? ComponentScore(string query, string target, ComponentScoreWeights weights)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }

        string lowerTarget = target.ToLowerInvariant();
        if (lowerTarget == query)
        {
            return weights.Exact;
        }

        if (lowerTarget.StartsWith(query, StringComparison.Ordinal))
        {
            return weights.Prefix;
        }

        if (lowerTarget.Contains(query, StringComparison.Ordinal))
        {
            return weights.Substring;
        }

        return query.Length <= 3 && IsSubsequence(query, lowerTarget) ? weights.Subsequence : null;
    }

    private static bool IsSubsequence(string needle, string haystack)
    {
        int needleIndex = 0;
        foreach (char current in haystack)
        {
            if (needleIndex < needle.Length && needle[needleIndex] == current)
            {
                needleIndex++;
            }
        }

        return needleIndex == needle.Length;
    }

    private sealed record ComponentScoreWeights(int Exact, int Prefix, int Substring, int Subsequence);
}
