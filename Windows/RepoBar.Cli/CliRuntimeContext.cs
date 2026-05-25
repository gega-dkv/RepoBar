using RepoBar.Core.Api;
using RepoBar.Core.Auth;
using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using System.Diagnostics;

namespace RepoBar.Cli;

public interface ICliRuntimeContext
{
    RepoBarPaths Paths { get; }

    UserSettings Settings { get; }

    IRepositoryService RepositoryService { get; }

    ICredentialStore CredentialStore { get; }

    PersistentCacheStore Cache { get; }

    GitHubArchiveStore Archives { get; }

    LocalProjectsService LocalProjects { get; }

    IOAuthLoginService OAuthLogin { get; }

    ICliShellService Shell { get; }

    Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default);
}

public sealed class CliRuntimeContext(
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
    : ICliRuntimeContext
{
    public RepoBarPaths Paths { get; } = paths;

    public UserSettings Settings { get; private set; } = settings;

    public IRepositoryService RepositoryService { get; } = repositoryService;

    public ICredentialStore CredentialStore { get; } = credentialStore;

    public PersistentCacheStore Cache { get; } = cache;

    public GitHubArchiveStore Archives { get; } = archives;

    public LocalProjectsService LocalProjects { get; } = localProjects;

    public IOAuthLoginService OAuthLogin { get; } = oAuthLogin;

    public ICliShellService Shell { get; } = shell;

    public static CliRuntimeContext CreateDefault()
    {
        ProcessEnvironmentReader environment = new();
        RepoBarPaths paths = RepoBarPaths.FromEnvironment(environment);
        paths.EnsureCreated();
        SettingsStore settingsStore = new(paths);
        UserSettings settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ICredentialStore credentialStore = CredentialStoreFactory.Create(
            CredentialStoreMode.Auto,
            paths,
            environment,
            isReleaseBuild: !IsDebugBuild(),
            isWindows: OperatingSystem.IsWindows());
        IRepositoryService repositoryService = RepoBar.Core.Api.RepositoryServiceFactory.Create(settings, credentialStore);
        GitExecutableLocator gitLocator = new(new PhysicalFileSystem(), new ProcessRunner());
        string gitPath = gitLocator.Locate(settings.LocalProjects.PreferredTerminal, Environment.GetEnvironmentVariable("PATH")) ?? "git.exe";
        HttpClient authHttpClient = new();
        return new CliRuntimeContext(
            paths,
            settingsStore,
            settings,
            repositoryService,
            credentialStore,
            new PersistentCacheStore(paths.CacheDatabasePath),
            new GitHubArchiveStore(),
            new LocalProjectsService(new PhysicalFileSystem(), new ProcessRunner(), gitPath),
            new GitHubOAuthLoginService(
                authHttpClient,
                credentialStore,
                new HttpListenerOAuthCallbackServer(),
                new SystemBrowserLauncher()),
            new CliShellService());
    }

    public async Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        Settings = settings;
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}

public interface ICliShellService
{
    void OpenFolder(string path);

    void OpenTerminal(string path, string? preferredTerminal);
}

public sealed class CliShellService : ICliShellService
{
    public void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
        })?.Dispose();
    }

    public void OpenTerminal(string path, string? preferredTerminal)
    {
        string executable = string.IsNullOrWhiteSpace(preferredTerminal)
            ? OperatingSystem.IsWindows() ? "wt.exe" : Environment.GetEnvironmentVariable("SHELL") ?? "sh"
            : preferredTerminal;
        Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = path,
            UseShellExecute = true,
        })?.Dispose();
    }
}
