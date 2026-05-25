namespace RepoBar.Core.Models;

public sealed record RepositoryUrlTemplates(
    string? Repository = null,
    string? Branch = null,
    string? Commit = null,
    string? Issue = null,
    string? PullRequest = null);

public sealed record RepositoryHost(
    SourceControlProvider Provider,
    string DisplayName,
    Uri WebBaseUrl,
    Uri? ApiBaseUrl,
    AuthMethod AuthMethod,
    RepositoryUrlTemplates? UrlTemplates = null,
    string? Id = null)
{
    public string Id { get; init; } = Id ?? DefaultId(Provider, WebBaseUrl);

    public RepositoryUrlTemplates UrlTemplates { get; init; } = UrlTemplates ?? new RepositoryUrlTemplates();

    public static RepositoryHost GitHubCom { get; } = new(
        Provider: SourceControlProvider.GitHub,
        DisplayName: "GitHub.com",
        WebBaseUrl: new Uri("https://github.com"),
        ApiBaseUrl: new Uri("https://api.github.com"),
        AuthMethod: AuthMethod.OAuth,
        Id: "github:github.com");

    public static RepositoryHost GitLabCom { get; } = new(
        Provider: SourceControlProvider.GitLab,
        DisplayName: "GitLab.com",
        WebBaseUrl: new Uri("https://gitlab.com"),
        ApiBaseUrl: new Uri("https://gitlab.com/api/v4"),
        AuthMethod: AuthMethod.Pat,
        Id: "gitlab:gitlab.com");

    public static RepositoryHost BitbucketCloud { get; } = new(
        Provider: SourceControlProvider.BitbucketCloud,
        DisplayName: "Bitbucket Cloud",
        WebBaseUrl: new Uri("https://bitbucket.org"),
        ApiBaseUrl: new Uri("https://api.bitbucket.org/2.0"),
        AuthMethod: AuthMethod.ApiToken,
        Id: "bitbucketCloud:bitbucket.org");

    public static RepositoryHost Codeberg { get; } = new(
        Provider: SourceControlProvider.Forgejo,
        DisplayName: "Codeberg",
        WebBaseUrl: new Uri("https://codeberg.org"),
        ApiBaseUrl: new Uri("https://codeberg.org/api/v1"),
        AuthMethod: AuthMethod.Pat,
        Id: "forgejo:codeberg.org");

    public static string DefaultId(SourceControlProvider provider, Uri webBaseUrl) =>
        $"{ProviderId(provider)}:{webBaseUrl.Host.ToLowerInvariant()}";

    private static string ProviderId(SourceControlProvider provider) =>
        provider switch
        {
            SourceControlProvider.GitHub => "github",
            SourceControlProvider.GitLab => "gitlab",
            SourceControlProvider.BitbucketCloud => "bitbucketCloud",
            SourceControlProvider.Forgejo => "forgejo",
            SourceControlProvider.Gitea => "gitea",
            SourceControlProvider.CustomGit => "customGit",
            _ => provider.ToString(),
        };
}

public sealed record RepositoryAccount(
    SourceControlProvider Provider,
    Uri WebHost,
    Uri? ApiHost,
    string? Username = null);
