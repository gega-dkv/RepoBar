using System.Text.Json;
using RepoBar.Core.Api;
using RepoBar.Core.Auth;
using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using RepoBar.Core.Support;

namespace RepoBar.Cli;

public static class CliProgram
{
    public static Task<int> MainAsync(string[] args) =>
        new CliApplication(CliRuntimeContext.CreateDefault(), Console.Out, Console.Error).RunAsync(args);
}

public sealed class CliApplication(ICliRuntimeContext context, TextWriter output, TextWriter error)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        try
        {
            CliOptions options = CliOptions.Parse(args);
            if (options.Help)
            {
                PrintHelp();
                return 0;
            }

            string command = options.CommandOrDefault("repos");
            return command switch
            {
                "repos" => await ReposAsync(options, cancellationToken).ConfigureAwait(false),
                "repo" => await RepoAsync(options, cancellationToken).ConfigureAwait(false),
                "issues" => await RecentAsync(options, "issues", cancellationToken).ConfigureAwait(false),
                "pulls" or "merge-requests" => await RecentAsync(options, "pulls", cancellationToken).ConfigureAwait(false),
                "releases" => await RecentAsync(options, "releases", cancellationToken).ConfigureAwait(false),
                "ci" => await RecentAsync(options, "ci", cancellationToken).ConfigureAwait(false),
                "discussions" => await RecentAsync(options, "discussions", cancellationToken).ConfigureAwait(false),
                "tags" => await RecentAsync(options, "tags", cancellationToken).ConfigureAwait(false),
                "branches" => await RecentAsync(options, "branches", cancellationToken).ConfigureAwait(false),
                "contributors" => await RecentAsync(options, "contributors", cancellationToken).ConfigureAwait(false),
                "commits" => await RecentAsync(options, "commits", cancellationToken).ConfigureAwait(false),
                "activity" => await ActivityAsync(options, cancellationToken).ConfigureAwait(false),
                "contributions" => await ContributionsAsync(options, cancellationToken).ConfigureAwait(false),
                "refresh" => await RefreshAsync(options, cancellationToken).ConfigureAwait(false),
                "changelog" => await ChangelogAsync(options, cancellationToken).ConfigureAwait(false),
                "markdown" => await MarkdownAsync(options, cancellationToken).ConfigureAwait(false),
                "worktrees" => await WorktreesAsync(options, cancellationToken).ConfigureAwait(false),
                "open" => await OpenAsync(options, cancellationToken).ConfigureAwait(false),
                "checkout" => await CheckoutAsync(options, cancellationToken).ConfigureAwait(false),
                "local" => await LocalAsync(options, cancellationToken).ConfigureAwait(false),
                "cache" => await CacheAsync(options, cancellationToken).ConfigureAwait(false),
                "rate-limits" => await RateLimitsAsync(options, cancellationToken).ConfigureAwait(false),
                "settings" => await SettingsAsync(options, cancellationToken).ConfigureAwait(false),
                "pin" or "unpin" or "hide" or "show" => await VisibilityAsync(options, command, cancellationToken).ConfigureAwait(false),
                "login" => await LoginAsync(options, cancellationToken).ConfigureAwait(false),
                "logout" => await LogoutAsync(cancellationToken).ConfigureAwait(false),
                "status" => await StatusAsync(cancellationToken).ConfigureAwait(false),
                "archives" => await ArchivesAsync(options, cancellationToken).ConfigureAwait(false),
                _ => Fail($"Unknown command '{command}'."),
            };
        }
        catch (CliUsageException usage)
        {
            return Fail(usage.Message);
        }
        catch (UnsupportedProviderFeatureException unsupported)
        {
            return Fail(unsupported.Message);
        }
    }

    private async Task<int> ReposAsync(CliOptions options, CancellationToken cancellationToken)
    {
        int limit = options.Number("limit", context.Settings.RepoList.DisplayLimit);
        IReadOnlyList<Repository> fetched = await context.RepositoryService.RepositoryListAsync(null, cancellationToken).ConfigureAwait(false);
        string scope = options.Value("scope") ?? (options.Has("pinned-only") ? "pinned" : "visible");
        HashSet<string> pinned = new(context.Settings.RepoList.PinnedRepositories, StringComparer.OrdinalIgnoreCase);
        HashSet<string> hidden = new(context.Settings.RepoList.HiddenRepositories, StringComparer.OrdinalIgnoreCase);
        IEnumerable<Repository> scoped = scope switch
        {
            "all" => fetched,
            "pinned" => fetched.Where(repository => pinned.Contains(repository.FullName)),
            "hidden" => fetched.Where(repository => hidden.Contains(repository.FullName)),
            "visible" => fetched.Where(repository => !hidden.Contains(repository.FullName)),
            _ => throw new CliUsageException($"Unsupported repo scope '{scope}'."),
        };
        if (options.Has("mine"))
        {
            UserIdentity user = await context.RepositoryService.CurrentUserAsync(cancellationToken).ConfigureAwait(false);
            scoped = scoped.Where(repository => repository.Owner.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
        }

        if (options.Has("release"))
        {
            scoped = scoped.Where(repository => repository.LatestRelease is not null);
        }

        if (options.Has("event"))
        {
            scoped = scoped.Where(repository => repository.LatestActivity is not null);
        }

        if (options.Value("age") is { } age)
        {
            DateTimeOffset since = DateTimeOffset.UtcNow - ParseAge(age);
            scoped = scoped.Where(repository => repository.ActivityDate is not null && repository.ActivityDate >= since);
        }

        Repository[] repositories = RepositorySort.Sorted(
                RepositoryFilter.Apply(
                    scoped,
                    includeForks: context.Settings.RepoList.ShowForks || options.Has("forks"),
                    includeArchived: context.Settings.RepoList.ShowArchived || options.Has("archived"),
                    pinned: pinned,
                    onlyWith: ParseOnlyWith(options.Value("only-with") ?? options.Value("filter")),
                    ownerFilter: SplitCsv(options.Value("owner"))),
                ParseRepositorySort(options.Value("sort") ?? context.Settings.RepoList.MenuSortKey.ToString().ToLowerInvariant()))
            .Take(limit)
            .ToArray();
        if (options.Json)
        {
            return Json(new
            {
                count = repositories.Length,
                provider = context.RepositoryService.Provider.Label(),
                repositories = repositories.Select(RepositoryOutput),
            });
        }

        output.WriteLine($"Repositories ({context.RepositoryService.Provider.Label()}):");
        foreach (Repository repository in repositories)
        {
            output.WriteLine($"{repository.FullName}\tissues:{repository.Stats.OpenIssues}\tprs:{repository.Stats.OpenPulls}\tstars:{repository.Stats.Stars}");
        }

        return 0;
    }

    private async Task<int> RepoAsync(CliOptions options, CancellationToken cancellationToken)
    {
        (string owner, string name) = options.RepositoryTarget();
        Repository repository = await context.RepositoryService.FullRepositoryAsync(owner, name, cancellationToken).ConfigureAwait(false);
        TrafficSummary? traffic = options.Has("traffic")
            ? await context.RepositoryService.TrafficAsync(owner, name, cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyList<HeatmapCell> heatmap = options.Has("heatmap")
            ? await context.RepositoryService.RepositoryHeatmapAsync(owner, name, cancellationToken).ConfigureAwait(false)
            : [];
        ReleaseSummary? release = null;
        if (options.Has("release"))
        {
            IReadOnlyList<ReleaseSummary> releases = await context.RepositoryService.RecentReleasesAsync(owner, name, 1, cancellationToken).ConfigureAwait(false);
            release = releases.Count > 0 ? releases[0] : null;
        }
        if (options.Json)
        {
            return Json(new { repository = RepositoryOutput(repository), traffic, heatmap, release });
        }

        output.WriteLine(repository.FullName);
        output.WriteLine($"Provider: {repository.Provider.Label()}");
        output.WriteLine($"Issues: {repository.Stats.OpenIssues}");
        output.WriteLine($"Pull requests: {repository.Stats.OpenPulls}");
        output.WriteLine($"Stars: {repository.Stats.Stars}");
        if (traffic is not null)
        {
            output.WriteLine($"Traffic: visitors:{traffic.UniqueVisitors} cloners:{traffic.UniqueCloners}");
        }

        if (heatmap.Count > 0)
        {
            output.WriteLine($"Heatmap: days:{heatmap.Count} total:{heatmap.Sum(cell => cell.Count)}");
        }

        if (release is not null)
        {
            output.WriteLine($"Release: {release.Tag} {release.Name}");
        }

        return 0;
    }

    private async Task<int> RecentAsync(CliOptions options, string kind, CancellationToken cancellationToken)
    {
        (string owner, string name) = options.RepositoryTarget();
        int limit = options.Number("limit", 20);
        object payload = kind switch
        {
            "issues" => await context.RepositoryService.RecentIssuesAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "pulls" => await context.RepositoryService.RecentPullRequestsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "releases" => await context.RepositoryService.RecentReleasesAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "ci" => await context.RepositoryService.RecentWorkflowRunsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "discussions" => await context.RepositoryService.RecentDiscussionsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "tags" => await context.RepositoryService.RecentTagsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "branches" => await context.RepositoryService.RecentBranchesAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "contributors" => await context.RepositoryService.TopContributorsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            "commits" => await context.RepositoryService.RecentCommitsAsync(owner, name, limit, cancellationToken).ConfigureAwait(false),
            _ => throw new CliUsageException($"Unsupported recent command '{kind}'."),
        };

        if (options.Json)
        {
            return Json(new { repository = $"{owner}/{name}", kind, items = payload });
        }

        output.WriteLine($"{Title(kind)}: {owner}/{name}");
        foreach (string line in PlainLines(payload))
        {
            output.WriteLine(line);
        }

        return 0;
    }

    private async Task<int> ActivityAsync(CliOptions options, CancellationToken cancellationToken)
    {
        int limit = options.Number("limit", 20);
        IReadOnlyList<Repository> repositories = await context.RepositoryService.RepositoryListAsync(limit, cancellationToken).ConfigureAwait(false);
        var activities = repositories
            .Where(repository => repository.LatestActivity is not null)
            .Select(repository => new
            {
                repository = repository.FullName,
                title = repository.LatestActivity!.Title,
                actor = repository.LatestActivity.Actor,
                date = repository.LatestActivity.Date,
            })
            .ToArray();

        if (options.Json)
        {
            return Json(new { count = activities.Length, activity = activities });
        }

        output.WriteLine("Activity:");
        foreach (var item in activities)
        {
            output.WriteLine($"{item.repository}\t{item.title}\t{item.actor}");
        }

        return 0;
    }

    private async Task<int> ContributionsAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string login = options.Value("login") ?? options.PositionalAt(1) ?? (await context.RepositoryService.CurrentUserAsync(cancellationToken).ConfigureAwait(false)).Username;
        IReadOnlyList<HeatmapCell> cells = await context.RepositoryService.UserContributionHeatmapAsync(login, cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            return Json(new { login, count = cells.Count, contributions = cells });
        }

        int total = cells.Sum(cell => cell.Count);
        output.WriteLine($"Contributions: {login}");
        output.WriteLine($"Days: {cells.Count}");
        output.WriteLine($"Total: {total}");
        return 0;
    }

    private async Task<int> RefreshAsync(CliOptions options, CancellationToken cancellationToken)
    {
        int limit = options.Number("limit", context.Settings.RepoList.DisplayLimit);
        IReadOnlyList<Repository> repositories = await context.RepositoryService.RepositoryListAsync(limit, cancellationToken).ConfigureAwait(false);
        RateLimitResourcesSnapshot rateLimits = await context.RepositoryService.RefreshRateLimitResourcesAsync(cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            return Json(new
            {
                refreshed = true,
                provider = context.RepositoryService.Provider.Label(),
                repositoryCount = repositories.Count,
                rateLimitResourceCount = rateLimits.Resources.Count,
            });
        }

        output.WriteLine($"Refreshed {repositories.Count} repositories from {context.RepositoryService.Provider.Label()}.");
        output.WriteLine($"Rate-limit resources: {rateLimits.Resources.Count}");
        return 0;
    }

    private async Task<int> ChangelogAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string path = ResolveChangelogPath(options.PositionalAt(1));
        string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        ChangelogSummary summary = ChangelogParser.Parse(markdown, options.Value("release"));
        if (options.Json)
        {
            return Json(new { path, count = summary.Sections.Count, summary.Selected, sections = summary.Sections });
        }

        if (summary.Selected is null)
        {
            return Plain("No changelog entries found.");
        }

        output.WriteLine(summary.Selected.Title);
        if (!string.IsNullOrWhiteSpace(summary.Selected.Body))
        {
            output.WriteLine(summary.Selected.Body);
        }

        return 0;
    }

    private async Task<int> MarkdownAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string path = options.PositionalAt(1) ?? throw new CliUsageException("markdown requires a path.");
        if (!File.Exists(path))
        {
            throw new CliUsageException($"Markdown file not found: {path}");
        }

        string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Plain(MarkdownPlainTextRenderer.Render(markdown, options.Number("width", 100), options.Has("no-wrap")));
    }

    private async Task<int> LocalAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string subcommand = options.SubcommandOrDefault("scan");
        if (subcommand == "sync")
        {
            string target = options.PositionalAt(2) ?? throw new CliUsageException("local sync requires a path.");
            string path = await ResolveLocalTargetAsync(target, cancellationToken).ConfigureAwait(false);
            LocalGitSyncResult result = await context.LocalProjects.FastForwardSyncAsync(path, cancellationToken).ConfigureAwait(false);
            return options.Json
                ? Json(result)
                : Plain(result.Succeeded ? "Sync succeeded." : $"Sync skipped: {result.Error}");
        }

        if (subcommand is "rebase" or "reset")
        {
            string target = options.PositionalAt(2) ?? throw new CliUsageException($"local {subcommand} requires a path.");
            string path = await ResolveLocalTargetAsync(target, cancellationToken).ConfigureAwait(false);
            bool confirmed = options.Has("confirm") || options.Has("yes");
            LocalGitSyncResult result = subcommand == "rebase"
                ? await context.LocalProjects.RebaseAsync(path, confirmed, cancellationToken).ConfigureAwait(false)
                : await context.LocalProjects.HardResetAsync(path, confirmed, cancellationToken).ConfigureAwait(false);
            return options.Json
                ? Json(result)
                : Plain(result.Succeeded ? $"{Title(subcommand)} succeeded." : $"{Title(subcommand)} skipped: {result.Error}");
        }

        if (subcommand == "branches")
        {
            string target = options.PositionalAt(2) ?? throw new CliUsageException("local branches requires a path.");
            string path = await ResolveLocalTargetAsync(target, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<LocalBranch> branches = await context.LocalProjects.BranchesAsync(path, cancellationToken).ConfigureAwait(false);
            if (options.Json)
            {
                return Json(new { path, count = branches.Count, branches });
            }

            output.WriteLine($"Branches: {branches.Count}");
            foreach (LocalBranch branch in branches)
            {
                string current = branch.IsCurrent ? "*" : " ";
                string upstream = branch.Upstream ?? "-";
                string tracking = branch.Tracking ?? string.Empty;
                output.WriteLine($"{current} {branch.Name}\t{upstream}\t{tracking}".TrimEnd());
            }

            return 0;
        }

        string root = options.Value("root") ?? context.Settings.LocalProjects.RootPath ?? Directory.GetCurrentDirectory();
        int depth = options.Number("depth", context.Settings.LocalProjects.MaxDepth);
        IReadOnlyList<string> repositories = context.LocalProjects.FindGitRepositories(root, depth);
        int limit = options.Number("limit", repositories.Count);
        List<LocalRepoStatus> statuses = [];
        List<LocalSyncOutput> syncResults = [];
        foreach (string repository in repositories.Take(limit))
        {
            LocalRepoStatus status = await context.LocalProjects.GetStatusAsync(repository, cancellationToken).ConfigureAwait(false);
            statuses.Add(status);
            if (options.Has("sync"))
            {
                LocalGitSyncResult sync = await context.LocalProjects.FastForwardSyncAsync(repository, cancellationToken).ConfigureAwait(false);
                syncResults.Add(new LocalSyncOutput(repository, sync.Attempted, sync.Succeeded, sync.Error));
            }
        }

        if (options.Json)
        {
            return Json(new { root, count = statuses.Count, repositories = statuses, sync = syncResults });
        }

        output.WriteLine($"Local repositories: {statuses.Count}");
        foreach (LocalRepoStatus status in statuses)
        {
            output.WriteLine($"{status.DisplayName}\t{status.Branch}\t{status.SyncDetail}\t{status.Path}");
        }

        foreach (LocalSyncOutput result in syncResults)
        {
            output.WriteLine(result.Succeeded ? $"Synced {result.Path}" : $"Sync skipped {result.Path}: {result.Error}");
        }

        return 0;
    }

    private async Task<int> WorktreesAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string target = options.PositionalAt(1) ?? throw new CliUsageException("worktrees requires a path.");
        string path = await ResolveLocalTargetAsync(target, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LocalWorktree> worktrees = await context.LocalProjects.WorktreesAsync(path, cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            return Json(new { path, count = worktrees.Count, worktrees });
        }

        output.WriteLine($"Worktrees: {worktrees.Count}");
        foreach (LocalWorktree worktree in worktrees)
        {
            string branch = worktree.Branch ?? (worktree.IsDetached ? "detached" : "-");
            output.WriteLine($"{worktree.Path}\t{branch}\t{worktree.Head}");
        }

        return 0;
    }

    private async Task<int> OpenAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string mode = options.PositionalAt(1) ?? throw new CliUsageException("open requires 'finder' or 'terminal'.");
        string target = options.PositionalAt(2) ?? throw new CliUsageException($"open {mode} requires a path or owner/name.");
        string path = await ResolveLocalTargetAsync(target, cancellationToken).ConfigureAwait(false);
        if (mode == "finder")
        {
            context.Shell.OpenFolder(path);
            return options.Json ? Json(new { opened = true, mode, path }) : Plain($"Opened {path}");
        }

        if (mode == "terminal")
        {
            context.Shell.OpenTerminal(path, context.Settings.LocalProjects.PreferredTerminal);
            return options.Json ? Json(new { opened = true, mode, path }) : Plain($"Opened terminal at {path}");
        }

        throw new CliUsageException("open requires 'finder' or 'terminal'.");
    }

    private async Task<int> CheckoutAsync(CliOptions options, CancellationToken cancellationToken)
    {
        (string owner, string name) = options.RepositoryTarget();
        string root = options.Value("root") ?? context.Settings.LocalProjects.RootPath ?? Directory.GetCurrentDirectory();
        string destination = options.Value("destination") ?? Path.Combine(root, name);
        string cloneUrl = CloneUrl(owner, name);
        LocalGitSyncResult result = await context.LocalProjects.CloneAsync(cloneUrl, destination, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded && options.Has("open"))
        {
            context.Shell.OpenFolder(destination);
        }

        return options.Json
            ? Json(new { repository = $"{owner}/{name}", cloneUrl, destination, result })
            : Plain(result.Succeeded ? $"Checked out {owner}/{name} to {destination}" : $"Checkout failed: {result.Error}");
    }

    private async Task<int> CacheAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string subcommand = options.SubcommandOrDefault("status");
        if (subcommand == "clear")
        {
            await context.Cache.ClearAsync(cancellationToken).ConfigureAwait(false);
            return options.Json ? Json(new { cleared = true }) : Plain("Cache cleared.");
        }

        if (subcommand == "rate-limits")
        {
            return await RateLimitsAsync(options, cancellationToken).ConfigureAwait(false);
        }

        CacheDiagnostics diagnostics = await context.Cache.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            return Json(diagnostics);
        }

        output.WriteLine($"Cache: {diagnostics.DatabasePath}");
        output.WriteLine($"REST responses: {diagnostics.ApiResponseCount}");
        output.WriteLine($"GraphQL responses: {diagnostics.GraphQlResponseCount}");
        output.WriteLine($"Rate limits: {diagnostics.RateLimitCount}");
        return 0;
    }

    private async Task<int> RateLimitsAsync(CliOptions options, CancellationToken cancellationToken)
    {
        CacheDiagnostics diagnostics = await context.Cache.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            return Json(new { count = diagnostics.RateLimits.Count, rateLimits = diagnostics.RateLimits });
        }

        output.WriteLine("Rate limits:");
        foreach (RepoBar.Core.Support.RateLimitSnapshot snapshot in diagnostics.RateLimits.Select(rate => new RepoBar.Core.Support.RateLimitSnapshot(rate.Resource, rate.Limit, rate.Remaining, rate.ResetAt, rate.LastError)))
        {
            RateLimitDisplayState display = RateLimitStatusFormatter.Format(snapshot, DateTimeOffset.UtcNow);
            output.WriteLine($"{display.Title}\t{display.Detail}");
        }

        return 0;
    }

    private async Task<int> SettingsAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string subcommand = options.SubcommandOrDefault("show");
        if (subcommand == "set")
        {
            string key = options.PositionalAt(2) ?? throw new CliUsageException("settings set requires a key.");
            string value = options.PositionalAt(3) ?? throw new CliUsageException("settings set requires a value.");
            UserSettings updated = SettingsMutator.Set(context.Settings, key, value);
            await context.SaveSettingsAsync(updated, cancellationToken).ConfigureAwait(false);
            return options.Json ? Json(new { key, value, saved = true }) : Plain($"Saved {key}={value}");
        }

        return options.Json ? Json(context.Settings) : Plain(SettingsMutator.Plain(context.Settings));
    }

    private async Task<int> VisibilityAsync(CliOptions options, string command, CancellationToken cancellationToken)
    {
        string fullName = options.PositionalAt(1) ?? throw new CliUsageException($"{command} requires <owner/name>.");
        if (fullName.Split('/', 2, StringSplitOptions.RemoveEmptyEntries).Length != 2)
        {
            throw new CliUsageException("Repository must be in owner/name form.");
        }

        UserSettings settings = command switch
        {
            "pin" => context.Settings with
            {
                RepoList = context.Settings.RepoList with
                {
                    PinnedRepositories = AddRepository(context.Settings.RepoList.PinnedRepositories, fullName),
                    HiddenRepositories = RemoveRepository(context.Settings.RepoList.HiddenRepositories, fullName),
                },
            },
            "unpin" => context.Settings with
            {
                RepoList = context.Settings.RepoList with
                {
                    PinnedRepositories = RemoveRepository(context.Settings.RepoList.PinnedRepositories, fullName),
                },
            },
            "hide" => context.Settings with
            {
                RepoList = context.Settings.RepoList with
                {
                    HiddenRepositories = AddRepository(context.Settings.RepoList.HiddenRepositories, fullName),
                    PinnedRepositories = RemoveRepository(context.Settings.RepoList.PinnedRepositories, fullName),
                },
            },
            "show" => context.Settings with
            {
                RepoList = context.Settings.RepoList with
                {
                    HiddenRepositories = RemoveRepository(context.Settings.RepoList.HiddenRepositories, fullName),
                },
            },
            _ => throw new CliUsageException($"Unsupported visibility command '{command}'."),
        };

        await context.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        return options.Json
            ? Json(new { repository = fullName, command, saved = true })
            : Plain($"{Title(command)} {fullName}.");
    }

    private async Task<int> LoginAsync(CliOptions options, CancellationToken cancellationToken)
    {
        if (options.Has("oauth"))
        {
            return await OAuthLoginAsync(options, cancellationToken).ConfigureAwait(false);
        }

        string token = options.Value("token") ?? Environment.GetEnvironmentVariable("REPOBAR_PAT") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CliUsageException("login requires --token/REPOBAR_PAT or --oauth with --client-id and --client-secret.");
        }

        SourceControlProvider provider = context.Settings.SelectedProvider;
        Uri host = SelectedAuthHost();
        await context.CredentialStore.SaveAsync(
            new CredentialRecord("provider-token", $"{provider}:{host.Host}:pat", token),
            cancellationToken).ConfigureAwait(false);

        return options.Json
            ? Json(new { authenticated = true, provider = provider.Label(), tokenStored = true, store = context.CredentialStore.Kind.ToString() })
            : Plain($"{provider.Label()} credential stored in {context.CredentialStore.Kind}.");
    }

    private async Task<int> OAuthLoginAsync(CliOptions options, CancellationToken cancellationToken)
    {
        if (context.Settings.SelectedProvider != SourceControlProvider.GitHub)
        {
            throw new CliUsageException("OAuth login is only supported for GitHub providers.");
        }

        string clientId = options.Value("client-id") ?? Environment.GetEnvironmentVariable("REPOBAR_GITHUB_CLIENT_ID") ?? string.Empty;
        string clientSecret = options.Value("client-secret") ?? Environment.GetEnvironmentVariable("REPOBAR_GITHUB_CLIENT_SECRET") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new CliUsageException("OAuth login requires --client-id and --client-secret, or REPOBAR_GITHUB_CLIENT_ID and REPOBAR_GITHUB_CLIENT_SECRET.");
        }

        Uri host = options.Value("host") is { } rawHost ? ParseHttpsHost(rawHost) : SelectedAuthHost();
        int loopbackPort = options.Number("loopback-port", context.Settings.LoopbackPort);
        if (loopbackPort is <= 0 or >= 65536)
        {
            throw new CliUsageException("--loopback-port must be between 1 and 65535.");
        }

        OAuthLoginResult result = await context.OAuthLogin.LoginAsync(
            new OAuthLoginRequest(
                host,
                clientId,
                clientSecret,
                loopbackPort,
                options.Value("scope")),
            cancellationToken).ConfigureAwait(false);
        UserSettings settings = context.Settings with
        {
            SelectedProvider = SourceControlProvider.GitHub,
            AuthMethod = AuthMethod.OAuth,
            LoopbackPort = loopbackPort,
            EnterpriseHost = result.Host.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? null : result.Host,
        };
        await context.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        return options.Json
            ? Json(new { authenticated = true, provider = result.Provider.Label(), method = "OAuth", expiresAt = result.ExpiresAt, store = result.Store.ToString() })
            : Plain($"{result.Provider.Label()} OAuth credential stored in {result.Store}.");
    }

    private async Task<int> LogoutAsync(CancellationToken cancellationToken)
    {
        SourceControlProvider provider = context.Settings.SelectedProvider;
        Uri host = SelectedAuthHost();
        await context.CredentialStore.DeleteAsync("provider-token", $"{provider}:{host.Host}:pat", cancellationToken).ConfigureAwait(false);
        await context.CredentialStore.DeleteAsync(GitHubOAuthLoginService.TokenService, GitHubOAuthLoginService.TokenAccount(host), cancellationToken).ConfigureAwait(false);
        await context.CredentialStore.DeleteAsync(GitHubOAuthLoginService.ClientService, GitHubOAuthLoginService.TokenAccount(host), cancellationToken).ConfigureAwait(false);
        output.WriteLine("Logged out.");
        return 0;
    }

    private async Task<int> StatusAsync(CancellationToken cancellationToken)
    {
        SourceControlProvider provider = context.Settings.SelectedProvider;
        Uri host = SelectedAuthHost();
        CredentialRecord? pat = await context.CredentialStore.ReadAsync("provider-token", $"{provider}:{host.Host}:pat", cancellationToken).ConfigureAwait(false);
        CredentialRecord? oauth = provider == SourceControlProvider.GitHub
            ? await context.CredentialStore.ReadAsync(GitHubOAuthLoginService.TokenService, GitHubOAuthLoginService.TokenAccount(host), cancellationToken).ConfigureAwait(false)
            : null;
        output.WriteLine($"Provider: {provider.Label()}");
        output.WriteLine($"Auth store: {context.CredentialStore.Kind}");
        output.WriteLine($"Authenticated: {pat is not null || oauth is not null}");
        output.WriteLine($"Auth method: {(oauth is not null ? "OAuth" : pat is not null ? "PAT" : "None")}");
        output.WriteLine($"Settings: {context.Paths.SettingsFilePath}");
        output.WriteLine($"Cache: {context.Paths.CacheDatabasePath}");
        return 0;
    }

    private async Task<int> ArchivesAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string subcommand = options.SubcommandOrDefault("list");
        UserSettings settings = context.Settings;
        GitHubArchiveStore archives = context.Archives;
        switch (subcommand)
        {
            case "list":
                return options.Json ? Json(settings.GitHubArchives.Sources) : Plain(string.Join(Environment.NewLine, settings.GitHubArchives.Sources.Select(source => $"{source.Id}\t{source.Name}\t{source.ImportedDatabasePath}")));
            case "status":
            {
                IReadOnlyList<ArchiveStatus> statuses = await Task.WhenAll(settings.GitHubArchives.Sources.Select(source => GitHubArchiveStore.GetStatusAsync(source, cancellationToken))).ConfigureAwait(false);
                return options.Json ? Json(statuses) : Plain(string.Join(Environment.NewLine, statuses.Select(status => $"{status.SourceId}\timported:{status.HasImportedDatabase}\trows:{status.ImportedRows}")));
            }
            case "validate":
            {
                GitHubArchiveSource source = FindArchive(options, settings);
                ArchiveValidationResult validation = await GitHubArchiveStore.ValidateAsync(source, cancellationToken).ConfigureAwait(false);
                return options.Json ? Json(validation) : Plain(validation.IsValid ? "Archive valid." : string.Join(Environment.NewLine, validation.Errors));
            }
            case "update":
            {
                GitHubArchiveSource source = FindArchive(options, settings);
                ArchiveImportResult result = await archives.ImportAsync(source, cancellationToken).ConfigureAwait(false);
                return options.Json ? Json(result) : Plain($"Imported {result.RowCount} rows from {result.SourceId}.");
            }
            case "add":
            {
                string name = options.PositionalAt(2) ?? throw new CliUsageException("archives add requires a name.");
                string repo = options.Value("repo") ?? throw new CliUsageException("archives add requires --repo.");
                string db = options.Value("db") ?? Path.Combine(context.Paths.ArchiveDirectory, $"{name}.sqlite");
                GitHubArchiveSource source = new(name, db, name, LocalRepositoryPath: repo, RemoteUrl: options.Value("remote"), Branch: options.Value("branch") ?? "main");
                settings = settings with { GitHubArchives = settings.GitHubArchives with { Sources = settings.GitHubArchives.Sources.Concat([source]).ToArray() } };
                await context.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
                return options.Json ? Json(source) : Plain($"Added archive {name}.");
            }
            case "remove":
            {
                GitHubArchiveSource source = FindArchive(options, settings);
                settings = settings with { GitHubArchives = settings.GitHubArchives with { Sources = settings.GitHubArchives.Sources.Where(candidate => candidate.Id != source.Id).ToArray() } };
                await context.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
                return options.Json ? Json(new { removed = source.Id }) : Plain($"Removed archive {source.Id}.");
            }
            case "enable":
            case "disable":
            {
                bool enabled = subcommand == "enable";
                GitHubArchiveSource source = FindArchive(options, settings);
                settings = settings with
                {
                    GitHubArchives = settings.GitHubArchives with
                    {
                        Sources = settings.GitHubArchives.Sources
                            .Select(candidate => candidate.Id == source.Id ? candidate with { Enabled = enabled } : candidate)
                            .ToArray(),
                    },
                };
                await context.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
                return options.Json ? Json(new { source = source.Id, enabled }) : Plain($"{(enabled ? "Enabled" : "Disabled")} archive {source.Id}.");
            }
            default:
                throw new CliUsageException($"Unsupported archives command '{subcommand}'.");
        }
    }

    private static GitHubArchiveSource FindArchive(CliOptions options, UserSettings settings)
    {
        string? name = options.PositionalAt(2);
        GitHubArchiveSource? source = name is null
            ? (settings.GitHubArchives.Sources.Count > 0 ? settings.GitHubArchives.Sources[0] : null)
            : settings.GitHubArchives.Sources.FirstOrDefault(source => source.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || source.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return source ?? throw new CliUsageException("Archive source not found.");
    }

    private int Json<T>(T value)
    {
        output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        return 0;
    }

    private int Plain(string value)
    {
        output.WriteLine(value);
        return 0;
    }

    private int Fail(string message)
    {
        error.WriteLine($"error: {message}");
        return 2;
    }

    private void PrintHelp()
    {
        output.WriteLine("RepoBar diagnostics and automation CLI");
        output.WriteLine("Commands: repos, repo, issues, pulls, merge-requests, releases, ci, discussions, tags, branches, contributors, commits, activity, contributions, refresh, changelog, markdown, local, worktrees, open, checkout, cache, rate-limits, settings, pin, unpin, hide, show, login, logout, status, archives");
        output.WriteLine("Auth: login --token <PAT> or login --oauth --client-id <id> --client-secret <secret> [--host <url>] [--loopback-port <port>]");
        output.WriteLine("Output: --json, --plain, --no-color");
    }

    private Uri SelectedAuthHost()
    {
        if (context.Settings.SelectedProvider == SourceControlProvider.GitLab)
        {
            RepositoryHost gitLabHost = context.Settings.RepositoryHosts.FirstOrDefault(host => host.Provider == SourceControlProvider.GitLab) ?? RepositoryHost.GitLabCom;
            return gitLabHost.WebBaseUrl;
        }

        return context.Settings.EnterpriseHost ?? context.Settings.GitHubHost;
    }

    private static Uri ParseHttpsHost(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
        {
            throw new CliUsageException("Host must be an absolute HTTPS URL.");
        }

        try
        {
            return GitHubOAuthLoginService.NormalizeHost(uri);
        }
        catch (ArgumentException error)
        {
            throw new CliUsageException(error.Message);
        }
    }

    private static string ResolveChangelogPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            if (File.Exists(requestedPath))
            {
                return requestedPath;
            }

            throw new CliUsageException($"Changelog file not found: {requestedPath}");
        }

        string directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            foreach (string candidate in new[] { "CHANGELOG.md", "CHANGELOG" })
            {
                string path = Path.Combine(directory, candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent is null || parent == directory)
            {
                break;
            }

            directory = parent;
        }

        throw new CliUsageException("No CHANGELOG.md or CHANGELOG found.");
    }

    private async Task<string> ResolveLocalTargetAsync(string target, CancellationToken cancellationToken)
    {
        if (Directory.Exists(target) || File.Exists(Path.Combine(target, ".git")))
        {
            return target;
        }

        string? preferredPath = context.Settings.LocalProjects.PreferredLocalPathFor(target);
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            return preferredPath;
        }

        string? root = context.Settings.LocalProjects.RootPath;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            foreach (string repositoryPath in context.LocalProjects.FindGitRepositories(root, context.Settings.LocalProjects.MaxDepth))
            {
                LocalRepoStatus status = await context.LocalProjects.GetStatusAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
                if (string.Equals(status.FullName, target, StringComparison.OrdinalIgnoreCase))
                {
                    return status.Path;
                }
            }
        }

        return target;
    }

    private string CloneUrl(string owner, string name)
    {
        Uri host = context.Settings.SelectedProvider switch
        {
            SourceControlProvider.GitLab => new("https://gitlab.com"),
            _ => context.Settings.EnterpriseHost ?? context.Settings.GitHubHost,
        };
        return new Uri(host, $"{owner}/{name}.git").AbsoluteUri;
    }

    private static RepositoryOnlyWith ParseOnlyWith(string? value) =>
        value switch
        {
            null or "" or "all" or "work" => RepositoryOnlyWith.None,
            "issues" => new RepositoryOnlyWith(RequireIssues: true),
            "prs" => new RepositoryOnlyWith(RequirePullRequests: true),
            _ => throw new CliUsageException($"Unsupported repository filter '{value}'."),
        };

    private static RepositorySortKey ParseRepositorySort(string value) =>
        value switch
        {
            "activity" => RepositorySortKey.Activity,
            "issues" => RepositorySortKey.Issues,
            "prs" or "pullrequests" => RepositorySortKey.PullRequests,
            "stars" => RepositorySortKey.Stars,
            "repo" or "name" => RepositorySortKey.Name,
            "event" => RepositorySortKey.Event,
            _ => throw new CliUsageException($"Unsupported repository sort '{value}'."),
        };

    private static TimeSpan ParseAge(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.EndsWith("d", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed[..^1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int days))
        {
            return TimeSpan.FromDays(days);
        }

        if (trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed[..^1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int hours))
        {
            return TimeSpan.FromHours(hours);
        }

        return TimeSpan.FromDays(int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string[] SplitCsv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static object RepositoryOutput(Repository repository) => new
    {
        repository.FullName,
        provider = repository.Provider.Label(),
        issues = repository.Stats.OpenIssues,
        pulls = repository.Stats.OpenPulls,
        stars = repository.Stats.Stars,
        url = repository.Identity.WebUrl,
    };

    private static string[] AddRepository(IReadOnlyList<string> repositories, string fullName) =>
        repositories.Any(repository => repository.Equals(fullName, StringComparison.OrdinalIgnoreCase))
            ? repositories.ToArray()
            : repositories.Concat([fullName]).ToArray();

    private static string[] RemoveRepository(IReadOnlyList<string> repositories, string fullName) =>
        repositories
            .Where(repository => !repository.Equals(fullName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static IEnumerable<string> PlainLines(object payload) =>
        payload switch
        {
            IEnumerable<IssueSummary> issues => issues.Select(item => $"#{item.Number}\t{item.Title}\t{item.AuthorLogin}"),
            IEnumerable<PullRequestSummary> pulls => pulls.Select(item => $"#{item.Number}\t{item.Title}\t{item.AuthorLogin}"),
            IEnumerable<ReleaseSummary> releases => releases.Select(item => $"{item.Tag}\t{item.Name}\t{item.PublishedAt:yyyy-MM-dd}"),
            IEnumerable<WorkflowRunSummary> runs => runs.Select(item => $"{item.Name}\t{item.Status}\t{item.Conclusion}"),
            IEnumerable<DiscussionSummary> discussions => discussions.Select(item => $"{item.CategoryName ?? "-"}\t{item.Title}\tcomments:{item.CommentCount}"),
            IEnumerable<TagSummary> tags => tags.Select(item => $"{item.Name}\t{item.CommitSha}"),
            IEnumerable<BranchSummary> branches => branches.Select(item => $"{item.Name}\t{item.CommitSha}"),
            IEnumerable<ContributorSummary> contributors => contributors.Select(item => $"{item.Name}\t{item.Contributions}"),
            IEnumerable<CommitSummary> commits => commits.Select(item => $"{item.Sha}\t{item.Title}"),
            _ => [payload.ToString() ?? string.Empty],
        };

    private static string Title(string kind) => kind switch
    {
        "ci" => "CI",
        "pulls" => "Pull requests",
        _ => char.ToUpperInvariant(kind[0]) + kind[1..],
    };

    private sealed record LocalSyncOutput(string Path, bool Attempted, bool Succeeded, string? Error);
}

public sealed class CliUsageException(string message) : Exception(message);
