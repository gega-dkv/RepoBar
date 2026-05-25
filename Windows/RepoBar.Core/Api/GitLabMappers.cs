namespace RepoBar.Core.Api;

public static class GitLabMappers
{
    public static IssueSummary Issue(GitLabIssueDto dto) =>
        new(
            dto.Iid,
            dto.Title,
            dto.Url,
            dto.UpdatedAt,
            dto.CreatedAt,
            dto.Author?.Username);

    public static PullRequestSummary MergeRequest(GitLabMergeRequestDto dto) =>
        new(
            dto.Iid,
            dto.Title,
            dto.Url,
            dto.UpdatedAt,
            dto.CreatedAt,
            dto.Author?.Username,
            dto.Draft,
            dto.SourceBranch,
            dto.TargetBranch);

    public static ReleaseSummary Release(GitLabReleaseDto dto) =>
        new(
            dto.Name,
            dto.TagName,
            dto.Links?.Self ?? new Uri("about:blank"),
            dto.ReleasedAt ?? DateTimeOffset.MinValue,
            IsPrerelease: false,
            dto.Author?.Username);

    public static BranchSummary Branch(GitLabBranchDto dto) =>
        new(dto.Name, dto.Commit?.Id ?? string.Empty, dto.Protected);

    public static TagSummary Tag(GitLabTagDto dto) =>
        new(dto.Name, dto.Commit?.Id ?? string.Empty);

    public static CommitSummary Commit(GitLabCommitDto dto) =>
        new(dto.Id, dto.Title ?? dto.Id, dto.WebUrl, dto.AuthoredDate);

    public static ContributorSummary Contributor(GitLabContributorDto dto) =>
        new(dto.Name, dto.Email, dto.Commits);

    public static ContentItemSummary TreeItem(GitLabTreeItemDto dto) =>
        new(dto.Name, dto.Path, dto.Type, Url: null, dto.Id);

    public static WorkflowRunSummary Pipeline(GitLabPipelineDto dto) =>
        new(
            $"Pipeline #{dto.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            dto.Url,
            dto.UpdatedAt ?? DateTimeOffset.MinValue,
            dto.Status,
            Conclusion: null,
            dto.Ref,
            Event: null,
            RunNumber: null);
}
