using RepoBar.Core.Api;
using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class ProviderClientMappingTests
{
    [Fact]
    public async Task GitHubRepositoryListMapsRepositoryIdentityAndStats()
    {
        using HttpClient httpClient = new(new StubHandler(
            """
            [
              {
                "id": 123,
                "node_id": "R_123",
                "name": "hello-world",
                "full_name": "octocat/hello-world",
                "html_url": "https://github.com/octocat/hello-world",
                "url": "https://api.github.com/repos/octocat/hello-world",
                "fork": false,
                "archived": false,
                "stargazers_count": 42,
                "forks_count": 7,
                "open_issues_count": 3,
                "pushed_at": "2026-05-24T10:00:00Z"
              }
            ]
            """));
        GitHubClient client = new(httpClient);

        var repositories = await client.RepositoryListAsync(limit: 1);

        Assert.Single(repositories);
        Assert.Equal("octocat/hello-world", repositories[0].FullName);
        Assert.Equal(42, repositories[0].Stats.Stars);
        Assert.Equal(7, repositories[0].Stats.Forks);
        Assert.Equal(3, repositories[0].Stats.OpenIssues);
    }

    [Fact]
    public async Task GitHubSearchMapsEnvelopeItems()
    {
        using HttpClient httpClient = new(new StubHandler(
            """
            {
              "items": [
                {
                  "id": 99,
                  "node_id": "R_99",
                  "name": "RepoBar",
                  "full_name": "owner/RepoBar",
                  "html_url": "https://github.com/owner/RepoBar",
                  "url": "https://api.github.com/repos/owner/RepoBar",
                  "fork": false,
                  "archived": false,
                  "stargazers_count": 5,
                  "forks_count": 1,
                  "open_issues_count": 0,
                  "pushed_at": "2026-05-24T10:00:00Z"
                }
              ]
            }
            """));
        GitHubClient client = new(httpClient);

        var repositories = await client.SearchRepositoriesAsync("RepoBar");

        Assert.Single(repositories);
        Assert.Equal("owner/RepoBar", repositories[0].FullName);
    }

    [Fact]
    public async Task GitLabProjectListMapsRepositoryIdentityAndStats()
    {
        using HttpClient httpClient = new(new StubHandler(
            """
            [
              {
                "id": 456,
                "name": "app",
                "path": "app",
                "path_with_namespace": "group/sub/app",
                "web_url": "https://gitlab.com/group/sub/app",
                "archived": false,
                "star_count": 11,
                "forks_count": 2,
                "open_issues_count": 4,
                "last_activity_at": "2026-05-24T10:00:00Z"
              }
            ]
            """));
        GitLabClient client = new(httpClient);

        var repositories = await client.RepositoryListAsync(limit: 1);

        Assert.Single(repositories);
        Assert.Equal("group/sub/app", repositories[0].FullName);
        Assert.Equal("group/sub", repositories[0].Owner);
        Assert.Equal(11, repositories[0].Stats.Stars);
        Assert.Equal(4, repositories[0].Stats.OpenIssues);
    }

    [Fact]
    public async Task CurrentUserMapsProviderHosts()
    {
        using HttpClient githubHttp = new(new StubHandler("""{"login":"octocat","html_url":"https://github.com/octocat"}"""));
        using HttpClient gitlabHttp = new(new StubHandler("""{"username":"tanuki","web_url":"https://gitlab.com/tanuki"}"""));

        var githubUser = await new GitHubClient(githubHttp).CurrentUserAsync();
        var gitlabUser = await new GitLabClient(gitlabHttp).CurrentUserAsync();

        Assert.Equal("octocat", githubUser.Username);
        Assert.Equal(new Uri("https://github.com"), githubUser.Host);
        Assert.Equal("tanuki", gitlabUser.Username);
        Assert.Equal(new Uri("https://gitlab.com"), gitlabUser.Host);
    }

    [Fact]
    public async Task GitHubRecentMethodsMapIssueReleaseBranchTagAndWorkflowRun()
    {
        using HttpClient issuesHttp = new(new StubHandler(
            """
            [
              {
                "number": 1,
                "title": "Bug",
                "html_url": "https://github.com/owner/repo/issues/1",
                "updated_at": "2026-05-24T10:00:00Z",
                "created_at": "2026-05-23T10:00:00Z",
                "user": {"login":"octocat","html_url":"https://github.com/octocat"}
              }
            ]
            """));
        var issues = await new GitHubClient(issuesHttp).RecentIssuesAsync("owner", "repo");

        Assert.Single(issues);
        Assert.Equal("Bug", issues[0].Title);

        using HttpClient workflowHttp = new(new StubHandler(
            """
            {
              "workflow_runs": [
                {
                  "name": "CI",
                  "html_url": "https://github.com/owner/repo/actions/runs/1",
                  "updated_at": "2026-05-24T10:00:00Z",
                  "status": "completed",
                  "conclusion": "success",
                  "head_branch": "main",
                  "event": "push",
                  "run_number": 1
                }
              ]
            }
            """));
        var runs = await new GitHubClient(workflowHttp).RecentWorkflowRunsAsync("owner", "repo");

        Assert.Single(runs);
        Assert.Equal("success", runs[0].Conclusion);

        using HttpClient discussionsHttp = new(new StubHandler(
            """
            [
              {
                "title": "Roadmap",
                "html_url": "https://github.com/owner/repo/discussions/1",
                "updated_at": "2026-05-24T10:00:00Z",
                "comments": 3,
                "user": {"login":"octocat","html_url":"https://github.com/octocat"},
                "category": {"name":"General"}
              }
            ]
            """));
        var discussions = await new GitHubClient(discussionsHttp).RecentDiscussionsAsync("owner", "repo");

        Assert.Single(discussions);
        Assert.Equal("Roadmap", discussions[0].Title);
        Assert.Equal("General", discussions[0].CategoryName);
    }

    [Fact]
    public async Task GitLabRecentMethodsMapIssueMergeRequestCommitAndContributor()
    {
        using HttpClient mergeRequestsHttp = new(new StubHandler(
            """
            [
              {
                "iid": 2,
                "title": "MR",
                "web_url": "https://gitlab.com/group/app/-/merge_requests/2",
                "updated_at": "2026-05-24T10:00:00Z",
                "created_at": "2026-05-23T10:00:00Z",
                "draft": false,
                "source_branch": "feature",
                "target_branch": "main",
                "author": {"username":"tanuki","web_url":"https://gitlab.com/tanuki"}
              }
            ]
            """));
        var mergeRequests = await new GitLabClient(mergeRequestsHttp).RecentMergeRequestsAsync("group/app");

        Assert.Single(mergeRequests);
        Assert.Equal("feature", mergeRequests[0].HeadRefName);

        using HttpClient commitsHttp = new(new StubHandler(
            """
            [
              {
                "id": "abc123",
                "title": "Commit title",
                "web_url": "https://gitlab.com/group/app/-/commit/abc123",
                "authored_date": "2026-05-24T10:00:00Z"
              }
            ]
            """));
        var commits = await new GitLabClient(commitsHttp).RecentCommitsAsync("group/app");

        Assert.Single(commits);
        Assert.Equal("abc123", commits[0].Sha);
    }

    [Fact]
    public async Task GitHubMapsContributorsContentsTrafficAndRateLimits()
    {
        using HttpClient contributorsHttp = new(new StubHandler("""[{"login":"octocat","contributions":12}]"""));
        var contributors = await new GitHubClient(contributorsHttp).TopContributorsAsync("owner", "repo");
        Assert.Equal("octocat", contributors[0].Name);
        Assert.Equal(12, contributors[0].Contributions);

        using HttpClient contentsHttp = new(new StubHandler("""[{"name":"README.md","path":"README.md","type":"file","html_url":"https://github.com/owner/repo/blob/main/README.md","sha":"abc"}]"""));
        var contents = await new GitHubClient(contentsHttp).RepositoryContentsAsync("owner", "repo");
        Assert.Equal("README.md", contents[0].Name);
        Assert.Equal("abc", contents[0].Sha);

        using HttpClient rateLimitHttp = new(new StubHandler("""{"resources":{"core":{"limit":5000,"remaining":4999,"reset":1779667200}}}"""));
        var rateLimits = await new GitHubClient(rateLimitHttp).RefreshRateLimitResourcesAsync();
        Assert.Equal("core", rateLimits.Resources[0].Resource);
        Assert.Equal(4999, rateLimits.Resources[0].Remaining);

        using HttpClient heatmapHttp = new(new StubHandler("""[{"week":1779667200,"days":[0,1,2,3,4,5,6]}]"""));
        var heatmap = await new GitHubClient(heatmapHttp).RepositoryHeatmapAsync("owner", "repo");
        Assert.Equal(7, heatmap.Count);
        Assert.Equal(6, heatmap[^1].Count);

        using HttpClient contributionHttp = new(new StubHandler(
            """
            {
              "data": {
                "user": {
                  "contributionsCollection": {
                    "contributionCalendar": {
                      "weeks": [
                        {
                          "contributionDays": [
                            {"date":"2026-05-24","contributionCount":2},
                            {"date":"2026-05-25","contributionCount":5}
                          ]
                        }
                      ]
                    }
                  }
                }
              }
            }
            """));
        var contributionHeatmap = await new GitHubClient(contributionHttp).UserContributionHeatmapAsync("octocat");
        Assert.Equal(2, contributionHeatmap.Count);
        Assert.Equal(5, contributionHeatmap[^1].Count);
    }

    [Fact]
    public async Task GitLabMapsTreeRawFileAndPipelines()
    {
        using HttpClient treeHttp = new(new StubHandler("""[{"name":"src","path":"src","type":"tree","id":"abc"}]"""));
        var tree = await new GitLabClient(treeHttp).RepositoryTreeAsync("group/app");
        Assert.Equal("src", tree[0].Name);
        Assert.Equal("tree", tree[0].Type);

        using HttpClient rawHttp = new(new StubHandler("hello"));
        byte[] bytes = await new GitLabClient(rawHttp).RawFileContentsAsync("group/app", "README.md");
        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(bytes));

        using HttpClient pipelineHttp = new(new StubHandler("""[{"id":42,"web_url":"https://gitlab.com/group/app/-/pipelines/42","updated_at":"2026-05-24T10:00:00Z","status":"success","ref":"main"}]"""));
        var pipelines = await new GitLabClient(pipelineHttp).RecentPipelinesAsync("group/app");
        Assert.Equal("Pipeline #42", pipelines[0].Name);
        Assert.Equal("success", pipelines[0].Status);
    }

    [Fact]
    public async Task GitLabUnsupportedTrafficAndRateLimitDiagnosticsAreExplicit()
    {
        GitLabClient client = new(new HttpClient());

        var traffic = await Assert.ThrowsAsync<UnsupportedProviderFeatureException>(() => client.TrafficAsync("group", "app"));
        var rateLimits = await Assert.ThrowsAsync<UnsupportedProviderFeatureException>(() => client.RefreshRateLimitResourcesAsync());
        var heatmap = await Assert.ThrowsAsync<UnsupportedProviderFeatureException>(() => client.RepositoryHeatmapAsync("group", "app"));
        var discussions = await Assert.ThrowsAsync<UnsupportedProviderFeatureException>(() => client.RecentDiscussionsAsync("group", "app"));

        Assert.Equal("traffic stats", traffic.Feature);
        Assert.Equal("rate-limit resources", rateLimits.Feature);
        Assert.Equal("contribution heatmap", heatmap.Feature);
        Assert.Equal("discussions", discussions.Feature);
    }
}
