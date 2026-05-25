using RepoBar.Core.Api;
using Xunit;

namespace RepoBar.Tests;

public sealed class GitLabMapperTests
{
    [Fact]
    public void MapsMergeRequestReleaseBranchAndContributor()
    {
        GitLabUserDto author = new("tanuki", new Uri("https://gitlab.com/tanuki"));
        GitLabMergeRequestDto mergeRequest = new(
            Iid: 7,
            Title: "Add Windows port",
            Url: new Uri("https://gitlab.com/group/app/-/merge_requests/7"),
            UpdatedAt: new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero),
            CreatedAt: null,
            Draft: true,
            SourceBranch: "feature/windows",
            TargetBranch: "main",
            Author: author);

        PullRequestSummary mappedMr = GitLabMappers.MergeRequest(mergeRequest);

        Assert.Equal(7, mappedMr.Number);
        Assert.True(mappedMr.IsDraft);
        Assert.Equal("feature/windows", mappedMr.HeadRefName);
        Assert.Equal("main", mappedMr.BaseRefName);

        BranchSummary branch = GitLabMappers.Branch(new GitLabBranchDto("main", Protected: true, new GitLabCommitDto("abc", "commit", null, null)));
        ContributorSummary contributor = GitLabMappers.Contributor(new GitLabContributorDto("Ada", "ada@example.com", 9));

        Assert.True(branch.IsProtected);
        Assert.Equal("abc", branch.CommitSha);
        Assert.Equal(9, contributor.Contributions);
    }

    [Fact]
    public void MapsTreeItemsAndPipelines()
    {
        ContentItemSummary item = GitLabMappers.TreeItem(new GitLabTreeItemDto("src", "src", "tree", "abc"));
        WorkflowRunSummary pipeline = GitLabMappers.Pipeline(new GitLabPipelineDto(
            Id: 12,
            Url: new Uri("https://gitlab.com/group/app/-/pipelines/12"),
            UpdatedAt: new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero),
            Status: "success",
            Ref: "main"));

        Assert.Equal("src", item.Name);
        Assert.Equal("tree", item.Type);
        Assert.Equal("Pipeline #12", pipeline.Name);
        Assert.Equal("main", pipeline.Branch);
    }
}
