using RepoBar.Core.Api;
using RepoBar.Core.Auth;
using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using RepoBar.Desktop.ViewModels;
using Xunit;

namespace RepoBar.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task CachedStartupPopulatesDashboardAndDetails()
    {
        ShellViewModel viewModel = CreateViewModel();

        await viewModel.LoadCachedStartupAsync();

        Assert.Equal("Cached startup seed", viewModel.CacheStatus);
        Assert.Equal(2, viewModel.Repositories.Count);
        Assert.NotNull(viewModel.SelectedRepository);
        Assert.Contains(viewModel.DetailSections, section => section.Title == "Local State" && section.IsAvailable);
        Assert.Contains(viewModel.DetailSections, section => section.Title == "Traffic" && section.IsAvailable);
    }

    [Fact]
    public async Task GitLabUnsupportedFeaturesAreLabeledUnavailableWithoutErrors()
    {
        ShellViewModel viewModel = CreateViewModel();
        await viewModel.LoadCachedStartupAsync();

        viewModel.SelectedRepository = viewModel.Repositories.Single(repository => repository.ProviderLabel == "GitLab");

        Assert.Contains(viewModel.DetailSections, section => section.Title == "Traffic" && !section.IsAvailable);
        Assert.Contains(viewModel.DetailSections, section => section.Title == "Discussions" && !section.IsAvailable);
        Assert.Contains(viewModel.ProviderOptions, option => option.Label == "GitLab" && option.Availability.Contains("traffic", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PatLoginStoresCredentialAndClearsInput()
    {
        string root = CreateTempRoot();
        FileCredentialStore credentialStore = new(Path.Combine(root, "DebugAuth"));
        ShellViewModel viewModel = CreateViewModel(root, credentialStore: credentialStore);
        viewModel.PatToken = "ghp_secret";

        await viewModel.SavePatAsync();
        CredentialRecord? saved = await credentialStore.ReadAsync("provider-token", "GitHub:github.com:pat");

        Assert.NotNull(saved);
        Assert.Equal("ghp_secret", saved.Secret);
        Assert.Empty(viewModel.PatToken);
        Assert.True(viewModel.IsLoggedIn);
        Assert.DoesNotContain("ghp_secret", viewModel.AccountSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatLoginUsesSelectedProviderHostForGitLab()
    {
        string root = CreateTempRoot();
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        paths.EnsureCreated();
        FileCredentialStore credentialStore = new(paths.DebugAuthDirectory);
        ShellViewModel viewModel = CreateViewModel(
            root,
            credentialStore: credentialStore,
            initialSettings: new UserSettings { SelectedProvider = SourceControlProvider.GitLab });
        viewModel.PatToken = "glpat_secret";

        await viewModel.SavePatAsync();
        CredentialRecord? saved = await credentialStore.ReadAsync("provider-token", "GitLab:gitlab.com:pat");

        Assert.NotNull(saved);
        Assert.Equal("glpat_secret", saved.Secret);
        Assert.DoesNotContain("glpat_secret", viewModel.AccountSummary, StringComparison.Ordinal);
    }


    [Fact]
    public async Task OAuthLoginUsesSharedServiceAndDoesNotRetainClientSecret()
    {
        string root = CreateTempRoot();
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        paths.EnsureCreated();
        FileCredentialStore credentialStore = new(paths.DebugAuthDirectory);
        ShellViewModel viewModel = CreateViewModel(
            root,
            credentialStore: credentialStore,
            oAuthLoginService: new FakeOAuthLoginService(credentialStore));
        viewModel.OAuthClientId = "client-id";
        viewModel.OAuthClientSecret = "client-secret";
        viewModel.OAuthScope = "repo read:org";

        await viewModel.StartOAuthAsync();
        CredentialRecord? saved = await credentialStore.ReadAsync(GitHubOAuthLoginService.TokenService, "GitHub:github.com:oauth");
        UserSettings loaded = await new SettingsStore(paths).LoadAsync();

        Assert.NotNull(saved);
        Assert.Empty(viewModel.OAuthClientSecret);
        Assert.True(viewModel.IsLoggedIn);
        Assert.Equal(AuthMethod.OAuth, loaded.AuthMethod);
        Assert.DoesNotContain("client-secret", viewModel.AccountSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsSavePersistsGeneralLocalAndDiagnosticsValues()
    {
        string root = CreateTempRoot();
        ShellViewModel viewModel = CreateViewModel(root);
        viewModel.DisplayLimit = 9;
        viewModel.RefreshIntervalMinutes = 15;
        viewModel.LaunchAtLogin = true;
        viewModel.DiagnosticsEnabled = true;
        viewModel.LocalProjectsRoot = @"C:\Projects";
        viewModel.LocalProjectsDepth = 6;

        await viewModel.SaveSettingsAsync();
        UserSettings loaded = await new SettingsStore(RepoBarPaths.ForTestRoot(root)).LoadAsync();

        Assert.Equal(9, loaded.RepoList.DisplayLimit);
        Assert.Equal(TimeSpan.FromMinutes(15), loaded.RefreshInterval);
        Assert.True(loaded.LaunchAtLogin);
        Assert.True(loaded.DiagnosticsEnabled);
        Assert.Equal(@"C:\Projects", loaded.LocalProjects.RootPath);
        Assert.Equal(6, loaded.LocalProjects.MaxDepth);
    }

    [Fact]
    public async Task RefreshCommandUsesBackgroundRefreshAfterCachedRender()
    {
        ShellViewModel viewModel = CreateViewModel();
        await viewModel.LoadCachedStartupAsync();

        await viewModel.RefreshAsync(force: true);

        Assert.Contains("refresh", viewModel.CacheStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepositoryCommandsPinHideAndOpenSelectedRepository()
    {
        FakeWindowService windowService = new();
        ShellViewModel viewModel = CreateViewModel(windowService: windowService);
        await viewModel.LoadCachedStartupAsync();

        viewModel.PinSelectedRepositoryCommand.Execute(null);
        Assert.Equal(RepositoryVisibility.Pinned, viewModel.SelectedRepository?.Visibility);

        viewModel.HideSelectedRepositoryCommand.Execute(null);
        Assert.Equal(RepositoryVisibility.Hidden, viewModel.SelectedRepository?.Visibility);

        viewModel.OpenSelectedRepositoryCommand.Execute(null);
        Assert.Equal(new Uri("https://github.com/openclaw/RepoBar"), windowService.OpenedUrl);
    }

    [Fact]
    public async Task RepositoryBrowserRowsFilterWithAutocompleteScoring()
    {
        ShellViewModel viewModel = CreateViewModel();
        await viewModel.LoadCachedStartupAsync();

        viewModel.RepositorySearchQuery = "internal";

        Assert.NotEmpty(viewModel.RepositoryBrowserRows);
        Assert.Equal("internal/platform", viewModel.RepositoryBrowserRows.First().FullName);
        Assert.DoesNotContain(viewModel.RepositoryBrowserRows, row => row.FullName == "openclaw/RepoBar");
    }

    [Fact]
    public async Task ProviderDashboardDataSourceUsesCachedRepositoriesAndCredentialState()
    {
        string root = CreateTempRoot();
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        paths.EnsureCreated();
        FileCredentialStore credentialStore = new(paths.DebugAuthDirectory);
        await credentialStore.SaveAsync(new CredentialRecord("provider-token", "GitHub:github.com:pat", "ghp_secret"));
        ProviderWindowsDashboardDataSource dataSource = new(
            new FakeRepositoryService(),
            credentialStore,
            new PersistentCacheStore(paths.CacheDatabasePath),
            new LocalProjectsService(new PhysicalFileSystem(), new FakeProcessRunner(), "git"));

        WindowsDashboardSnapshot snapshot = await dataSource.LoadCachedAsync(new UserSettings());

        Assert.Equal("Cached startup seed", snapshot.CacheStatus);
        Assert.True(snapshot.IsLoggedIn);
        Assert.Contains("credentials loaded", snapshot.AccountSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("openclaw/RepoBar", snapshot.Repositories.Single().FullName);
    }

    private static ShellViewModel CreateViewModel(
        string? root = null,
        ICredentialStore? credentialStore = null,
        FakeWindowService? windowService = null,
        IOAuthLoginService? oAuthLoginService = null,
        UserSettings? initialSettings = null)
    {
        root ??= CreateTempRoot();
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        paths.EnsureCreated();
        return new ShellViewModel(
            windowService ?? new FakeWindowService(),
            new SampleWindowsDashboardDataSource(),
            new SettingsStore(paths),
            credentialStore ?? new FileCredentialStore(paths.DebugAuthDirectory),
            initialSettings ?? new UserSettings(),
            oAuthLoginService);
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeWindowService : IRepoBarWindowService
    {
        public Uri? OpenedUrl { get; private set; }

        public void ShowDashboard()
        {
        }

        public void OpenUrl(Uri url) => OpenedUrl = url;

        public void Quit()
        {
        }
    }

    private sealed class FakeRepositoryService : IRepositoryService
    {
        private readonly Repository repository = new(
            "1",
            "RepoBar",
            "openclaw",
            identity: RepositoryIdentity.GitHub("1", "openclaw", "RepoBar", new Uri("https://github.com/openclaw/RepoBar")),
            stats: new RepositoryStats(OpenIssues: 1, OpenPulls: 2));

        public SourceControlProvider Provider => SourceControlProvider.GitHub;

        public ProviderCapabilities Capabilities => ProviderCapabilities.GitHub;

        public Task<IReadOnlyList<Repository>> RepositoryListAsync(int? limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Repository>>([repository]);

        public Task<IReadOnlyList<Repository>> CachedRepositoryListAsync(int? limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Repository>>([repository]);

        public Task<UserIdentity> CurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserIdentity("octocat", new Uri("https://github.com")));

        public Task<IReadOnlyList<Repository>> SearchRepositoriesAsync(string query, CancellationToken cancellationToken = default) => RepositoryListAsync(null, cancellationToken);

        public Task<Repository> FullRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default) => Task.FromResult(repository);

        public Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IssueSummary>>([]);

        public Task<IReadOnlyList<PullRequestSummary>> RecentPullRequestsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PullRequestSummary>>([]);

        public Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ReleaseSummary>>([]);

        public Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BranchSummary>>([]);

        public Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TagSummary>>([]);

        public Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CommitSummary>>([]);

        public Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContributorSummary>>([]);

        public Task<IReadOnlyList<WorkflowRunSummary>> RecentWorkflowRunsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkflowRunSummary>>([]);

        public Task<IReadOnlyList<DiscussionSummary>> RecentDiscussionsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DiscussionSummary>>([]);

        public Task<IReadOnlyList<ContentItemSummary>> RepositoryContentsAsync(string owner, string name, string? path = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentItemSummary>>([]);

        public Task<byte[]> RepositoryFileContentsAsync(string owner, string name, string path, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());

        public Task<TrafficSummary?> TrafficAsync(string owner, string name, CancellationToken cancellationToken = default) => Task.FromResult<TrafficSummary?>(null);

        public Task<IReadOnlyList<HeatmapCell>> RepositoryHeatmapAsync(string owner, string name, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HeatmapCell>>([]);

        public Task<IReadOnlyList<HeatmapCell>> UserContributionHeatmapAsync(string login, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HeatmapCell>>([]);

        public Task<RateLimitResourcesSnapshot> RefreshRateLimitResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new RateLimitResourcesSnapshot([]));
    }

    private sealed class FakeOAuthLoginService(ICredentialStore credentialStore) : IOAuthLoginService
    {
        public async Task<OAuthLoginResult> LoginAsync(OAuthLoginRequest request, CancellationToken cancellationToken = default)
        {
            Uri host = GitHubOAuthLoginService.NormalizeHost(request.Host);
            OAuthTokens tokens = new("gho_desktop", "ghr_desktop", new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero));
            await credentialStore.SaveAsync(
                new CredentialRecord(GitHubOAuthLoginService.TokenService, GitHubOAuthLoginService.TokenAccount(host), tokens.ToSecret()),
                cancellationToken);
            await credentialStore.SaveAsync(
                new CredentialRecord(GitHubOAuthLoginService.ClientService, GitHubOAuthLoginService.TokenAccount(host), new OAuthClientCredentials(request.ClientId, request.ClientSecret).ToSecret()),
                cancellationToken);
            return new OAuthLoginResult(SourceControlProvider.GitHub, host, tokens.ExpiresAt, credentialStore.Kind);
        }

        public Task<OAuthTokens?> RefreshIfNeededAsync(Uri host, bool force = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<OAuthTokens?>(new OAuthTokens("gho_desktop", "ghr_desktop", new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero)));
    }
}
