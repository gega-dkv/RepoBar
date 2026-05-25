using RepoBar.Core.Models;

namespace RepoBar.Core.Support;

public sealed record RepositoryOnlyWith(bool RequireIssues = false, bool RequirePullRequests = false)
{
    public static RepositoryOnlyWith None { get; } = new();

    public bool IsActive => RequireIssues || RequirePullRequests;

    public bool Matches(Repository repository)
    {
        bool ok = false;
        if (RequireIssues)
        {
            ok = repository.Stats.OpenIssues > 0;
        }

        if (RequirePullRequests)
        {
            ok = ok || repository.Stats.OpenPulls > 0;
        }

        return ok;
    }
}
