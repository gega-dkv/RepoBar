using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class ChangelogPreviewServiceTests
{
    [Fact]
    public async Task PreviewReadsFirstOrMatchingChangelogSectionFromLocalCheckout()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Changelog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "CHANGELOG.md"),
            """
            # Changelog

            ## v1.2.0
            - New Windows dashboard
            - OAuth login

            ## v1.1.0
            - Previous release
            """);
        string? latest = await ChangelogPreviewService.PreviewForRepositoryAsync(root);
        string? selected = await ChangelogPreviewService.PreviewForRepositoryAsync(root, "v1.1.0");

        Assert.Contains("v1.2.0", latest, StringComparison.Ordinal);
        Assert.Contains("New Windows dashboard", latest, StringComparison.Ordinal);
        Assert.Contains("v1.1.0", selected, StringComparison.Ordinal);
        Assert.Contains("Previous release", selected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewReturnsNullWhenNoChangelogExists()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Changelog.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string? preview = await ChangelogPreviewService.PreviewForRepositoryAsync(root);

        Assert.Null(preview);
    }
}
