using System.Text.Json.Serialization;
using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public sealed record GitLabUserDto(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("web_url")] Uri? WebUrl);

public sealed record GitLabProjectDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("path_with_namespace")] string PathWithNamespace,
    [property: JsonPropertyName("web_url")] Uri? WebUrl,
    [property: JsonPropertyName("archived")] bool Archived,
    [property: JsonPropertyName("star_count")] int StarCount,
    [property: JsonPropertyName("forks_count")] int ForksCount,
    [property: JsonPropertyName("open_issues_count")] int? OpenIssuesCount,
    [property: JsonPropertyName("last_activity_at")] DateTimeOffset? LastActivityAt)
{
    public Repository ToRepository(Uri apiHost)
    {
        string[] parts = PathWithNamespace.Split('/');
        string owner = parts.Length > 1 ? string.Join('/', parts.Take(parts.Length - 1)) : string.Empty;
        Uri apiUrl = new(apiHost, $"projects/{Uri.EscapeDataString(PathWithNamespace)}");

        return new Repository(
            id: Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            name: Name,
            owner: owner,
            identity: new RepositoryIdentity(
                SourceControlProvider.GitLab,
                Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name,
                owner,
                pathWithNamespace: PathWithNamespace,
                slug: Path,
                webUrl: WebUrl,
                apiUrl: apiUrl,
                providerSpecificId: Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            isArchived: Archived,
            stats: new RepositoryStats(
                OpenIssues: OpenIssuesCount ?? 0,
                OpenPulls: 0,
                Stars: StarCount,
                Forks: ForksCount,
                PushedAt: LastActivityAt));
    }
}

public sealed record GitLabIssueDto(
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("web_url")] Uri Url,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("author")] GitLabUserDto? Author);

public sealed record GitLabMergeRequestDto(
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("web_url")] Uri Url,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("source_branch")] string? SourceBranch,
    [property: JsonPropertyName("target_branch")] string? TargetBranch,
    [property: JsonPropertyName("author")] GitLabUserDto? Author);

public sealed record GitLabReleaseDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("_links")] GitLabReleaseLinks? Links,
    [property: JsonPropertyName("released_at")] DateTimeOffset? ReleasedAt,
    [property: JsonPropertyName("author")] GitLabUserDto? Author);

public sealed record GitLabReleaseLinks(
    [property: JsonPropertyName("self")] Uri? Self);

public sealed record GitLabBranchDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("protected")] bool Protected,
    [property: JsonPropertyName("commit")] GitLabCommitDto? Commit);

public sealed record GitLabTagDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("commit")] GitLabCommitDto? Commit);

public sealed record GitLabCommitDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("web_url")] Uri? WebUrl,
    [property: JsonPropertyName("authored_date")] DateTimeOffset? AuthoredDate);

public sealed record GitLabContributorDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("commits")] int Commits);

public sealed record GitLabTreeItemDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string? Id);

public sealed record GitLabPipelineDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("web_url")] Uri Url,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("ref")] string? Ref);
