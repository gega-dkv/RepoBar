using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class ChangelogParserTests
{
    [Fact]
    public void SelectsFirstSectionByDefault()
    {
        ChangelogSummary summary = ChangelogParser.Parse(
            """
            # Changelog

            ## 1.2.0
            - Added Windows scaffold.

            ## 1.1.0
            - Previous release.
            """);

        Assert.Equal(2, summary.Sections.Count);
        Assert.Equal("1.2.0", summary.Selected?.Title);
        Assert.Contains("Windows scaffold", summary.Selected?.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectsSectionByReleaseTag()
    {
        ChangelogSummary summary = ChangelogParser.Parse(
            """
            ## v2.0.0
            - Major update.

            ## v1.9.0
            - Maintenance.
            """,
            releaseTag: "v1.9.0");

        Assert.Equal("v1.9.0", summary.Selected?.Title);
    }
}
