using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using RepoBar.Core.Api;
using RepoBar.Core.Auth;
using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using RepoBar.Core.Support;
using RepoBar.Desktop.Platform;

namespace RepoBar.Desktop.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly IRepoBarWindowService windowService;
    private readonly IWindowsDashboardDataSource dataSource;
    private readonly SettingsStore settingsStore;
    private readonly ICredentialStore credentialStore;
    private readonly IOAuthLoginService oAuthLoginService;
    private readonly TimeProvider timeProvider;
    private UserSettings settings;
    private readonly List<RepositoryCardViewModel> allRepositoryCards = [];
    private RepositoryCardViewModel? selectedRepository;
    private string patToken = string.Empty;
    private string oAuthClientId = string.Empty;
    private string oAuthClientSecret = string.Empty;
    private string repositorySearchQuery = string.Empty;
    private bool isBusy;

    public ShellViewModel(
        IRepoBarWindowService windowService,
        IWindowsDashboardDataSource dataSource,
        SettingsStore settingsStore,
        ICredentialStore credentialStore,
        UserSettings? initialSettings = null,
        IOAuthLoginService? oAuthLoginService = null,
        TimeProvider? timeProvider = null)
    {
        this.windowService = windowService;
        this.dataSource = dataSource;
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        this.oAuthLoginService = oAuthLoginService ?? new GitHubOAuthLoginService(
            new HttpClient(),
            credentialStore,
            new HttpListenerOAuthCallbackServer(),
            new SystemBrowserLauncher());
        this.timeProvider = timeProvider ?? TimeProvider.System;
        settings = initialSettings ?? new UserSettings();

        ShowDashboardCommand = new RelayCommand(windowService.ShowDashboard);
        ShowSettingsCommand = new RelayCommand(() =>
        {
            SelectedPrimaryTabIndex = 1;
            windowService.ShowDashboard();
        });
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync(force: true));
        SaveSettingsCommand = new RelayCommand(() => _ = SaveSettingsAsync());
        SavePatCommand = new RelayCommand(() => _ = SavePatAsync(), () => !string.IsNullOrWhiteSpace(PatToken));
        StartOAuthCommand = new RelayCommand(() => _ = StartOAuthAsync(), () => !string.IsNullOrWhiteSpace(OAuthClientId) && !string.IsNullOrWhiteSpace(OAuthClientSecret));
        LogoutCommand = new RelayCommand(() => _ = LogoutAsync());
        OpenSelectedRepositoryCommand = new RelayCommand(() => OpenSelectedRepository(), () => SelectedRepository?.WebUrl is not null);
        PinSelectedRepositoryCommand = new RelayCommand(() => SetSelectedVisibility(RepositoryVisibility.Pinned), () => SelectedRepository is not null);
        HideSelectedRepositoryCommand = new RelayCommand(() => SetSelectedVisibility(RepositoryVisibility.Hidden), () => SelectedRepository is not null);
        QuitCommand = new RelayCommand(windowService.Quit);

        SettingsTabs =
        [
            new SettingsTabViewModel("General", "Refresh, display limit, launch at login, and startup behavior."),
            new SettingsTabViewModel("Accounts", "GitHub.com, GitHub Enterprise, GitLab.com, PAT, and OAuth configuration."),
            new SettingsTabViewModel("Repositories", "Visible, pinned, hidden, owner filters, and repository browser state."),
            new SettingsTabViewModel("Display", "Density, contribution header, heatmap, and rate-limit meter preferences."),
            new SettingsTabViewModel("Local Projects", "Project folder, scan depth, auto-sync, dirty files, and terminal preferences."),
            new SettingsTabViewModel("Cache", "Persistent REST/GraphQL cache, archives, stale reads, and clear/import controls."),
            new SettingsTabViewModel("Diagnostics", "Verbose logging, API status, redacted diagnostics, and local troubleshooting."),
            new SettingsTabViewModel("Startup", "Launch at login, updates, package route checks, and manual verification state."),
        ];

        ProviderOptions =
        [
            ProviderOptionViewModel.For(SourceControlProvider.GitHub),
            ProviderOptionViewModel.For(SourceControlProvider.GitLab),
        ];

        ApplySettings(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepositoryCardViewModel> Repositories { get; } = [];

    public ObservableCollection<RepositoryBrowserRowViewModel> RepositoryBrowserRows { get; } = [];

    public ObservableCollection<DetailSectionViewModel> DetailSections { get; } = [];

    public ObservableCollection<SettingsTabViewModel> SettingsTabs { get; }

    public ObservableCollection<ProviderOptionViewModel> ProviderOptions { get; }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand SavePatCommand { get; }

    public ICommand StartOAuthCommand { get; }

    public ICommand LogoutCommand { get; }

    public ICommand OpenSelectedRepositoryCommand { get; }

    public ICommand PinSelectedRepositoryCommand { get; }

    public ICommand HideSelectedRepositoryCommand { get; }

    public ICommand QuitCommand { get; }

    public string WindowTitle => $"RepoBar - {settings.SelectedProvider.Label()}";

    public string AccountSummary { get; private set; } = "Not signed in";

    public string CacheStatus { get; private set; } = "Waiting for cache";

    public string RateLimitSummary { get; private set; } = "API status unavailable";

    public string SelectedProviderLabel => settings.SelectedProvider.Label();

    public bool IsLoggedIn { get; private set; }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetField(ref isBusy, value);
    }

    public int SelectedPrimaryTabIndex { get; set; }

    public RepositoryCardViewModel? SelectedRepository
    {
        get => selectedRepository;
        set
        {
            if (SetField(ref selectedRepository, value))
            {
                RebuildDetailSections();
                RaiseCommandStates();
            }
        }
    }

    public string PatToken
    {
        get => patToken;
        set
        {
            if (SetField(ref patToken, value))
            {
                ((RelayCommand)SavePatCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OAuthClientId
    {
        get => oAuthClientId;
        set
        {
            if (SetField(ref oAuthClientId, value))
            {
                ((RelayCommand)StartOAuthCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OAuthClientSecret
    {
        get => oAuthClientSecret;
        set
        {
            if (SetField(ref oAuthClientSecret, value))
            {
                ((RelayCommand)StartOAuthCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OAuthScope { get; set; } = string.Empty;

    public string RepositorySearchQuery
    {
        get => repositorySearchQuery;
        set
        {
            if (SetField(ref repositorySearchQuery, value))
            {
                RebuildRepositoryBrowserRows();
            }
        }
    }

    public int DisplayLimit { get; set; }

    public int RefreshIntervalMinutes { get; set; }

    public bool LaunchAtLogin { get; set; }

    public bool ShowContributionHeader { get; set; }

    public bool ShowRateLimitMeter { get; set; }

    public bool DiagnosticsEnabled { get; set; }

    public bool AutoSyncEnabled { get; set; }

    public string LocalProjectsRoot { get; set; } = string.Empty;

    public int LocalProjectsDepth { get; set; }

    public string GitHubEnterpriseHost { get; set; } = string.Empty;

    public string LoopbackPort { get; set; } = "53682";

    public static async Task<ShellViewModel> CreateDefaultAsync(IRepoBarWindowService windowService, CancellationToken cancellationToken = default)
    {
        RepoBarPaths paths = RepoBarPaths.FromEnvironment(new ProcessEnvironmentReader());
        paths.EnsureCreated();
        SettingsStore settingsStore = new(paths);
        UserSettings settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        ICredentialStore credentialStore = CredentialStoreFactory.Create(
            CredentialStoreMode.Auto,
            paths,
            new ProcessEnvironmentReader(),
            isReleaseBuild: !IsDebugBuild(),
            isWindows: OperatingSystem.IsWindows());
        GitExecutableLocator gitLocator = new(new PhysicalFileSystem(), new ProcessRunner());
        string gitPath = gitLocator.Locate(null, Environment.GetEnvironmentVariable("PATH")) ?? "git.exe";
        LocalProjectsService localProjects = new(new PhysicalFileSystem(), new ProcessRunner(), gitPath);
        PersistentCacheStore cache = new(paths.CacheDatabasePath);
        IRepositoryService repositoryService = RepositoryServiceFactory.Create(settings, credentialStore);
        ShellViewModel viewModel = new(
            windowService,
            new ProviderWindowsDashboardDataSource(repositoryService, credentialStore, cache, localProjects),
            settingsStore,
            credentialStore,
            settings,
            new GitHubOAuthLoginService(
                new HttpClient(),
                credentialStore,
                new HttpListenerOAuthCallbackServer(),
                new SystemBrowserLauncher()));
        await viewModel.LoadCachedStartupAsync(cancellationToken).ConfigureAwait(false);
        return viewModel;
    }

    public async Task LoadCachedStartupAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            WindowsDashboardSnapshot snapshot = await dataSource.LoadCachedAsync(settings, cancellationToken).ConfigureAwait(false);
            ApplySnapshot(snapshot);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync(bool force, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            WindowsDashboardSnapshot snapshot = await dataSource.RefreshAsync(settings, force, cancellationToken).ConfigureAwait(false);
            ApplySnapshot(snapshot);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        settings = settings with
        {
            RefreshInterval = TimeSpan.FromMinutes(Math.Max(1, RefreshIntervalMinutes)),
            LaunchAtLogin = LaunchAtLogin,
            DiagnosticsEnabled = DiagnosticsEnabled,
            RepoList = settings.RepoList with
            {
                DisplayLimit = Math.Max(1, DisplayLimit),
            },
            Appearance = settings.Appearance with
            {
                ShowContributionHeader = ShowContributionHeader,
                ShowRateLimitMeterInMenuBar = ShowRateLimitMeter,
            },
            LocalProjects = settings.LocalProjects with
            {
                RootPath = string.IsNullOrWhiteSpace(LocalProjectsRoot) ? null : LocalProjectsRoot,
                MaxDepth = Math.Max(0, LocalProjectsDepth),
                AutoSyncEnabled = AutoSyncEnabled,
            },
            EnterpriseHost = Uri.TryCreate(GitHubEnterpriseHost, UriKind.Absolute, out Uri? enterpriseHost) ? enterpriseHost : null,
            LoopbackPort = int.TryParse(LoopbackPort, out int port) ? port : 53682,
        };
        await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        ApplySettings(settings);
    }

    public async Task SavePatAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(PatToken))
        {
            return;
        }

        await credentialStore.SaveAsync(
            new CredentialRecord("provider-token", $"{settings.SelectedProvider}:{SelectedAuthHost().Host}:pat", PatToken),
            cancellationToken).ConfigureAwait(false);
        PatToken = string.Empty;
        IsLoggedIn = true;
        AccountSummary = $"{settings.SelectedProvider.Label()} PAT stored in {credentialStore.Kind}";
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountSummary));
    }

    public async Task StartOAuthAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(OAuthClientId) || string.IsNullOrWhiteSpace(OAuthClientSecret))
        {
            return;
        }

        Uri host = Uri.TryCreate(GitHubEnterpriseHost, UriKind.Absolute, out Uri? enterpriseHost)
            ? enterpriseHost
            : settings.GitHubHost;
        int loopbackPort = int.TryParse(LoopbackPort, out int parsedPort) ? parsedPort : settings.LoopbackPort;
        OAuthLoginResult result = await oAuthLoginService.LoginAsync(
            new OAuthLoginRequest(
                host,
                OAuthClientId,
                OAuthClientSecret,
                loopbackPort,
                string.IsNullOrWhiteSpace(OAuthScope) ? null : OAuthScope),
            cancellationToken).ConfigureAwait(false);

        settings = settings with
        {
            SelectedProvider = SourceControlProvider.GitHub,
            AuthMethod = AuthMethod.OAuth,
            LoopbackPort = loopbackPort,
            EnterpriseHost = result.Host.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? null : result.Host,
        };
        await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        OAuthClientSecret = string.Empty;
        IsLoggedIn = true;
        AccountSummary = $"{result.Provider.Label()} OAuth stored in {result.Store}";
        ApplySettings(settings);
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountSummary));
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        Uri host = SelectedAuthHost();
        await credentialStore.DeleteAsync("provider-token", $"{settings.SelectedProvider}:{host.Host}:pat", cancellationToken).ConfigureAwait(false);
        await credentialStore.DeleteAsync(GitHubOAuthLoginService.TokenService, GitHubOAuthLoginService.TokenAccount(host), cancellationToken).ConfigureAwait(false);
        await credentialStore.DeleteAsync(GitHubOAuthLoginService.ClientService, GitHubOAuthLoginService.TokenAccount(host), cancellationToken).ConfigureAwait(false);
        IsLoggedIn = false;
        AccountSummary = "Not signed in";
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountSummary));
    }

    private void ApplySnapshot(WindowsDashboardSnapshot snapshot)
    {
        allRepositoryCards.Clear();
        allRepositoryCards.AddRange(snapshot.Repositories);
        Repositories.Clear();
        foreach (RepositoryCardViewModel repository in allRepositoryCards.Take(settings.RepoList.DisplayLimit))
        {
            Repositories.Add(repository);
        }

        SelectedRepository = Repositories.FirstOrDefault();
        CacheStatus = snapshot.CacheStatus;
        RateLimitSummary = snapshot.RateLimitSummary;
        AccountSummary = snapshot.AccountSummary;
        IsLoggedIn = snapshot.IsLoggedIn || IsLoggedIn;
        RebuildRepositoryBrowserRows();
        OnPropertyChanged(nameof(CacheStatus));
        OnPropertyChanged(nameof(RateLimitSummary));
        OnPropertyChanged(nameof(AccountSummary));
        OnPropertyChanged(nameof(IsLoggedIn));
    }

    private void ApplySettings(UserSettings value)
    {
        DisplayLimit = value.RepoList.DisplayLimit;
        RefreshIntervalMinutes = Math.Max(1, (int)value.RefreshInterval.TotalMinutes);
        LaunchAtLogin = value.LaunchAtLogin;
        ShowContributionHeader = value.Appearance.ShowContributionHeader;
        ShowRateLimitMeter = value.Appearance.ShowRateLimitMeterInMenuBar;
        DiagnosticsEnabled = value.DiagnosticsEnabled;
        AutoSyncEnabled = value.LocalProjects.AutoSyncEnabled;
        LocalProjectsRoot = value.LocalProjects.RootPath ?? string.Empty;
        LocalProjectsDepth = value.LocalProjects.MaxDepth;
        GitHubEnterpriseHost = value.EnterpriseHost?.AbsoluteUri ?? string.Empty;
        LoopbackPort = value.LoopbackPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        OnPropertyChanged(nameof(DisplayLimit));
        OnPropertyChanged(nameof(RefreshIntervalMinutes));
        OnPropertyChanged(nameof(LaunchAtLogin));
        OnPropertyChanged(nameof(ShowContributionHeader));
        OnPropertyChanged(nameof(ShowRateLimitMeter));
        OnPropertyChanged(nameof(DiagnosticsEnabled));
        OnPropertyChanged(nameof(AutoSyncEnabled));
        OnPropertyChanged(nameof(LocalProjectsRoot));
        OnPropertyChanged(nameof(LocalProjectsDepth));
        OnPropertyChanged(nameof(GitHubEnterpriseHost));
        OnPropertyChanged(nameof(LoopbackPort));
        OnPropertyChanged(nameof(SelectedProviderLabel));
    }

    private void RebuildDetailSections()
    {
        DetailSections.Clear();
        if (SelectedRepository is not { } repository)
        {
            return;
        }

        DetailSections.Add(new DetailSectionViewModel("Local State", repository.LocalStatus, true));
        DetailSections.Add(new DetailSectionViewModel("Issues", repository.IssueSummary, repository.Capabilities.Issues));
        DetailSections.Add(new DetailSectionViewModel("Pull Requests", repository.PullRequestSummary, repository.Capabilities.PullRequests));
        DetailSections.Add(new DetailSectionViewModel("CI", repository.CiSummary, repository.Capabilities.Ci));
        DetailSections.Add(new DetailSectionViewModel("Releases", repository.ReleaseSummary, repository.Capabilities.Releases));
        DetailSections.Add(new DetailSectionViewModel("Traffic", repository.TrafficSummary, repository.Capabilities.TrafficStats));
        DetailSections.Add(new DetailSectionViewModel("Discussions", repository.DiscussionsSummary, repository.Capabilities.Discussions));
        DetailSections.Add(new DetailSectionViewModel("Changelog", repository.ChangelogPreview, true));
    }

    private void OpenSelectedRepository()
    {
        if (SelectedRepository?.WebUrl is Uri url)
        {
            windowService.OpenUrl(url);
        }
    }

    private void SetSelectedVisibility(RepositoryVisibility visibility)
    {
        if (SelectedRepository is null)
        {
            return;
        }

        SelectedRepository.Visibility = visibility;
        OnPropertyChanged(nameof(SelectedRepository));
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)OpenSelectedRepositoryCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PinSelectedRepositoryCommand).RaiseCanExecuteChanged();
        ((RelayCommand)HideSelectedRepositoryCommand).RaiseCanExecuteChanged();
    }

    private Uri SelectedAuthHost()
    {
        if (settings.SelectedProvider == SourceControlProvider.GitLab)
        {
            RepositoryHost gitLabHost = settings.RepositoryHosts.FirstOrDefault(host => host.Provider == SourceControlProvider.GitLab) ?? RepositoryHost.GitLabCom;
            return gitLabHost.WebBaseUrl;
        }

        return settings.EnterpriseHost ?? settings.GitHubHost;
    }

    private void RebuildRepositoryBrowserRows()
    {
        RepositoryBrowserRows.Clear();
        IReadOnlyList<Repository> ordered = RepositoryAutocomplete.Suggestions(
            RepositorySearchQuery,
            allRepositoryCards.Select(card => card.Repository).ToArray(),
            limit: Math.Max(settings.RepoList.DisplayLimit, 20));
        Dictionary<string, RepositoryCardViewModel> cardsByFullName = allRepositoryCards.ToDictionary(card => card.FullName, StringComparer.OrdinalIgnoreCase);
        foreach (Repository repository in ordered)
        {
            RepositoryCardViewModel card = cardsByFullName[repository.FullName];
            RepositoryBrowserRows.Add(new RepositoryBrowserRowViewModel(card.FullName, card.ProviderLabel, card.Visibility, card.WebUrl));
        }
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}

public interface IRepoBarWindowService
{
    void ShowDashboard();

    void OpenUrl(Uri url);

    void Quit();
}

public sealed class RepoBarWindowService : IRepoBarWindowService
{
    private readonly Func<Avalonia.Controls.Window?> windowProvider;

    public RepoBarWindowService(Func<Avalonia.Controls.Window?> windowProvider)
    {
        this.windowProvider = windowProvider;
    }

    public void ShowDashboard()
    {
        Avalonia.Controls.Window? window = windowProvider();
        if (window is null)
        {
            return;
        }

        window.Show();
        window.Activate();
    }

    public void OpenUrl(Uri url) => new WindowsShellLauncher(new ProcessStarter()).OpenUrl(url);

    public void Quit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}

public sealed record SettingsTabViewModel(string Header, string Summary);

public sealed record ProviderOptionViewModel(string Label, SourceControlProvider Provider, string Availability)
{
    public static ProviderOptionViewModel For(SourceControlProvider provider)
    {
        ProviderCapabilities capabilities = ProviderCapabilities.For(provider);
        string unavailable = string.Join(
            ", ",
            new[]
            {
                capabilities.TrafficStats ? null : "traffic",
                capabilities.Discussions ? null : "discussions",
                capabilities.ContributionCalendar ? null : "calendar",
            }.Where(value => value is not null));
        return new ProviderOptionViewModel(
            provider.Label(),
            provider,
            string.IsNullOrWhiteSpace(unavailable) ? "Full v1 dashboard support" : $"Unavailable: {unavailable}");
    }
}

public sealed record DetailSectionViewModel(string Title, string Detail, bool IsAvailable)
{
    public string Availability => IsAvailable ? "Available" : "Unavailable";
}

public sealed record RepositoryBrowserRowViewModel(
    string FullName,
    string ProviderLabel,
    RepositoryVisibility Visibility,
    Uri? WebUrl);

public enum RepositoryVisibility
{
    Visible,
    Pinned,
    Hidden,
}

public sealed class RepositoryCardViewModel
{
    public RepositoryCardViewModel(
        Repository repository,
        ProviderCapabilities capabilities,
        LocalRepoStatus? localStatus = null,
        string? cacheSource = null,
        string? changelogPreview = null)
    {
        Repository = repository;
        Capabilities = capabilities;
        LocalRepoStatus = localStatus;
        CacheSource = cacheSource ?? "Live";
        ChangelogPreview = changelogPreview ?? $"No changelog preview cached for {repository.Name}.";
    }

    public Repository Repository { get; }

    public ProviderCapabilities Capabilities { get; }

    public LocalRepoStatus? LocalRepoStatus { get; }

    public RepositoryVisibility Visibility { get; set; } = RepositoryVisibility.Visible;

    public string CacheSource { get; }

    public string Name => Repository.Name;

    public string Owner => Repository.Owner;

    public string FullName => Repository.FullName;

    public Uri? WebUrl => Repository.Identity.WebUrl;

    public string ProviderLabel => Repository.Provider.Label();

    public string IssueSummary => Capabilities.Issues ? $"{Repository.Stats.OpenIssues} open issues" : "Issues unavailable";

    public string PullRequestSummary => Capabilities.PullRequests ? $"{Repository.Stats.OpenPulls} open pull requests" : "Pull requests unavailable";

    public string CiSummary => Capabilities.Ci ? Repository.CiStatus.ToString() : "CI unavailable";

    public string ReleaseSummary => Repository.LatestRelease is null ? "No release cached" : $"{Repository.LatestRelease.Tag} published {Repository.LatestRelease.PublishedAt:yyyy-MM-dd}";

    public string ActivitySummary => Repository.LatestActivity is null ? "No recent activity cached" : $"{Repository.LatestActivity.Title} by {Repository.LatestActivity.Actor}";

    public string TrafficSummary => Capabilities.TrafficStats && Repository.Traffic is not null
        ? $"{Repository.Traffic.UniqueVisitors} visitors, {Repository.Traffic.UniqueCloners} cloners"
        : "Traffic unavailable";

    public string HeatmapSummary => Repository.Heatmap.Count == 0 ? "No heatmap cached" : $"{Repository.Heatmap.Sum(cell => cell.Count)} events cached";

    public string LocalStatus => LocalRepoStatus?.SyncDetail ?? "No local checkout matched";

    public string DiscussionsSummary => Capabilities.Discussions ? "Discussions available when the provider returns them" : "Discussions unavailable";

    public string ChangelogPreview { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(Repository.Error);

    public string ErrorText => Repository.Error ?? string.Empty;

    public string RateLimitText => Repository.RateLimitedUntil is null ? "No rate-limit blocker" : $"Rate limited until {Repository.RateLimitedUntil:HH:mm}";
}

public interface IWindowsDashboardDataSource
{
    Task<WindowsDashboardSnapshot> LoadCachedAsync(UserSettings settings, CancellationToken cancellationToken = default);

    Task<WindowsDashboardSnapshot> RefreshAsync(UserSettings settings, bool force, CancellationToken cancellationToken = default);
}

public sealed record WindowsDashboardSnapshot(
    IReadOnlyList<RepositoryCardViewModel> Repositories,
    string CacheStatus,
    string RateLimitSummary,
    string AccountSummary,
    bool IsLoggedIn);

public sealed class ProviderWindowsDashboardDataSource(
    IRepositoryService repositoryService,
    ICredentialStore credentialStore,
    PersistentCacheStore cache,
    LocalProjectsService localProjects)
    : IWindowsDashboardDataSource
{
    public async Task<WindowsDashboardSnapshot> LoadCachedAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.CachedRepositoryListAsync(settings.RepoList.DisplayLimit, cancellationToken).ConfigureAwait(false);
        CacheDiagnostics diagnostics = await cache.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return await BuildSnapshotAsync(settings, repositories, diagnostics, "Cached startup seed", cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsDashboardSnapshot> RefreshAsync(UserSettings settings, bool force, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.RepositoryListAsync(settings.RepoList.DisplayLimit, cancellationToken).ConfigureAwait(false);
        RateLimitResourcesSnapshot? liveRateLimits = null;
        try
        {
            liveRateLimits = await repositoryService.RefreshRateLimitResourcesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UnsupportedProviderFeatureException)
        {
        }

        CacheDiagnostics diagnostics = await cache.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        string cacheStatus = force ? "Manual refresh completed after cached render" : "Background refresh completed";
        return await BuildSnapshotAsync(settings, repositories, diagnostics, cacheStatus, cancellationToken, liveRateLimits).ConfigureAwait(false);
    }

    private async Task<WindowsDashboardSnapshot> BuildSnapshotAsync(
        UserSettings settings,
        IReadOnlyList<Repository> repositories,
        CacheDiagnostics diagnostics,
        string cacheStatus,
        CancellationToken cancellationToken,
        RateLimitResourcesSnapshot? liveRateLimits = null)
    {
        LocalRepoIndex localIndex = await LocalIndexAsync(settings, cancellationToken).ConfigureAwait(false);
        List<RepositoryCardViewModel> cards = [];
        foreach (Repository repository in repositories)
        {
            LocalRepoStatus? localStatus = localIndex.StatusFor(repository);
            string? changelogPreview = localStatus is null
                ? null
                : await ChangelogPreviewService.PreviewForRepositoryAsync(localStatus.Path, repository.LatestRelease?.Tag, cancellationToken: cancellationToken).ConfigureAwait(false);
            cards.Add(new RepositoryCardViewModel(
                repository,
                ProviderCapabilities.For(repository.Provider),
                localStatus,
                cacheStatus,
                changelogPreview));
        }
        string rateLimitSummary = liveRateLimits is not null
            ? string.Join(", ", liveRateLimits.Resources.Take(3).Select(resource => $"{resource.Resource}: {resource.Remaining}/{resource.Limit}"))
            : diagnostics.RateLimits.Count == 0
                ? "API status unavailable"
                : string.Join(", ", diagnostics.RateLimits.Take(3).Select(rate => $"{rate.Resource}: {rate.Remaining}/{rate.Limit}"));
        bool isLoggedIn = await HasCredentialsAsync(settings, cancellationToken).ConfigureAwait(false);
        string accountSummary = isLoggedIn
            ? $"{repositoryService.Provider.Label()} credentials loaded from {credentialStore.Kind}"
            : $"{repositoryService.Provider.Label()} not signed in";
        return new WindowsDashboardSnapshot(cards, cacheStatus, rateLimitSummary, accountSummary, isLoggedIn);
    }

    private async Task<LocalRepoIndex> LocalIndexAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.LocalProjects.RootPath))
        {
            return LocalRepoIndex.Empty;
        }

        IReadOnlyList<string> paths = localProjects.FindGitRepositories(settings.LocalProjects.RootPath, settings.LocalProjects.MaxDepth);
        List<LocalRepoStatus> statuses = [];
        foreach (string path in paths)
        {
            statuses.Add(await localProjects.GetStatusAsync(path, cancellationToken).ConfigureAwait(false));
        }

        return new LocalRepoIndex(statuses, settings.LocalProjects.PreferredLocalPathsByFullName);
    }

    private async Task<bool> HasCredentialsAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        Uri gitHubHost = settings.EnterpriseHost ?? settings.GitHubHost;
        if (repositoryService.Provider == SourceControlProvider.GitHub)
        {
            CredentialRecord? pat = await credentialStore.ReadAsync("provider-token", $"{SourceControlProvider.GitHub}:{gitHubHost.Host}:pat", cancellationToken).ConfigureAwait(false);
            CredentialRecord? oauth = await credentialStore.ReadAsync(RepoBar.Core.Auth.GitHubOAuthLoginService.TokenService, RepoBar.Core.Auth.GitHubOAuthLoginService.TokenAccount(gitHubHost), cancellationToken).ConfigureAwait(false);
            return pat is not null || oauth is not null;
        }

        RepositoryHost gitLabHost = settings.RepositoryHosts.FirstOrDefault(host => host.Provider == SourceControlProvider.GitLab) ?? RepositoryHost.GitLabCom;
        return await credentialStore.ReadAsync("provider-token", $"{SourceControlProvider.GitLab}:{gitLabHost.WebBaseUrl.Host}:pat", cancellationToken).ConfigureAwait(false) is not null;
    }
}

public sealed class SampleWindowsDashboardDataSource : IWindowsDashboardDataSource
{
    public Task<WindowsDashboardSnapshot> LoadCachedAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildSnapshot(settings, "Cached startup seed", "core: 4,980/5,000", "Debug auth ready", isLoggedIn: false));

    public Task<WindowsDashboardSnapshot> RefreshAsync(UserSettings settings, bool force, CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildSnapshot(settings, force ? "Manual refresh queued after cached render" : "Background refresh queued", "core: 4,979/5,000", "Provider refresh pending", isLoggedIn: false));

    private static WindowsDashboardSnapshot BuildSnapshot(
        UserSettings settings,
        string cacheStatus,
        string rateLimitSummary,
        string accountSummary,
        bool isLoggedIn)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Repository github = new(
            id: "github-repobar",
            name: "RepoBar",
            owner: "openclaw",
            identity: RepositoryIdentity.GitHub(
                "github-repobar",
                "openclaw",
                "RepoBar",
                webUrl: new Uri("https://github.com/openclaw/RepoBar")),
            ciStatus: CiStatus.Passing,
            stats: new RepositoryStats(OpenIssues: 3, OpenPulls: 2, Stars: 148, Forks: 12, PushedAt: now.AddHours(-5)),
            latestRelease: new Release("Windows planning", "v0.6.0", now.AddDays(-3), new Uri("https://github.com/openclaw/RepoBar/releases")),
            latestActivity: new ActivityEvent("Phase 6 UI shell", "RepoBar", now.AddHours(-2), new Uri("https://github.com/openclaw/RepoBar")),
            traffic: new TrafficStats(42, 11),
            heatmap: Enumerable.Range(0, 24).Select(index => new HeatmapCell(DateOnly.FromDateTime(now.Date.AddDays(-index)), index % 5)).ToList());
        Repository gitLab = new(
            id: "gitlab-mirror",
            name: "platform",
            owner: "internal",
            identity: new RepositoryIdentity(
                SourceControlProvider.GitLab,
                "gitlab-mirror",
                "platform",
                "internal",
                webUrl: new Uri("https://gitlab.com/internal/platform")),
            ciStatus: CiStatus.Pending,
            stats: new RepositoryStats(OpenIssues: 7, OpenPulls: 1, Stars: 0, Forks: 0, PushedAt: now.AddHours(-9)),
            latestActivity: new ActivityEvent("Pipeline moved to pending", "gitlab", now.AddHours(-1), new Uri("https://gitlab.com/internal/platform")));

        LocalRepoStatus local = new(
            @"C:\Projects\RepoBar",
            "RepoBar",
            "openclaw/RepoBar",
            "main",
            true,
            0,
            1,
            LocalSyncState.Behind,
            null,
            [],
            null,
            "origin/main");

        List<RepositoryCardViewModel> repositories =
        [
            new(github, ProviderCapabilities.GitHub, local, cacheStatus),
            new(gitLab, ProviderCapabilities.GitLab, null, cacheStatus),
        ];

        return new WindowsDashboardSnapshot(repositories, cacheStatus, rateLimitSummary, accountSummary, isLoggedIn);
    }
}
