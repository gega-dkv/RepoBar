using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public sealed class GitLabClient(HttpClient httpClient) : IRepositoryService
{
    private readonly ProviderJsonRequestRunner runner = new(httpClient);
    private IReadOnlyList<Repository> repositoryCache = [];

    public SourceControlProvider Provider => Credential?.Provider ?? SourceControlProvider.GitLab;

    public ProviderCapabilities Capabilities => ProviderCapabilities.For(Provider);

    public Uri ApiHost { get; private set; } = RepositoryHost.GitLabCom.ApiBaseUrl!;

    public ProviderCredential? Credential { get; private set; }

    public void SetApiHost(Uri apiHost)
    {
        if (apiHost.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(apiHost.Host))
        {
            throw new ArgumentException("GitLab API host must be an HTTPS URL with a host.", nameof(apiHost));
        }

        ApiHost = apiHost;
    }

    public void SetCredential(ProviderCredential? credential)
    {
        Credential = credential;
    }

    public HttpRequestMessage BuildProjectsRequest(int? limit = null) =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            "projects",
            LimitQuery(limit),
            Credential);

    public HttpRequestMessage BuildProjectMergeRequestsRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            $"projects/{Uri.EscapeDataString(pathWithNamespace)}/merge_requests",
            LimitQuery(limit),
            Credential);

    public HttpRequestMessage BuildProjectIssuesRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/issues", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectReleasesRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/releases", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectBranchesRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/branches", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectTagsRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/tags", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectCommitsRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/commits", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectContributorsRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/contributors", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectRawFileRequest(string pathWithNamespace, string filePath, string reference = "HEAD") =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/files/{Uri.EscapeDataString(filePath)}/raw",
            [new KeyValuePair<string, string?>("ref", reference)],
            Credential);

    public HttpRequestMessage BuildProjectTreeRequest(string pathWithNamespace, string? path = null, int limit = 20) =>
        ProviderRequestBuilder.Get(
            Provider,
            ApiHost,
            $"projects/{Uri.EscapeDataString(pathWithNamespace)}/repository/tree",
            LimitQuery(limit).Concat(path is null ? [] : [new KeyValuePair<string, string?>("path", path)]),
            Credential);

    public HttpRequestMessage BuildProjectPipelinesRequest(string pathWithNamespace, int limit = 20) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}/pipelines", LimitQuery(limit), Credential);

    public HttpRequestMessage BuildProjectDetailRequest(string pathWithNamespace) =>
        ProviderRequestBuilder.Get(Provider, ApiHost, $"projects/{Uri.EscapeDataString(pathWithNamespace)}", credential: Credential);

    public async Task<IReadOnlyList<Repository>> RepositoryListAsync(int? limit, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectsRequest(limit);
        IReadOnlyList<GitLabProjectDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabProjectDto>>(request, cancellationToken).ConfigureAwait(false);
        repositoryCache = dtos.Select(dto => dto.ToRepository(ApiHost)).ToArray();
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
        GitLabUserDto user = await runner.SendJsonAsync<GitLabUserDto>(request, cancellationToken).ConfigureAwait(false);
        return new UserIdentity(user.Username, user.WebUrl is null ? new Uri("https://gitlab.com") : new Uri(user.WebUrl.GetLeftPart(UriPartial.Authority)));
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
            "projects",
            [new KeyValuePair<string, string?>("search", trimmed), new KeyValuePair<string, string?>("per_page", "20")],
            Credential);
        IReadOnlyList<GitLabProjectDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabProjectDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(dto => dto.ToRepository(ApiHost)).ToArray();
    }

    public async Task<Repository> FullRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        string pathWithNamespace = PathWithNamespace(owner, name);
        using HttpRequestMessage request = BuildProjectDetailRequest(pathWithNamespace);
        GitLabProjectDto dto = await runner.SendJsonAsync<GitLabProjectDto>(request, cancellationToken).ConfigureAwait(false);
        return dto.ToRepository(ApiHost);
    }

    public Task<IReadOnlyList<PullRequestSummary>> RecentPullRequestsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentMergeRequestsAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentIssuesAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentReleasesAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentBranchesAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentTagsAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentCommitsAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        TopContributorsAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<WorkflowRunSummary>> RecentWorkflowRunsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default) =>
        RecentPipelinesAsync(PathWithNamespace(owner, name), limit, cancellationToken);

    public Task<IReadOnlyList<DiscussionSummary>> RecentDiscussionsAsync(string owner, string name, int limit = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnsupportedProviderFeatureException(SourceControlProvider.GitLab, "discussions");
    }

    public Task<IReadOnlyList<ContentItemSummary>> RepositoryContentsAsync(string owner, string name, string? path = null, CancellationToken cancellationToken = default) =>
        RepositoryTreeAsync(PathWithNamespace(owner, name), path, cancellationToken: cancellationToken);

    public Task<byte[]> RepositoryFileContentsAsync(string owner, string name, string path, CancellationToken cancellationToken = default) =>
        RawFileContentsAsync(PathWithNamespace(owner, name), path, cancellationToken: cancellationToken);

    public Task<TrafficSummary?> TrafficAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnsupportedProviderFeatureException(SourceControlProvider.GitLab, "traffic stats");
    }

    public Task<RateLimitResourcesSnapshot> RefreshRateLimitResourcesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnsupportedProviderFeatureException(SourceControlProvider.GitLab, "rate-limit resources");
    }

    public Task<IReadOnlyList<HeatmapCell>> RepositoryHeatmapAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnsupportedProviderFeatureException(SourceControlProvider.GitLab, "contribution heatmap");
    }

    public Task<IReadOnlyList<HeatmapCell>> UserContributionHeatmapAsync(string login, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new UnsupportedProviderFeatureException(SourceControlProvider.GitLab, $"user contribution heatmap for {login}");
    }

    public async Task<IReadOnlyList<IssueSummary>> RecentIssuesAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectIssuesRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabIssueDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabIssueDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Issue).ToArray();
    }

    public async Task<IReadOnlyList<PullRequestSummary>> RecentMergeRequestsAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectMergeRequestsRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabMergeRequestDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabMergeRequestDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.MergeRequest).ToArray();
    }

    public async Task<IReadOnlyList<ReleaseSummary>> RecentReleasesAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectReleasesRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabReleaseDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabReleaseDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Release).ToArray();
    }

    public async Task<IReadOnlyList<BranchSummary>> RecentBranchesAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectBranchesRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabBranchDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabBranchDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Branch).ToArray();
    }

    public async Task<IReadOnlyList<TagSummary>> RecentTagsAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectTagsRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabTagDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabTagDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Tag).ToArray();
    }

    public async Task<IReadOnlyList<CommitSummary>> RecentCommitsAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectCommitsRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabCommitDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabCommitDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Commit).ToArray();
    }

    public async Task<IReadOnlyList<ContributorSummary>> TopContributorsAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectContributorsRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabContributorDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabContributorDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Contributor).ToArray();
    }

    public async Task<IReadOnlyList<ContentItemSummary>> RepositoryTreeAsync(string pathWithNamespace, string? path = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectTreeRequest(pathWithNamespace, path, limit);
        IReadOnlyList<GitLabTreeItemDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabTreeItemDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.TreeItem).ToArray();
    }

    public async Task<byte[]> RawFileContentsAsync(string pathWithNamespace, string filePath, string reference = "HEAD", CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectRawFileRequest(pathWithNamespace, filePath, reference);
        return await runner.SendBytesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkflowRunSummary>> RecentPipelinesAsync(string pathWithNamespace, int limit = 20, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildProjectPipelinesRequest(pathWithNamespace, limit);
        IReadOnlyList<GitLabPipelineDto> dtos = await runner.SendJsonAsync<IReadOnlyList<GitLabPipelineDto>>(request, cancellationToken).ConfigureAwait(false);
        return dtos.Select(GitLabMappers.Pipeline).ToArray();
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

    private static string PathWithNamespace(string owner, string name) => $"{owner}/{name}";
}
