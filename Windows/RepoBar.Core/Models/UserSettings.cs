using RepoBar.Core.Support;

namespace RepoBar.Core.Models;

public sealed record UserSettings
{
    public AppearanceSettings Appearance { get; init; } = new();

    public HeatmapSettings Heatmap { get; init; } = new();

    public RepoListSettings RepoList { get; init; } = new();

    public LocalProjectsSettings LocalProjects { get; init; } = new();

    public GitHubReferenceMonitorSettings GitHubReferenceMonitor { get; init; } = new();

    public GitHubArchiveSettings GitHubArchives { get; init; } = new();

    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    public bool LaunchAtLogin { get; init; }

    public bool DiagnosticsEnabled { get; init; }

    public LogVerbosity LoggingVerbosity { get; init; } = LogVerbosity.Info;

    public bool FileLoggingEnabled { get; init; }

    public SourceControlProvider SelectedProvider { get; init; } = SourceControlProvider.GitHub;

    public IReadOnlyList<RepositoryHost> RepositoryHosts { get; init; } = [RepositoryHost.GitHubCom];

    public IReadOnlyList<RepositoryAccount> RepositoryAccounts { get; init; } = [];

    public Uri GitHubHost { get; init; } = new("https://github.com");

    public Uri? EnterpriseHost { get; init; }

    public int LoopbackPort { get; init; } = 53682;

    public AuthMethod AuthMethod { get; init; } = AuthMethod.OAuth;
}

public sealed record AppearanceSettings
{
    public bool ShowContributionHeader { get; init; } = true;

    public bool ShowRateLimitMeterInMenuBar { get; init; } = true;

    public CardDensity CardDensity { get; init; } = CardDensity.Comfortable;

    public AccentTone AccentTone { get; init; } = AccentTone.GitHubGreen;

    public GlobalActivityScope ActivityScope { get; init; } = GlobalActivityScope.MyActivity;
}

public sealed record HeatmapSettings
{
    public HeatmapDisplay Display { get; init; } = HeatmapDisplay.Inline;

    public HeatmapSpan Span { get; init; } = HeatmapSpan.TwelveMonths;
}

public sealed record RepoListSettings
{
    public int DisplayLimit { get; init; } = 6;

    public bool ShowForks { get; init; }

    public bool ShowArchived { get; init; }

    public RepositorySortKey MenuSortKey { get; init; } = RepositorySortKey.Activity;

    public IReadOnlyList<string> PinnedRepositories { get; init; } = [];

    public IReadOnlyList<string> HiddenRepositories { get; init; } = [];

    public IReadOnlyList<string> OwnerFilter { get; init; } = [];
}

public sealed record LocalProjectsSettings
{
    public string? RootPath { get; init; }

    public bool AutoSyncEnabled { get; init; } = true;

    public bool ShowDirtyFilesInMenu { get; init; }

    public TimeSpan FetchInterval { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxDepth { get; init; } = 4;

    public string WorktreeFolderName { get; init; } = ".work";

    public string? PreferredTerminal { get; init; }

    public string? PreferredLocalPathFor(string fullName) =>
        PreferredLocalPathsByFullName.TryGetValue(fullName, out string? path) ? path : null;

    public IReadOnlyDictionary<string, string> PreferredLocalPathsByFullName { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record GitHubReferenceMonitorSettings
{
    public bool Enabled { get; init; }
}

public sealed record GitHubArchiveSettings
{
    public IReadOnlyList<GitHubArchiveSource> Sources { get; init; } = [];

    public bool PreferArchiveWhenRateLimited { get; init; } = true;

    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromMinutes(15);
}

public sealed record GitHubArchiveSource(
    string Name,
    string ImportedDatabasePath,
    string Id,
    bool Enabled = true,
    string? LocalRepositoryPath = null,
    string? RemoteUrl = null,
    string Branch = "main",
    GitHubArchiveFormat Format = GitHubArchiveFormat.DiscrawlSnapshot);

public enum GitHubArchiveFormat
{
    DiscrawlSnapshot,
}

public enum LogVerbosity
{
    Debug,
    Info,
    Warning,
    Error,
}

public enum HeatmapDisplay
{
    Inline,
    Submenu,
}

public enum HeatmapSpan
{
    OneMonth,
    ThreeMonths,
    SixMonths,
    TwelveMonths,
}

public enum CardDensity
{
    Comfortable,
    Compact,
}

public enum AccentTone
{
    System,
    GitHubGreen,
}

public enum GlobalActivityScope
{
    AllActivity,
    MyActivity,
}
