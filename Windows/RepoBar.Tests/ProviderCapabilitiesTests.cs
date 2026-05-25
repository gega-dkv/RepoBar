using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class ProviderCapabilitiesTests
{
    [Fact]
    public void GitLabHidesGitHubOnlySurfaces()
    {
        ProviderCapabilities capabilities = ProviderCapabilities.For(SourceControlProvider.GitLab);

        Assert.True(capabilities.Repositories);
        Assert.True(capabilities.PullRequests);
        Assert.False(capabilities.TrafficStats);
        Assert.False(capabilities.ContributionCalendar);
        Assert.False(capabilities.Discussions);
    }

    [Fact]
    public void ProviderLabelsMatchSwiftReference()
    {
        Assert.Equal("GitHub", SourceControlProvider.GitHub.Label());
        Assert.Equal("GitLab", SourceControlProvider.GitLab.Label());
        Assert.Equal("Bitbucket Cloud", SourceControlProvider.BitbucketCloud.Label());
        Assert.Equal("Forgejo", SourceControlProvider.Forgejo.Label());
        Assert.Equal("Gitea", SourceControlProvider.Gitea.Label());
        Assert.Equal("Custom Git", SourceControlProvider.CustomGit.Label());
    }
}
