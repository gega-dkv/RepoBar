using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public sealed class GitHubClient(HttpClient httpClient) : IRepositoryService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private const string ContributionCalendarQuery = """
        query RepoBarContributionCalendar($login: String!) {
          user(login: $login) {
            contributionsCollection {
              contributionCalendar {
                weeks {
                  contributionDays {
                    date
                    contributionCount
                  }
                }
              }
            }
          }
        }
        """;

    private readonly ProviderJsonRequestRunner runner = new(httpClient);
    private IReadOnlyList<Repository> repositoryCache = [];

    public SourceControlProvider Provider => Credential?.Provider ?? SourceControlProvider.GitHub;

    public ProviderCapabilities Capabilities => ProviderCapabilities.For(Provider);

    public Uri ApiHost { get; private set; } = RepositoryHost.GitHubCom.ApiBaseUrl!;

    public ProviderCredential? Credential { get; private set; }

    public void SetApiHost(Uri apiHost)
    {
        if (apiHost.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(apiHost.Host))
        {
            throw new ArgumentException("GitHub API host must be an HTTPS URL with a host.", nameof(apiHost));
        }

        ApiHost = apiHost;
    }

    public void SetCredential(ProviderCredential? credential)
    {
        Credential = credential;
    }

    public HttpRequestMessage BuildRepositoryListRequest(int? limit = null) =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            "user/repos",
            LimitQuery(limit),
            Credential);

    public HttpRequestMessage BuildRepositoryIssuesRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            $"repos/{owner}/{name}/issues",
            LimitQuery(limit).Append(new KeyValuePair<string, string?>("state", "open")),
            Credential);

    public HttpRequestMessage BuildRepositoryPullRequestsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/pulls", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryReleasesRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/releases", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryBranchesRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/branches", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryTagsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/tags", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryWorkflowRunsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/actions/runs", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryDiscussionsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/discussions", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryContentsRequest(string owner, string name, string? path = null) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/contents/{path?.TrimStart('/') ?? string.Empty}", credential: Credential);

    public HttpRequestMessage BuildRepositoryTrafficViewsRequest(string owner, string name) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/traffic/views", credential: Credential);

    public HttpRequestMessage BuildRepositoryTrafficClonesRequest(string owner, string name) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/traffic/clones", credential: Credential);

    public HttpRequestMessage BuildRepositoryCommitsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/commits", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryContributorsRequest(string owner, string name, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/contributors", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildRepositoryCommitActivityRequest(string owner, string name) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}/stats/commit_activity", credential: Credential);

    public HttpRequestMessage BuildRepositoryDetailRequest(string owner, string name) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"repos/{owner}/{name}", credential: Credential);

    public HttpRequestMessage BuildRateLimitRequest() =>
        ProviderRequestBuilder.Get(Provider, ApiHost, "rate_limit", credential: Credential);

    public HttpRequestMessage BuildContributionCalendarRequest(string login)
    {
        GraphQLRequest body = new(
            ContributionCalendarQuery,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["login"] = login,
            });
        string json = System.Text.Json.JsonSerializer.Serialize(body, JsonOptions);
        return ProviderRequestBuilder.PostJson(Provider, ApiHost, "graphql", json, Credential);
    }

    public async Task<IReadOnlyList<Repository>> RepositoryListAsync(int? limit, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryListRequest(limit);
        IReadOnlyList<GitHubRepositoryDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubRepositoryDto>>(request, cancellationToken).ConfigureAwait(false);
        repositoryCache = dtos.Select(dto => dto.ToRepository()).ToArray();
        return repositoryCache;
    }

    public Task<IReadOnlyList<Repository>> CachedRepositoryListAsync(int? limit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Repository> result = limit is > 0
            ? repositoryCache.Take(limit.Value).ToArray()
            : repositoryCache;
        return Task.FromResult(result);
    }

    public async Task<UserIdentity> CurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = ProviderRequestBuilder.Get(Provider, ApiHost, "user", credential: Credential);
        GitHubUserDto user = await runner.SendJsonAsync<GitHubUserDto>(request, cancellationToken).ConfigureAwait(false);
        return new UserIdentity(user.Login, user.HtmlUrl is null ? new Uri("https://github.com") : new Uri(user.HtmlUrl.GetLeftPart(UriPartial.Authority)));
    }

    public async Task<IReadOnlyList<Repository>> SearchRepositoriesAsync(string query, CancellationToken cancellationToken = default)
    {
        string trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return await RepositoryListAsync(20, cancellationToken).ConfigureAwait(false);
        }

        using HttpRequestMessage request = ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            "search/repositories",
            [new KeyValuePair<string, string?>("q", trimmed), new KeyValuePair<string, string?>("per_page", "20")],
            Credential);
        GitHubSearchRepositoriesEnvelope envelope = await runner.SendJsonAsync<GitHubSearchRepositoriesEnvelope>(request, cancellationToken).ConfigureAwait(false);
        return envelope.Items.Select(dto => dto.ToRepository()).ToArray();
    }

    public async Task<Repository> FullRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryDetailRequest(owner, name);
        GitHubRepositoryDto dto = await runner.SendJsonAsync<GitHubRepositoryDto>(request, cancellationToken).ConfigureAwait(false);
        return dto.ToRepository();
    }

    public async Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryIssuesRequest(owner, name, limit);
        IReadOnlyList<GitHubIssueDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubIssueDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Where(dto => dto.PullRequest is null).Select(GitHubMappers.Issue).ToArray();
    }

    public async Task<IReadOnlyList<PullRequestSummary>> RecentPullRequestsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryPullRequestsRequest(owner, name, limit);
        IReadOnlyList<GitHubIssueDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubIssueDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.PullRequest).ToArray();
    }

    public async Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryReleasesRequest(owner, name, limit);
        IReadOnlyList<GitHubReleaseDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubReleaseDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Release).ToArray();
    }

    public async Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryBranchesRequest(owner, name, limit);
        IReadOnlyList<GitHubBranchDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubBranchDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Branch).ToArray();
    }

    public async Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryTagsRequest(owner, name, limit);
        IReadOnlyList<GitHubTagDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubTagDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Tag).ToArray();
    }

    public async Task<IReadOnlyList<WorkflowRunSummary>> RecentWorkflowRunsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryWorkflowRunsRequest(owner, name, limit);
        GitHubWorkflowRunsEnvelope envelope = await runner.SendJsonAsync<GitHubWorkflowRunsEnvelope>(request, cancellationToken).ConfigureAwait(false);
        return envelope.WorkflowRuns.Select(GitHubMappers.WorkflowRun).ToArray();
    }

    public async Task<IReadOnlyList<DiscussionSummary>> RecentDiscussionsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryDiscussionsRequest(owner, name, limit);
        IReadOnlyList<GitHubDiscussionDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubDiscussionDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Discussion).ToArray();
    }

    public async Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryCommitsRequest(owner, name, limit);
        IReadOnlyList<GitHubCommitDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubCommitDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Commit).ToArray();
    }

    public async Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryContributorsRequest(owner, name, limit);
        IReadOnlyList<GitHubContributorDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubContributorDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.Contributor).ToArray();
    }

    public async Task<IReadOnlyList<ContentItemSummary>> RepositoryContentsAsync(string owner, string name, string? path = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryContentsRequest(owner, name, path);
        IReadOnlyList<GitHubContentItemDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitHubContentItemDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitHubMappers.ContentItem).ToArray();
    }

    public async Task<byte[]> RepositoryFileContentsAsync(string owner, string name, string path, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryContentsRequest(owner, name, path);
        return await runner.SendBytesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrafficSummary?> TrafficAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage viewsRequest = BuildRepositoryTrafficViewsRequest(owner, name);
        using HttpRequestMessage clonesRequest = BuildRepositoryTrafficClonesRequest(owner, name);
        GitHubTrafficDto views = await runner.SendJsonAsync<GitHubTrafficDto>(viewsRequest, cancellationToken).ConfigureAwait(false);
        GitHubTrafficDto clones = await runner.SendJsonAsync<GitHubTrafficDto>(clonesRequest, cancellationToken).ConfigureAwait(false);
        return new TrafficSummary(views.Uniques, clones.Uniques);
    }

    public async Task<RateLimitResourcesSnapshot> RefreshRateLimitResourcesAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRateLimitRequest();
        GitHubRateLimitEnvelope envelope = await runner.SendJsonAsync<GitHubRateLimitEnvelope>(request, cancellationToken).ConfigureAwait(false);
        return GitHubMappers.RateLimits(envelope);
    }

    public async Task<IReadOnlyList<HeatmapCell>> RepositoryHeatmapAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRepositoryCommitActivityRequest(owner, name);
        IReadOnlyList<GitHubCommitActivityWeekDto> weeks = await runner.SendJsonAsync<IReadOnlyList<GitHubCommitActivityWeekDto>>(request, cancellationToken).ConfigureAwait(false);
        return GitHubMappers.HeatmapCells(weeks);
    }

    public async Task<IReadOnlyList<HeatmapCell>> UserContributionHeatmapAsync(string login, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildContributionCalendarRequest(login);
        GitHubContributionGraphQLResponse response = await runner.SendJsonAsync<GitHubContributionGraphQLResponse>(request, cancellationToken).ConfigureAwait(false);
        return GitHubMappers.ContributionHeatmapCells(response);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        httpClient.SendAsync(request, cancellationToken);

    private static IEnumerable<KeyValuePair<string, string?>> LimitQuery(int? limit)
    {
        if (limit is > 0)
        {
            yield return new KeyValuePair<string, string?>("per_page", Math.Min(limit.Value, 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
