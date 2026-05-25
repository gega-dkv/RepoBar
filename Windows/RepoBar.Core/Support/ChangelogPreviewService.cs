namespace RepoBar.Core.Support;

public static class ChangelogPreviewService
{
    private static readonly string[] CandidateFiles =
    [
        "CHANGELOG.md",
        "Changelog.md",
        "changelog.md",
        "CHANGELOG",
    ];

    public static async Task<string?> PreviewForRepositoryAsync(
        string repositoryPath,
        string? releaseTag = null,
        int maxBodyLines = 4,
        CancellationToken cancellationToken = default)
    {
        string? path = CandidateFiles
            .Select(candidate => Path.Combine(repositoryPath, candidate))
            .FirstOrDefault(File.Exists);
        if (path is null)
        {
            return null;
        }

        string markdown = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        ChangelogSummary summary = ChangelogParser.Parse(markdown, releaseTag);
        if (summary.Selected is null)
        {
            return null;
        }

        string body = string.Join(
            Environment.NewLine,
            summary.Selected.Body
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(Math.Max(1, maxBodyLines)));
        return string.IsNullOrWhiteSpace(body)
            ? summary.Selected.Title
            : $"{summary.Selected.Title}{Environment.NewLine}{body}";
    }
}
