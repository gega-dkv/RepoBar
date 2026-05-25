using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public static class GitHubMappers
{
    public static IssueSummary Issue(GitHubIssueDto dto) =>
        new(
            dto.Number,
            dto.Title,
            dto.Url,
            dto.UpdatedAt,
            dto.CreatedAt,
            dto.User?.Login);

    public static PullRequestSummary PullRequest(GitHubIssueDto dto) =>
        new(
            dto.Number,
            dto.Title,
            dto.Url,
            dto.UpdatedAt,
            dto.CreatedAt,
            dto.User?.Login,
            IsDraft: false,
            HeadRefName: null,
            BaseRefName: null);

    public static ReleaseSummary Release(GitHubReleaseDto dto) =>
        new(
            string.IsNullOrWhiteSpace(dto.Name) ? dto.TagName : dto.Name,
            dto.TagName,
            dto.HtmlUrl,
            dto.PublishedAt ?? DateTimeOffset.MinValue,
            dto.Prerelease,
            dto.Author?.Login);

    public static BranchSummary Branch(GitHubBranchDto dto) =>
        new(dto.Name, dto.Commit?.Sha ?? string.Empty, dto.Protected);

    public static TagSummary Tag(GitHubTagDto dto) =>
        new(dto.Name, dto.Commit?.Sha ?? string.Empty);

    public static WorkflowRunSummary WorkflowRun(GitHubWorkflowRunDto dto) =>
        new(
            dto.Name ?? "Workflow run",
            dto.HtmlUrl,
            dto.UpdatedAt,
            dto.Status,
            dto.Conclusion,
            dto.HeadBranch,
            dto.Event,
            dto.RunNumber);

    public static DiscussionSummary Discussion(GitHubDiscussionDto dto) =>
        new(
            dto.Title,
            dto.HtmlUrl,
            dto.UpdatedAt,
            dto.User?.Login,
            dto.Comments,
            dto.Category?.Name);

    public static CommitSummary Commit(GitHubCommitDto dto) =>
        new(
            dto.Sha,
            FirstLine(dto.Commit?.Message) ?? dto.Sha,
            dto.HtmlUrl,
            dto.Commit?.Author?.Date);

    public static ContributorSummary Contributor(GitHubContributorDto dto) =>
        new(dto.Login ?? "unknown", Email: null, dto.Contributions);

    public static ContentItemSummary ContentItem(GitHubContentItemDto dto) =>
        new(dto.Name, dto.Path, dto.Type, dto.HtmlUrl, dto.Sha);

    public static RateLimitResourcesSnapshot RateLimits(GitHubRateLimitEnvelope envelope) =>
        new(envelope.Resources.Select(pair => new RateLimitResourceSnapshot(
            pair.Key,
            pair.Value.Limit,
            pair.Value.Remaining,
            pair.Value.Reset is long reset ? DateTimeOffset.FromUnixTimeSeconds(reset) : null)).ToArray());

    public static IReadOnlyList<HeatmapCell> HeatmapCells(IEnumerable<GitHubCommitActivityWeekDto> weeks)
    {
        List<HeatmapCell> cells = [];
        foreach (GitHubCommitActivityWeekDto week in weeks)
        {
            DateOnly start = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(week.Week).UtcDateTime);
            for (int index = 0; index < week.Days.Count; index++)
            {
                cells.Add(new HeatmapCell(start.AddDays(index), week.Days[index]));
            }
        }

        return cells;
    }

    public static IReadOnlyList<HeatmapCell> ContributionHeatmapCells(GitHubContributionGraphQLResponse response)
    {
        IReadOnlyList<GitHubContributionWeekDto> weeks = response.Data?.User?.ContributionsCollection.ContributionCalendar.Weeks ?? [];
        return weeks
            .SelectMany(week => week.ContributionDays)
            .Select(day => new HeatmapCell(
                DateOnly.Parse(day.Date, System.Globalization.CultureInfo.InvariantCulture),
                day.ContributionCount))
            .ToArray();
    }

    private static string? FirstLine(string? value) =>
        value?.Split('\n', 2)[0];
}
