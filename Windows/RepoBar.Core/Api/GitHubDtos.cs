using System.Text.Json.Serialization;
using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public sealed record GitHubUserDto(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("html_url")] Uri? HtmlUrl);

public sealed record GitHubRepositoryDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("node_id")] string? NodeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("html_url")] Uri? HtmlUrl,
    [property: JsonPropertyName("url")] Uri? ApiUrl,
    [property: JsonPropertyName("fork")] bool Fork,
    [property: JsonPropertyName("archived")] bool Archived,
    [property: JsonPropertyName("stargazers_count")] int StargazersCount,
    [property: JsonPropertyName("forks_count")] int ForksCount,
    [property: JsonPropertyName("open_issues_count")] int OpenIssuesCount,
    [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt)
{
    public Repository ToRepository()
    {
        string[] parts = FullName.Split('/', 2);
        string owner = parts.Length == 2 ? parts[0] : string.Empty;
        int openIssues = Math.Max(OpenIssuesCount, 0);

        return new Repository(
            id: (NodeId ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            name: Name,
            owner: owner,
            identity: new RepositoryIdentity(
                SourceControlProvider.GitHub,
                NodeId ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name,
                owner,
                pathWithNamespace: FullName,
                webUrl: HtmlUrl,
                apiUrl: ApiUrl,
                providerSpecificId: Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            isFork: Fork,
            isArchived: Archived,
            stats: new RepositoryStats(
                OpenIssues: openIssues,
                OpenPulls: 0,
                Stars: StargazersCount,
                Forks: ForksCount,
                PushedAt: PushedAt));
    }
}

public sealed record GitHubIssueDto(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("html_url")] Uri Url,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("pull_request")] object? PullRequest,
    [property: JsonPropertyName("user")] GitHubUserDto? User);

public sealed record GitHubReleaseDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("html_url")] Uri HtmlUrl,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("author")] GitHubUserDto? Author);

public sealed record GitHubBranchDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("protected")] bool Protected,
    [property: JsonPropertyName("commit")] GitHubCommitRefDto? Commit);

public sealed record GitHubTagDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("commit")] GitHubCommitRefDto? Commit);

public sealed record GitHubCommitRefDto(
    [property: JsonPropertyName("sha")] string Sha);

public sealed record GitHubCommitDto(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("html_url")] Uri? HtmlUrl,
    [property: JsonPropertyName("commit")] GitHubCommitDetailsDto? Commit);

public sealed record GitHubCommitDetailsDto(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("author")] GitHubCommitAuthorDto? Author);

public sealed record GitHubCommitAuthorDto(
    [property: JsonPropertyName("date")] DateTimeOffset? Date);

public sealed record GitHubContributorDto(
    [property: JsonPropertyName("login")] string? Login,
    [property: JsonPropertyName("contributions")] int Contributions);

public sealed record GitHubContentItemDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("html_url")] Uri? HtmlUrl,
    [property: JsonPropertyName("sha")] string? Sha);

public sealed record GitHubWorkflowRunDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("html_url")] Uri HtmlUrl,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("conclusion")] string? Conclusion,
    [property: JsonPropertyName("head_branch")] string? HeadBranch,
    [property: JsonPropertyName("event")] string? Event,
    [property: JsonPropertyName("run_number")] int? RunNumber);

public sealed record GitHubWorkflowRunsEnvelope(
    [property: JsonPropertyName("workflow_runs")] IReadOnlyList<GitHubWorkflowRunDto> WorkflowRuns);

public sealed record GitHubDiscussionDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("html_url")] Uri HtmlUrl,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("user")] GitHubUserDto? User,
    [property: JsonPropertyName("comments")] int Comments,
    [property: JsonPropertyName("category")] GitHubDiscussionCategoryDto? Category);

public sealed record GitHubDiscussionCategoryDto(
    [property: JsonPropertyName("name")] string? Name);

public sealed record GitHubTrafficDto(
    [property: JsonPropertyName("uniques")] int Uniques);

public sealed record GitHubSearchRepositoriesEnvelope(
    [property: JsonPropertyName("items")] IReadOnlyList<GitHubRepositoryDto> Items);

public sealed record GitHubRateLimitEnvelope(
    [property: JsonPropertyName("resources")] IReadOnlyDictionary<string, GitHubRateLimitResourceDto> Resources);

public sealed record GitHubRateLimitResourceDto(
    [property: JsonPropertyName("limit")] int? Limit,
    [property: JsonPropertyName("remaining")] int? Remaining,
    [property: JsonPropertyName("reset")] long? Reset);

public sealed record GitHubCommitActivityWeekDto(
    [property: JsonPropertyName("week")] long Week,
    [property: JsonPropertyName("days")] IReadOnlyList<int> Days);

public sealed record GraphQLRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("variables")] IReadOnlyDictionary<string, string> Variables);

public sealed record GitHubContributionGraphQLResponse(
    [property: JsonPropertyName("data")] GitHubContributionGraphQLData? Data);

public sealed record GitHubContributionGraphQLData(
    [property: JsonPropertyName("user")] GitHubContributionGraphQLUser? User);

public sealed record GitHubContributionGraphQLUser(
    [property: JsonPropertyName("contributionsCollection")] GitHubContributionCollectionDto ContributionsCollection);

public sealed record GitHubContributionCollectionDto(
    [property: JsonPropertyName("contributionCalendar")] GitHubContributionCalendarDto ContributionCalendar);

public sealed record GitHubContributionCalendarDto(
    [property: JsonPropertyName("weeks")] IReadOnlyList<GitHubContributionWeekDto> Weeks);

public sealed record GitHubContributionWeekDto(
    [property: JsonPropertyName("contributionDays")] IReadOnlyList<GitHubContributionDayDto> ContributionDays);

public sealed record GitHubContributionDayDto(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("contributionCount")] int ContributionCount);
