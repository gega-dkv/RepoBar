using RepoBar.Core.Api;
using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class ProviderClientRequestTests
{
    [Fact]
    public void GitHubRepositoryListRequestMatchesRestEndpoint()
    {
        GitHubClient client = new(new HttpClient());

        using HttpRequestMessage request = client.BuildRepositoryListRequest(limit: 50);

        Assert.Equal(new Uri("https://api.github.com/user/repos?per_page=50"), request.RequestUri);
    }

    [Fact]
    public void GitHubRecentSurfaceRequestsMatchRestEndpoints()
    {
        GitHubClient client = new(new HttpClient());

        using HttpRequestMessage pulls = client.BuildRepositoryPullRequestsRequest("owner", "repo", 10);
        using HttpRequestMessage releases = client.BuildRepositoryReleasesRequest("owner", "repo", 10);
        using HttpRequestMessage runs = client.BuildRepositoryWorkflowRunsRequest("owner", "repo", 10);
        using HttpRequestMessage discussions = client.BuildRepositoryDiscussionsRequest("owner", "repo", 10);
        using HttpRequestMessage contents = client.BuildRepositoryContentsRequest("owner", "repo", "CHANGELOG.md");
        using HttpRequestMessage traffic = client.BuildRepositoryTrafficViewsRequest("owner", "repo");
        using HttpRequestMessage commits = client.BuildRepositoryCommitsRequest("owner", "repo", 10);
        using HttpRequestMessage contributors = client.BuildRepositoryContributorsRequest("owner", "repo", 10);
        using HttpRequestMessage heatmap = client.BuildRepositoryCommitActivityRequest("owner", "repo");
        using HttpRequestMessage rateLimit = client.BuildRateLimitRequest();
        using HttpRequestMessage contributionCalendar = client.BuildContributionCalendarRequest("octocat");

        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/pulls?per_page=10"), pulls.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/releases?per_page=10"), releases.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/actions/runs?per_page=10"), runs.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/discussions?per_page=10"), discussions.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/contents/CHANGELOG.md"), contents.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/traffic/views"), traffic.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/commits?per_page=10"), commits.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/contributors?per_page=10"), contributors.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/repos/owner/repo/stats/commit_activity"), heatmap.RequestUri);
        Assert.Equal(new Uri("https://api.github.com/rate_limit"), rateLimit.RequestUri);
        Assert.Equal(HttpMethod.Post, contributionCalendar.Method);
        Assert.Equal(new Uri("https://api.github.com/graphql"), contributionCalendar.RequestUri);
    }

    [Fact]
    public void GitLabProjectMergeRequestsEscapesProjectPath()
    {
        GitLabClient client = new(new HttpClient());

        using HttpRequestMessage request = client.BuildProjectMergeRequestsRequest("group/sub/app", limit: 20);

        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/merge_requests?per_page=20"), request.RequestUri);
    }

    [Fact]
    public void GitLabRecentSurfaceRequestsMatchRestEndpoints()
    {
        GitLabClient client = new(new HttpClient());

        using HttpRequestMessage issues = client.BuildProjectIssuesRequest("group/sub/app", 10);
        using HttpRequestMessage releases = client.BuildProjectReleasesRequest("group/sub/app", 10);
        using HttpRequestMessage branches = client.BuildProjectBranchesRequest("group/sub/app", 10);
        using HttpRequestMessage tags = client.BuildProjectTagsRequest("group/sub/app", 10);
        using HttpRequestMessage commits = client.BuildProjectCommitsRequest("group/sub/app", 10);
        using HttpRequestMessage raw = client.BuildProjectRawFileRequest("group/sub/app", "docs/readme.md");
        using HttpRequestMessage tree = client.BuildProjectTreeRequest("group/sub/app", "src", 10);
        using HttpRequestMessage pipelines = client.BuildProjectPipelinesRequest("group/sub/app", 10);

        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/issues?per_page=10"), issues.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/releases?per_page=10"), releases.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/repository/branches?per_page=10"), branches.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/repository/tags?per_page=10"), tags.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/repository/commits?per_page=10"), commits.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/repository/files/docs%2Freadme.md/raw?ref=HEAD"), raw.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/repository/tree?per_page=10&path=src"), tree.RequestUri);
        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects/group%2Fsub%2Fapp/pipelines?per_page=10"), pipelines.RequestUri);
    }

    [Fact]
    public void GitLabRejectsNonHttpsHosts()
    {
        GitLabClient client = new(new HttpClient());

        Assert.Throws<ArgumentException>(() => client.SetApiHost(new Uri("http://gitlab.example.com/api/v4")));
    }

    [Fact]
    public void GitHubClientReportsGitHubCapabilities()
    {
        GitHubClient client = new(new HttpClient());

        Assert.Equal(SourceControlProvider.GitHub, client.Provider);
        Assert.True(client.Capabilities.TrafficStats);
        Assert.True(client.Capabilities.Discussions);
    }
}
