using RepoBar.Core.Api;
using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class GitHubMapperTests
{
    [Fact]
    public void MapsReleaseFallbackNameAndWorkflowRun()
    {
        GitHubReleaseDto release = new(
            Name: null,
            TagName: "v1.0.0",
            HtmlUrl: new Uri("https://github.com/owner/repo/releases/v1.0.0"),
            PublishedAt: new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero),
            Prerelease: false,
            Author: new GitHubUserDto("octocat", new Uri("https://github.com/octocat")));

        ReleaseSummary mappedRelease = GitHubMappers.Release(release);

        Assert.Equal("v1.0.0", mappedRelease.Name);
        Assert.Equal("octocat", mappedRelease.AuthorLogin);

        GitHubWorkflowRunDto run = new(
            Name: "CI",
            HtmlUrl: new Uri("https://github.com/owner/repo/actions/runs/1"),
            UpdatedAt: new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero),
            Status: "completed",
            Conclusion: "success",
            HeadBranch: "main",
            Event: "push",
            RunNumber: 12);

        WorkflowRunSummary mappedRun = GitHubMappers.WorkflowRun(run);

        Assert.Equal("CI", mappedRun.Name);
        Assert.Equal("success", mappedRun.Conclusion);
        Assert.Equal(12, mappedRun.RunNumber);

        DiscussionSummary discussion = GitHubMappers.Discussion(new GitHubDiscussionDto(
            Title: "Roadmap",
            HtmlUrl: new Uri("https://github.com/owner/repo/discussions/1"),
            UpdatedAt: new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero),
            User: new GitHubUserDto("octocat", new Uri("https://github.com/octocat")),
            Comments: 4,
            Category: new GitHubDiscussionCategoryDto("General")));

        Assert.Equal("Roadmap", discussion.Title);
        Assert.Equal("octocat", discussion.AuthorLogin);
        Assert.Equal(4, discussion.CommentCount);
    }

    [Fact]
    public void MapsCommitAndRateLimitResources()
    {
        CommitSummary commit = GitHubMappers.Commit(new GitHubCommitDto(
            Sha: "abc",
            HtmlUrl: new Uri("https://github.com/owner/repo/commit/abc"),
            Commit: new GitHubCommitDetailsDto(
                Message: "Subject\n\nBody",
                Author: new GitHubCommitAuthorDto(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero)))));

        Assert.Equal("Subject", commit.Title);
        Assert.Equal("abc", commit.Sha);

        RateLimitResourcesSnapshot snapshot = GitHubMappers.RateLimits(new GitHubRateLimitEnvelope(
            new Dictionary<string, GitHubRateLimitResourceDto>
            {
                ["core"] = new GitHubRateLimitResourceDto(5000, 42, 1779667200),
            }));

        Assert.Equal("core", snapshot.Resources[0].Resource);
        Assert.Equal(42, snapshot.Resources[0].Remaining);
    }

    [Fact]
    public void MapsCommitActivityWeeksToHeatmapCells()
    {
        IReadOnlyList<HeatmapCell> cells = GitHubMappers.HeatmapCells(
            [
                new GitHubCommitActivityWeekDto(1779667200, [0, 1, 2, 3, 4, 5, 6]),
            ]);

        Assert.Equal(7, cells.Count);
        Assert.Equal(new DateOnly(2026, 5, 25), cells[0].Date);
        Assert.Equal(6, cells[^1].Count);
    }

    [Fact]
    public void MapsGraphQLContributionCalendarToHeatmapCells()
    {
        GitHubContributionGraphQLResponse response = new(new GitHubContributionGraphQLData(
            new GitHubContributionGraphQLUser(
                new GitHubContributionCollectionDto(
                    new GitHubContributionCalendarDto(
                        [
                            new GitHubContributionWeekDto(
                                [
                                    new GitHubContributionDayDto("2026-05-24", 2),
                                    new GitHubContributionDayDto("2026-05-25", 5),
                                ]),
                        ])))));

        IReadOnlyList<HeatmapCell> cells = GitHubMappers.ContributionHeatmapCells(response);

        Assert.Equal(2, cells.Count);
        Assert.Equal(new DateOnly(2026, 5, 25), cells[^1].Date);
        Assert.Equal(5, cells[^1].Count);
    }
}
