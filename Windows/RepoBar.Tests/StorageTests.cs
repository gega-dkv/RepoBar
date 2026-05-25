using RepoBar.Core.Models;
using RepoBar.Core.Storage;
using Xunit;

namespace RepoBar.Tests;

public sealed class StorageTests
{
    [Fact]
    public void PathsUseRoamingAppDataForSettingsAndLocalAppDataForCache()
    {
        TestEnvironmentReader environment = new(
            appData: @"C:\Users\person\AppData\Roaming",
            localAppData: @"C:\Users\person\AppData\Local");

        RepoBarPaths paths = RepoBarPaths.FromEnvironment(environment);

        Assert.Equal(@"C:\Users\person\AppData\Roaming/RepoBar".Replace('/', Path.DirectorySeparatorChar), paths.SettingsDirectory);
        Assert.Equal(@"C:\Users\person\AppData\Local/RepoBar".Replace('/', Path.DirectorySeparatorChar), paths.CacheDirectory);
        Assert.EndsWith(Path.Combine("RepoBar", "settings.json"), paths.SettingsFilePath, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("RepoBar", "Cache.sqlite"), paths.CacheDatabasePath, StringComparison.Ordinal);
    }

    [Fact]
    public void CredentialFactoryUsesFileStoreForDebugAndWindowsCredentialManagerForRelease()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        TestEnvironmentReader environment = new();

        ICredentialStore debugStore = CredentialStoreFactory.Create(
            CredentialStoreMode.Auto,
            paths,
            environment,
            isReleaseBuild: false,
            isWindows: true);
        ICredentialStore releaseStore = CredentialStoreFactory.Create(
            CredentialStoreMode.Auto,
            paths,
            environment,
            isReleaseBuild: true,
            isWindows: true);

        Assert.Equal(CredentialStoreKind.File, debugStore.Kind);
        Assert.Equal(CredentialStoreKind.WindowsCredentialManager, releaseStore.Kind);
    }

    [Fact]
    public void CredentialFactoryHonorsExplicitFileOverride()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        TestEnvironmentReader environment = new(variables: new Dictionary<string, string>
        {
            ["REPOBAR_TOKEN_STORE"] = "file",
        });

        ICredentialStore store = CredentialStoreFactory.Create(
            CredentialStoreMode.Auto,
            paths,
            environment,
            isReleaseBuild: true,
            isWindows: true);

        Assert.Equal(CredentialStoreKind.File, store.Kind);
    }

    [Fact]
    public async Task FileCredentialStorePersistsReadsAndDeletesDebugSecrets()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        FileCredentialStore store = new(paths.DebugAuthDirectory);
        CredentialRecord credential = new("github", "default", "token-secret");

        await store.SaveAsync(credential);
        CredentialRecord? saved = await store.ReadAsync("github", "default");
        await store.DeleteAsync("github", "default");

        Assert.Equal(credential, saved);
        Assert.Null(await store.ReadAsync("github", "default"));
    }

    [Fact]
    public async Task SettingsStorePersistsUserSettings()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        SettingsStore store = new(paths);
        UserSettings settings = new()
        {
            SelectedProvider = SourceControlProvider.GitLab,
            DiagnosticsEnabled = true,
            RepoList = new RepoListSettings
            {
                DisplayLimit = 12,
                PinnedRepositories = ["openai/codex"],
            },
        };

        await store.SaveAsync(settings);
        UserSettings loaded = await store.LoadAsync();

        Assert.Equal(SourceControlProvider.GitLab, loaded.SelectedProvider);
        Assert.True(loaded.DiagnosticsEnabled);
        Assert.Equal(12, loaded.RepoList.DisplayLimit);
        Assert.Equal("openai/codex", loaded.RepoList.PinnedRepositories.Single());
    }

    [Fact]
    public async Task PersistentCacheStoresRestGraphQlAndRateLimits()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        PersistentCacheStore cache = new(paths.CacheDatabasePath);
        DateTimeOffset fetchedAt = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        RateLimitSnapshot rateLimit = new("core", 5000, 4999, fetchedAt.AddHours(1), null, fetchedAt);
        CachedApiResponse apiResponse = new(
            "GET /user/repos",
            "https://api.github.com/user/repos",
            "\"etag\"",
            200,
            "{}",
            """[{"name":"RepoBar"}]""",
            fetchedAt,
            rateLimit);
        CachedGraphQlResponse graphQlResponse = new(
            "https://api.github.com/graphql",
            "ContributionCalendar",
            "abc123",
            """{"data":{}}""",
            fetchedAt);

        await cache.SaveApiResponseAsync(apiResponse);
        await cache.SaveGraphQlResponseAsync(graphQlResponse);

        CachedApiResponse? savedApi = await cache.ReadApiResponseAsync("GET /user/repos");
        CachedGraphQlResponse? savedGraphQl = await cache.ReadGraphQlResponseAsync("https://api.github.com/graphql", "ContributionCalendar", "abc123");
        RateLimitSnapshot? savedRateLimit = await cache.ReadRateLimitAsync("core");
        CacheDiagnostics diagnostics = await cache.GetDiagnosticsAsync();

        Assert.Equal(apiResponse.Body, savedApi?.Body);
        Assert.Equal("\"etag\"", savedApi?.ETag);
        Assert.Equal(graphQlResponse.Body, savedGraphQl?.Body);
        Assert.Equal(4999, savedRateLimit?.Remaining);
        Assert.Equal(1, diagnostics.ApiResponseCount);
        Assert.Equal(1, diagnostics.GraphQlResponseCount);
        Assert.Equal(1, diagnostics.RateLimitCount);
    }

    [Fact]
    public async Task PersistentCacheReturnsStaleStartupBodyBeforeRefresh()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        PersistentCacheStore cache = new(paths.CacheDatabasePath);
        DateTimeOffset fetchedAt = new(2026, 5, 25, 10, 0, 0, TimeSpan.Zero);
        await cache.SaveApiResponseAsync(new CachedApiResponse(
            "GET /user/repos",
            "https://api.github.com/user/repos",
            null,
            200,
            "{}",
            """[{"name":"cached"}]""",
            fetchedAt));

        CacheFirstResult<string>? result = await cache.ReadApiBodyForStartupAsync(
            "GET /user/repos",
            TimeSpan.FromMinutes(15),
            fetchedAt.AddHours(2));

        Assert.NotNull(result);
        Assert.True(result.IsStale);
        Assert.Equal("""[{"name":"cached"}]""", result.Value);
    }

    [Fact]
    public async Task PersistentCacheClearRemovesResponsesAndRateLimits()
    {
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(CreateTempRoot());
        PersistentCacheStore cache = new(paths.CacheDatabasePath);
        DateTimeOffset fetchedAt = new(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        await cache.SaveApiResponseAsync(new CachedApiResponse(
            "GET /rate_limit",
            "https://api.github.com/rate_limit",
            null,
            200,
            "{}",
            "{}",
            fetchedAt,
            new RateLimitSnapshot("core", 5000, 0, fetchedAt.AddHours(1), "blocked", fetchedAt)));

        await cache.ClearAsync();
        CacheDiagnostics diagnostics = await cache.GetDiagnosticsAsync();

        Assert.Equal(0, diagnostics.ApiResponseCount);
        Assert.Equal(0, diagnostics.RateLimitCount);
    }

    [Fact]
    public async Task ArchiveValidationAndImportReadDiscrawlSnapshotShape()
    {
        string root = CreateTempRoot();
        string snapshot = Path.Combine(root, "snapshot");
        string tableDirectory = Path.Combine(snapshot, "tables", "threads");
        Directory.CreateDirectory(tableDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(snapshot, "manifest.json"),
            """
            {
              "tables": [
                {
                  "name": "threads",
                  "files": ["000001.jsonl"],
                  "columns": ["repository", "title"],
                  "rows": 2
                }
              ]
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(tableDirectory, "000001.jsonl"),
            """
            {"repository":"openai/codex","title":"First"}
            {"repository":"openai/codex","title":"Second"}
            """);
        GitHubArchiveSource source = new(
            Name: "Codex",
            ImportedDatabasePath: Path.Combine(root, "Archives", "codex.sqlite"),
            Id: "codex",
            LocalRepositoryPath: snapshot);
        GitHubArchiveStore store = new();

        ArchiveValidationResult validation = await GitHubArchiveStore.ValidateAsync(source);
        ArchiveImportResult import = await store.ImportAsync(source);
        ArchiveStatus status = await GitHubArchiveStore.GetStatusAsync(source);

        Assert.True(validation.IsValid);
        Assert.Single(validation.Tables);
        Assert.Equal(2, import.RowCount);
        Assert.True(status.HasImportedDatabase);
        Assert.Equal(2, status.ImportedRows);
    }

    [Fact]
    public async Task ArchiveValidationReportsMissingFiles()
    {
        string root = CreateTempRoot();
        string snapshot = Path.Combine(root, "snapshot");
        Directory.CreateDirectory(snapshot);
        await File.WriteAllTextAsync(
            Path.Combine(snapshot, "manifest.json"),
            """
            {
              "tables": [
                {
                  "name": "threads",
                  "files": ["missing.jsonl"],
                  "columns": ["repository", "title"],
                  "rows": 1
                }
              ]
            }
            """);
        GitHubArchiveSource source = new(
            Name: "Codex",
            ImportedDatabasePath: Path.Combine(root, "Archives", "codex.sqlite"),
            Id: "codex",
            LocalRepositoryPath: snapshot);

        ArchiveValidationResult validation = await GitHubArchiveStore.ValidateAsync(source);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("missing.jsonl", StringComparison.Ordinal));
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
