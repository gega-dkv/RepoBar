using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void DefaultsMatchWindowsV1Scope()
    {
        UserSettings settings = new();

        Assert.Equal(SourceControlProvider.GitHub, settings.SelectedProvider);
        Assert.Equal(AuthMethod.OAuth, settings.AuthMethod);
        Assert.Equal(53682, settings.LoopbackPort);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.RefreshInterval);
        Assert.Equal(6, settings.RepoList.DisplayLimit);
        Assert.True(settings.Appearance.ShowContributionHeader);
        Assert.True(settings.Appearance.ShowRateLimitMeterInMenuBar);
        Assert.True(settings.GitHubArchives.PreferArchiveWhenRateLimited);
    }

    [Fact]
    public void BuiltInHostsPreserveProviderSpecificApiUrls()
    {
        Assert.Equal(new Uri("https://api.github.com"), RepositoryHost.GitHubCom.ApiBaseUrl);
        Assert.Equal(new Uri("https://gitlab.com/api/v4"), RepositoryHost.GitLabCom.ApiBaseUrl);
        Assert.Equal(new Uri("https://api.bitbucket.org/2.0"), RepositoryHost.BitbucketCloud.ApiBaseUrl);
        Assert.Equal(new Uri("https://codeberg.org/api/v1"), RepositoryHost.Codeberg.ApiBaseUrl);
    }
}
