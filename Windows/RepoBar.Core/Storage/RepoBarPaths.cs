namespace RepoBar.Core.Storage;

public sealed record RepoBarPaths(string SettingsDirectory, string CacheDirectory)
{
    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public string DebugAuthDirectory => Path.Combine(SettingsDirectory, "DebugAuth");

    public string CacheDatabasePath => Path.Combine(CacheDirectory, "Cache.sqlite");

    public string ArchiveDirectory => Path.Combine(CacheDirectory, "Archives");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DebugAuthDirectory);
        Directory.CreateDirectory(ArchiveDirectory);
    }

    public static RepoBarPaths FromEnvironment(IEnvironmentReader environment)
    {
        string roaming = environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new RepoBarPaths(
            Path.Combine(RequireFolder(roaming, "ApplicationData"), "RepoBar"),
            Path.Combine(RequireFolder(local, "LocalApplicationData"), "RepoBar"));
    }

    public static RepoBarPaths ForTestRoot(string root) =>
        new(Path.Combine(root, "Roaming", "RepoBar"), Path.Combine(root, "Local", "RepoBar"));

    private static string RequireFolder(string folder, string name) =>
        string.IsNullOrWhiteSpace(folder)
            ? throw new InvalidOperationException($"Environment folder {name} is not available.")
            : folder;
}

public interface IEnvironmentReader
{
    string? GetVariable(string name);

    string GetFolderPath(Environment.SpecialFolder folder);
}

public sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
}

public sealed class TestEnvironmentReader(
    string? appData = null,
    string? localAppData = null,
    IReadOnlyDictionary<string, string>? variables = null)
    : IEnvironmentReader
{
    public string? GetVariable(string name) =>
        variables is not null && variables.TryGetValue(name, out string? value) ? value : null;

    public string GetFolderPath(Environment.SpecialFolder folder) =>
        folder switch
        {
            Environment.SpecialFolder.ApplicationData => appData ?? string.Empty,
            Environment.SpecialFolder.LocalApplicationData => localAppData ?? string.Empty,
            _ => string.Empty,
        };
}
