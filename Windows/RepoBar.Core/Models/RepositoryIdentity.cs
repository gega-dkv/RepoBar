namespace RepoBar.Core.Models;

public sealed record RepositoryIdentity
{
    public RepositoryIdentity(
        SourceControlProvider provider,
        string id,
        string name,
        string namespacePath,
        string? pathWithNamespace = null,
        string? slug = null,
        Uri? webUrl = null,
        Uri? apiUrl = null,
        string? providerSpecificId = null)
    {
        Provider = provider;
        Id = id;
        Name = name;
        NamespacePath = namespacePath;
        PathWithNamespace = pathWithNamespace ?? MakePath(namespacePath, name);
        Slug = slug ?? name;
        WebUrl = webUrl;
        ApiUrl = apiUrl;
        ProviderSpecificId = providerSpecificId;
    }

    public SourceControlProvider Provider { get; }

    public string Id { get; }

    public string Name { get; }

    public string NamespacePath { get; }

    public string PathWithNamespace { get; }

    public string Slug { get; }

    public Uri? WebUrl { get; }

    public Uri? ApiUrl { get; }

    public string? ProviderSpecificId { get; }

    public static RepositoryIdentity GitHub(
        string id,
        string owner,
        string name,
        Uri? webUrl = null,
        Uri? apiUrl = null,
        string? providerSpecificId = null) =>
        new(
            SourceControlProvider.GitHub,
            id,
            name,
            owner,
            webUrl: webUrl,
            apiUrl: apiUrl,
            providerSpecificId: providerSpecificId);

    private static string MakePath(string namespacePath, string name)
    {
        string trimmed = namespacePath.Trim('/');
        return string.IsNullOrEmpty(trimmed) ? name : $"{trimmed}/{name}";
    }
}
