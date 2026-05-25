using RepoBar.Cli;
using RepoBar.Core.Api;
using RepoBar.Core.Auth;
using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using Xunit;

namespace RepoBar.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task ReposSupportsJsonOutputAndProviderRouting()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create(SourceControlProvider.GitLab);
        CliResult result = await RunAsync(context, "repos", "--json", "--limit", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"provider\": \"GitLab\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("internal/platform", result.Output, StringComparison.Ordinal);
        Assert.Equal(SourceControlProvider.GitLab, context.RepositoryService.Provider);
    }

    [Fact]
    public async Task ReposAppliesDocumentedFilterScopeOwnerAndSortFlags()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult owner = await RunAsync(context, "repos", "--owner", "other", "--sort", "stars", "--json");
        await RunAsync(context, "pin", "openclaw/RepoBar");
        CliResult pinned = await RunAsync(context, "repos", "--pinned-only", "--json");
        await RunAsync(context, "hide", "openclaw/RepoBar");
        CliResult hidden = await RunAsync(context, "repos", "--scope", "hidden", "--json");

        Assert.Equal(0, owner.ExitCode);
        Assert.Contains("other/Quiet", owner.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("openclaw/RepoBar", owner.Output, StringComparison.Ordinal);
        Assert.Contains("openclaw/RepoBar", pinned.Output, StringComparison.Ordinal);
        Assert.Contains("openclaw/RepoBar", hidden.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReposAppliesMineReleaseEventAndAgeFlags()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult mine = await RunAsync(context, "repos", "--mine", "--json");
        CliResult release = await RunAsync(context, "repos", "--release", "--json");
        CliResult eventResult = await RunAsync(context, "repos", "--event", "--json");
        CliResult age = await RunAsync(context, "repos", "--age", "9999d", "--json");

        Assert.Equal(0, mine.ExitCode);
        Assert.Contains("octocat/Mine", mine.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("openclaw/RepoBar", mine.Output, StringComparison.Ordinal);
        Assert.Contains("openclaw/RepoBar", release.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("other/Quiet", release.Output, StringComparison.Ordinal);
        Assert.Contains("openclaw/RepoBar", eventResult.Output, StringComparison.Ordinal);
        Assert.Contains("\"count\": 3", age.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepoAndRecentCommandsEmitPlainDeterministicOutput()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult repo = await RunAsync(context, "repo", "openclaw/RepoBar", "--plain");
        CliResult issues = await RunAsync(context, "issues", "openclaw/RepoBar", "--limit", "1", "--plain");
        CliResult discussions = await RunAsync(context, "discussions", "openclaw/RepoBar", "--plain");

        Assert.Equal(0, repo.ExitCode);
        Assert.Contains("openclaw/RepoBar", repo.Output, StringComparison.Ordinal);
        Assert.Equal(0, issues.ExitCode);
        Assert.Contains("#12\tBug", issues.Output, StringComparison.Ordinal);
        Assert.Equal(0, discussions.ExitCode);
        Assert.Contains("General\tRoadmap", discussions.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepoCommandIncludesOptionalTrafficHeatmapAndReleaseDetails()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult result = await RunAsync(context, "repo", "openclaw/RepoBar", "--traffic", "--heatmap", "--release", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"uniqueVisitors\": 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"count\": 5", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"tag\": \"v1.0.0\"", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginStatusAndLogoutUseSharedCredentialStoreWithoutPrintingSecret()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult login = await RunAsync(context, "login", "--token", "ghp_secret", "--json");
        CliResult status = await RunAsync(context, "status");
        CliResult logout = await RunAsync(context, "logout");
        CredentialRecord? savedAfterLogout = await context.CredentialStore.ReadAsync("provider-token", "GitHub:github.com:pat");

        Assert.Equal(0, login.ExitCode);
        Assert.DoesNotContain("ghp_secret", login.Output, StringComparison.Ordinal);
        Assert.Contains("\"tokenStored\": true", login.Output, StringComparison.Ordinal);
        Assert.Contains("Authenticated: True", status.Output, StringComparison.Ordinal);
        Assert.Contains("Logged out.", logout.Output, StringComparison.Ordinal);
        Assert.Null(savedAfterLogout);
    }

    [Fact]
    public async Task LoginUsesSelectedProviderHostForGitLabPat()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create(SourceControlProvider.GitLab);

        CliResult login = await RunAsync(context, "login", "--token", "glpat_secret", "--json");
        CredentialRecord? saved = await context.CredentialStore.ReadAsync("provider-token", "GitLab:gitlab.com:pat");

        Assert.Equal(0, login.ExitCode);
        Assert.NotNull(saved);
        Assert.Equal("glpat_secret", saved.Secret);
        Assert.DoesNotContain("glpat_secret", login.Output, StringComparison.Ordinal);
    }


    [Fact]
    public async Task OAuthLoginUsesBrowserFlowServiceAndStoresMethodWithoutPrintingSecrets()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult login = await RunAsync(context, "login", "--oauth", "--client-id", "client-id", "--client-secret", "client-secret", "--json");
        CliResult status = await RunAsync(context, "status");
        CredentialRecord? saved = await context.CredentialStore.ReadAsync(GitHubOAuthLoginService.TokenService, "GitHub:github.com:oauth");

        Assert.Equal(0, login.ExitCode);
        Assert.Contains("\"method\": \"OAuth\"", login.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret", login.Output, StringComparison.Ordinal);
        Assert.NotNull(saved);
        Assert.Contains("Auth method: OAuth", status.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CacheStatusRateLimitsAndClearUsePersistentCache()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        DateTimeOffset now = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        await context.Cache.SaveApiResponseAsync(new CachedApiResponse(
            "GET /rate_limit",
            "https://api.github.com/rate_limit",
            null,
            200,
            "{}",
            "{}",
            now,
            new RepoBar.Core.Storage.RateLimitSnapshot("core", 5000, 0, now.AddMinutes(30), "blocked", now)));

        CliResult status = await RunAsync(context, "cache", "status", "--json");
        CliResult rateLimits = await RunAsync(context, "rate-limits", "--plain");
        CliResult clear = await RunAsync(context, "cache", "clear", "--json");

        Assert.Contains("\"apiResponseCount\": 1", status.Output, StringComparison.Ordinal);
        Assert.Contains("API limited", rateLimits.Output, StringComparison.Ordinal);
        Assert.Contains("\"cleared\": true", clear.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContributionsAndRefreshUseProviderService()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult contributions = await RunAsync(context, "contributions", "--login", "octocat", "--json");
        CliResult refresh = await RunAsync(context, "refresh", "--limit", "1", "--json");

        Assert.Equal(0, contributions.ExitCode);
        Assert.Contains("\"login\": \"octocat\"", contributions.Output, StringComparison.Ordinal);
        Assert.Contains("\"count\": 1", contributions.Output, StringComparison.Ordinal);
        Assert.Equal(0, refresh.ExitCode);
        Assert.Contains("\"refreshed\": true", refresh.Output, StringComparison.Ordinal);
        Assert.Contains("\"repositoryCount\": 1", refresh.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsShowAndSetPersistThroughContext()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult set = await RunAsync(context, "settings", "set", "repo-limit", "9");
        CliResult show = await RunAsync(context, "settings", "show", "--plain");

        Assert.Equal(0, set.ExitCode);
        Assert.Equal(9, context.Settings.RepoList.DisplayLimit);
        Assert.Contains("Repository limit: 9", show.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsSetCoversActivityAndHeatmapKeys()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult density = await RunAsync(context, "settings", "set", "card-density", "compact");
        CliResult accent = await RunAsync(context, "settings", "set", "accent-tone", "system");
        CliResult activity = await RunAsync(context, "settings", "set", "activity-scope", "all");
        CliResult display = await RunAsync(context, "settings", "set", "heatmap-display", "submenu");
        CliResult span = await RunAsync(context, "settings", "set", "heatmap-span", "6m");
        CliResult show = await RunAsync(context, "settings", "show", "--plain");

        Assert.Equal(0, density.ExitCode);
        Assert.Equal(0, accent.ExitCode);
        Assert.Equal(0, activity.ExitCode);
        Assert.Equal(0, display.ExitCode);
        Assert.Equal(0, span.ExitCode);
        Assert.Equal(CardDensity.Compact, context.Settings.Appearance.CardDensity);
        Assert.Equal(AccentTone.System, context.Settings.Appearance.AccentTone);
        Assert.Equal(GlobalActivityScope.AllActivity, context.Settings.Appearance.ActivityScope);
        Assert.Equal(HeatmapDisplay.Submenu, context.Settings.Heatmap.Display);
        Assert.Equal(HeatmapSpan.SixMonths, context.Settings.Heatmap.Span);
        Assert.Contains("Card density: Compact", show.Output, StringComparison.Ordinal);
        Assert.Contains("Accent tone: System", show.Output, StringComparison.Ordinal);
        Assert.Contains("Activity scope: AllActivity", show.Output, StringComparison.Ordinal);
        Assert.Contains("Heatmap display: Submenu", show.Output, StringComparison.Ordinal);
        Assert.Contains("Heatmap span: SixMonths", show.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VisibilityCommandsPersistPinnedAndHiddenRepositories()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();

        CliResult pin = await RunAsync(context, "pin", "openclaw/RepoBar", "--json");
        CliResult hide = await RunAsync(context, "hide", "openclaw/RepoBar", "--json");
        CliResult show = await RunAsync(context, "show", "openclaw/RepoBar", "--json");
        CliResult unpin = await RunAsync(context, "unpin", "openclaw/RepoBar", "--json");

        Assert.Equal(0, pin.ExitCode);
        Assert.Contains("\"saved\": true", pin.Output, StringComparison.Ordinal);
        Assert.Equal(0, hide.ExitCode);
        Assert.Equal(0, show.ExitCode);
        Assert.Equal(0, unpin.ExitCode);
        Assert.DoesNotContain("openclaw/RepoBar", context.Settings.RepoList.PinnedRepositories);
        Assert.DoesNotContain("openclaw/RepoBar", context.Settings.RepoList.HiddenRepositories);
    }

    [Fact]
    public async Task ChangelogAndMarkdownCommandsRenderLocalFiles()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string changelog = Path.Combine(root, "CHANGELOG.md");
        string markdown = Path.Combine(root, "README.md");
        await File.WriteAllTextAsync(
            changelog,
            """
            # Changelog

            ## v2.0.0
            - Latest

            ## v1.0.0
            - Previous
            """);
        await File.WriteAllTextAsync(
            markdown,
            """
            # RepoBar
            **Windows** `port`
            """);

        CliResult changelogResult = await RunAsync(context, "changelog", changelog, "--release", "v1.0.0", "--json");
        CliResult markdownResult = await RunAsync(context, "markdown", markdown, "--plain");

        Assert.Equal(0, changelogResult.ExitCode);
        Assert.Contains("\"title\": \"v1.0.0\"", changelogResult.Output, StringComparison.Ordinal);
        Assert.Equal(0, markdownResult.ExitCode);
        Assert.Contains("RepoBar", markdownResult.Output, StringComparison.Ordinal);
        Assert.Contains("Windows port", markdownResult.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCommandScansRepositories()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();

        CliResult result = await RunAsync(context, "local", "--root", root, "--depth", "2", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"count\": 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("owner/RepoBar", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCommandSyncFlagUsesSafeSyncGuardrail()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();

        CliResult result = await RunAsync(context, "local", "--root", root, "--sync", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"sync\":", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"attempted\": false", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalDestructiveActionsRequireConfirmation()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");

        CliResult skipped = await RunAsync(context, "local", "reset", repo, "--json");
        CliResult confirmed = await RunAsync(context, "local", "reset", repo, "--yes", "--json");

        Assert.Equal(0, skipped.ExitCode);
        Assert.Contains("\"attempted\": false", skipped.Output, StringComparison.Ordinal);
        Assert.Equal(0, confirmed.ExitCode);
        Assert.Contains("\"succeeded\": true", confirmed.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorktreesCommandListsLocalWorktrees()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");

        CliResult result = await RunAsync(context, "worktrees", repo, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"count\": 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("RepoBar-feature", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalBranchesCommandListsBranches()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");

        CliResult result = await RunAsync(context, "local", "branches", repo, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"count\": 2", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"main\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"tracking\": \"[ahead 1]\"", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCommandUsesShellServiceForLocalPaths()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");

        CliResult result = await RunAsync(context, "open", "finder", repo, "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(repo, Assert.IsType<FakeCliShellService>(context.Shell).OpenedFolder);
        Assert.Contains("\"opened\": true", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckoutCommandClonesIntoLocalProjectsRoot()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string expected = Path.Combine(context.Settings.LocalProjects.RootPath!, "RepoBar");

        CliResult result = await RunAsync(context, "checkout", "openclaw/RepoBar", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"succeeded\": true", result.Output, StringComparison.Ordinal);
        Assert.Contains(expected.Replace("\\", "\\\\", StringComparison.Ordinal), result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArchivesAddListValidateAndStatus()
    {
        TestCliRuntimeContext context = TestCliRuntimeContext.Create();
        string snapshot = CreateArchiveSnapshot();

        CliResult add = await RunAsync(context, "archives", "add", "sample", "--repo", snapshot);
        CliResult list = await RunAsync(context, "archives", "list", "--plain");
        CliResult validate = await RunAsync(context, "archives", "validate", "sample", "--json");
        CliResult disable = await RunAsync(context, "archives", "disable", "sample", "--json");
        CliResult remove = await RunAsync(context, "archives", "remove", "sample", "--json");

        Assert.Equal(0, add.ExitCode);
        Assert.Contains("sample", list.Output, StringComparison.Ordinal);
        Assert.Contains("\"isValid\": true", validate.Output, StringComparison.Ordinal);
        Assert.Contains("\"enabled\": false", disable.Output, StringComparison.Ordinal);
        Assert.Contains("\"removed\": \"sample\"", remove.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCommandReturnsErrorOutput()
    {
        CliResult result = await RunAsync(TestCliRuntimeContext.Create(), "unknown");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown command", result.Error, StringComparison.Ordinal);
    }

    private static async Task<CliResult> RunAsync(TestCliRuntimeContext context, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await new CliApplication(context, output, error).RunAsync(args);
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateGitTree()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Cli.Tests", Guid.NewGuid().ToString("N"));
        string repo = Path.Combine(root, "RepoBar");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        return root;
    }

    private static string CreateArchiveSnapshot()
    {
        string snapshot = Path.Combine(Path.GetTempPath(), "RepoBar.Cli.Tests", Guid.NewGuid().ToString("N"), "snapshot");
        string table = Path.Combine(snapshot, "tables", "threads");
        Directory.CreateDirectory(table);
        File.WriteAllText(
            Path.Combine(snapshot, "manifest.json"),
            """
            {"tables":[{"name":"threads","files":["000001.jsonl"],"columns":["repository","title"],"rows":1}]}
            """);
        File.WriteAllText(Path.Combine(table, "000001.jsonl"), """{"repository":"openclaw/RepoBar","title":"One"}""");
        return snapshot;
    }

    private sealed record CliResult(int ExitCode, string Output, string Error);

    private sealed class TestCliRuntimeContext : ICliRuntimeContext
    {
        private readonly SettingsStore settingsStore;

        private TestCliRuntimeContext(
            RepoBarPaths paths,
            SettingsStore settingsStore,
            UserSettings settings,
            IRepositoryService repositoryService,
            ICredentialStore credentialStore,
            PersistentCacheStore cache,
            GitHubArchiveStore archives,
            LocalProjectsService localProjects,
            IOAuthLoginService oAuthLogin,
            ICliShellService shell)
        {
            Paths = paths;
            this.settingsStore = settingsStore;
            Settings = settings;
            RepositoryService = repositoryService;
            CredentialStore = credentialStore;
            Cache = cache;
            Archives = archives;
            LocalProjects = localProjects;
            OAuthLogin = oAuthLogin;
            Shell = shell;
        }

        public RepoBarPaths Paths { get; }

        public UserSettings Settings { get; private set; }

        public IRepositoryService RepositoryService { get; }

        public ICredentialStore CredentialStore { get; }

        public PersistentCacheStore Cache { get; }

        public GitHubArchiveStore Archives { get; }

        public LocalProjectsService LocalProjects { get; }

        public IOAuthLoginService OAuthLogin { get; }

        public ICliShellService Shell { get; }

        public static TestCliRuntimeContext Create(SourceControlProvider provider = SourceControlProvider.GitHub)
        {
            string root = Path.Combine(Path.GetTempPath(), "RepoBar.Cli.Tests", Guid.NewGuid().ToString("N"));
            RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
            paths.EnsureCreated();
            SettingsStore settingsStore = new(paths);
            string projectsRoot = Path.Combine(root, "Projects");
            UserSettings settings = new() { SelectedProvider = provider, LocalProjects = new LocalProjectsSettings { RootPath = projectsRoot } };
            FakeProcessRunner runner = new();
            runner.Set(["branch", "--show-current"], new ProcessRunResult(0, "main\n", ""));
            runner.Set(["status", "--porcelain=v1"], new ProcessRunResult(0, "", ""));
            runner.Set(["rev-list", "--left-right", "--count", "HEAD...@{u}"], new ProcessRunResult(0, "0\t0\n", ""));
            runner.Set(["config", "--get", "remote.origin.url"], new ProcessRunResult(0, "https://github.com/owner/RepoBar.git\n", ""));
            runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], new ProcessRunResult(0, "origin/main\n", ""));
            runner.Set(["reset", "--hard", "@{u}"], new ProcessRunResult(0, "HEAD is now at abc\n", ""));
            runner.Set(["rebase", "@{u}"], new ProcessRunResult(0, "Current branch main is up to date.\n", ""));
            runner.Set(["clone", "https://github.com/openclaw/RepoBar.git", Path.Combine(projectsRoot, "RepoBar")], new ProcessRunResult(0, "Cloning\n", ""));
            runner.Set(
                ["branch", "--all", "--format=%(HEAD)%09%(refname:short)%09%(upstream:short)%09%(upstream:track)"],
                new ProcessRunResult(
                    0,
                    """
                    *	main	origin/main	[ahead 1]
                     	feature	origin/feature	
                    """,
                    ""));
            runner.Set(
                ["worktree", "list", "--porcelain"],
                new ProcessRunResult(
                    0,
                    """
                    worktree C:/Projects/RepoBar-feature
                    HEAD abc123
                    branch refs/heads/feature

                    """,
                    ""));
            return new TestCliRuntimeContext(
                paths,
                settingsStore,
                settings,
                new FakeRepositoryService(provider),
                new FileCredentialStore(paths.DebugAuthDirectory),
                new PersistentCacheStore(paths.CacheDatabasePath),
                new GitHubArchiveStore(),
                new LocalProjectsService(new PhysicalFileSystem(), runner, "git"),
                new FakeOAuthLoginService(new FileCredentialStore(paths.DebugAuthDirectory)),
                new FakeCliShellService());
        }

        public async Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            await settingsStore.SaveAsync(settings, cancellationToken);
            Settings = settings;
        }
    }

    private sealed class FakeCliShellService : ICliShellService
    {
        public string? OpenedFolder { get; private set; }

        public string? TerminalPath { get; private set; }

        public string? PreferredTerminal { get; private set; }

        public void OpenFolder(string path) => OpenedFolder = path;

        public void OpenTerminal(string path, string? preferredTerminal)
        {
            TerminalPath = path;
            PreferredTerminal = preferredTerminal;
        }
    }

    private sealed class FakeRepositoryService(SourceControlProvider provider) : IRepositoryService
    {
        private readonly Repository repository = new(
            "1",
            "RepoBar",
            provider == SourceControlProvider.GitLab ? "internal" : "openclaw",
            identity: provider == SourceControlProvider.GitLab
                ? new RepositoryIdentity(SourceControlProvider.GitLab, "1", "platform", "internal", webUrl: new Uri("https://gitlab.com/internal/platform"))
                : RepositoryIdentity.GitHub("1", "openclaw", "RepoBar", new Uri("https://github.com/openclaw/RepoBar")),
            stats: new RepositoryStats(OpenIssues: 3, OpenPulls: 2, Stars: 42, Forks: 4),
            latestRelease: new Release("Release", "v1.0.0", new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero), new Uri("https://example.com/releases/1")),
            latestActivity: new ActivityEvent("Updated README", "octocat", new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero), new Uri("https://example.com/activity")));

        public SourceControlProvider Provider { get; } = provider;

        public ProviderCapabilities Capabilities => ProviderCapabilities.For(Provider);

        public Task<IReadOnlyList<Repository>> RepositoryListAsync(int? limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Repository>>((Provider == SourceControlProvider.GitLab
                ? GitLabRepositories()
                : GitHubRepositories())
                .Take(limit ?? int.MaxValue)
                .ToArray());

        private Repository[] GitLabRepositories() => [repository];

        private Repository[] GitHubRepositories() =>
            [
                repository,
                new Repository(
                    "2",
                    "Quiet",
                    "other",
                    identity: RepositoryIdentity.GitHub("2", "other", "Quiet", new Uri("https://github.com/other/Quiet")),
                    stats: new RepositoryStats(OpenIssues: 0, OpenPulls: 0, Stars: 120, Forks: 0),
                    latestActivity: new ActivityEvent("Older", "octocat", new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero), new Uri("https://example.com/older"))),
                new Repository(
                    "3",
                    "Mine",
                    "octocat",
                    identity: RepositoryIdentity.GitHub("3", "octocat", "Mine", new Uri("https://github.com/octocat/Mine")),
                    stats: new RepositoryStats(OpenIssues: 1, OpenPulls: 0, Stars: 1, Forks: 0),
                    latestActivity: new ActivityEvent("Mine", "octocat", new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero), new Uri("https://example.com/mine"))),
            ];

        public Task<IReadOnlyList<Repository>> CachedRepositoryListAsync(int? limit, CancellationToken cancellationToken = default) =>
            RepositoryListAsync(limit, cancellationToken);

        public Task<UserIdentity> CurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserIdentity("octocat", new Uri("https://github.com")));

        public Task<IReadOnlyList<Repository>> SearchRepositoriesAsync(string query, CancellationToken cancellationToken = default) =>
            RepositoryListAsync(20, cancellationToken);

        public Task<Repository> FullRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(repository);

        public Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueSummary>>([new IssueSummary(12, "Bug", new Uri("https://example.com/issues/12"), DateTimeOffset.UtcNow, null, "octocat")]);

        public Task<IReadOnlyList<PullRequestSummary>> RecentPullRequestsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PullRequestSummary>>([new PullRequestSummary(9, "Fix", new Uri("https://example.com/pull/9"), DateTimeOffset.UtcNow, null, "octocat", false, "feature", "main")]);

        public Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReleaseSummary>>([new ReleaseSummary("Release", "v1.0.0", new Uri("https://example.com/releases/1"), DateTimeOffset.UtcNow, false, "octocat")]);

        public Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BranchSummary>>([new BranchSummary("main", "abc", true)]);

        public Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TagSummary>>([new TagSummary("v1.0.0", "abc")]);

        public Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CommitSummary>>([new CommitSummary("abc", "Commit", new Uri("https://example.com/commit/abc"), DateTimeOffset.UtcNow)]);

        public Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContributorSummary>>([new ContributorSummary("octocat", null, 12)]);

        public Task<IReadOnlyList<WorkflowRunSummary>> RecentWorkflowRunsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkflowRunSummary>>([new WorkflowRunSummary("build", new Uri("https://example.com/actions/1"), DateTimeOffset.UtcNow, "completed", "success", "main", "push", 1)]);

        public Task<IReadOnlyList<DiscussionSummary>> RecentDiscussionsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DiscussionSummary>>([new DiscussionSummary("Roadmap", new Uri("https://example.com/discussions/1"), DateTimeOffset.UtcNow, "octocat", 3, "General")]);

        public Task<IReadOnlyList<ContentItemSummary>> RepositoryContentsAsync(string owner, string name, string? path = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentItemSummary>>([]);

        public Task<byte[]> RepositoryFileContentsAsync(string owner, string name, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<TrafficSummary?> TrafficAsync(string owner, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<TrafficSummary?>(new TrafficSummary(1, 2));

        public Task<IReadOnlyList<HeatmapCell>> RepositoryHeatmapAsync(string owner, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HeatmapCell>>([new HeatmapCell(new DateOnly(2026, 5, 25), 5)]);

        public Task<IReadOnlyList<HeatmapCell>> UserContributionHeatmapAsync(string login, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HeatmapCell>>([new HeatmapCell(new DateOnly(2026, 5, 25), 4)]);

        public Task<RateLimitResourcesSnapshot> RefreshRateLimitResourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RateLimitResourcesSnapshot([new RateLimitResourceSnapshot("core", 5000, 4999, new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero))]));
    }

    private sealed class FakeOAuthLoginService(ICredentialStore credentialStore) : IOAuthLoginService
    {
        public async Task<OAuthLoginResult> LoginAsync(OAuthLoginRequest request, CancellationToken cancellationToken = default)
        {
            Uri host = GitHubOAuthLoginService.NormalizeHost(request.Host);
            OAuthTokens tokens = new("gho_oauth", "ghr_refresh", new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero));
            await credentialStore.SaveAsync(
                new CredentialRecord(GitHubOAuthLoginService.TokenService, GitHubOAuthLoginService.TokenAccount(host), tokens.ToSecret()),
                cancellationToken);
            await credentialStore.SaveAsync(
                new CredentialRecord(GitHubOAuthLoginService.ClientService, GitHubOAuthLoginService.TokenAccount(host), new OAuthClientCredentials(request.ClientId, request.ClientSecret).ToSecret()),
                cancellationToken);
            return new OAuthLoginResult(SourceControlProvider.GitHub, host, tokens.ExpiresAt, credentialStore.Kind);
        }

        public Task<OAuthTokens?> RefreshIfNeededAsync(Uri host, bool force = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<OAuthTokens?>(new OAuthTokens("gho_oauth", "ghr_refresh", new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero)));
    }
}
