using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class UnsupportedProviderFeatureTests
{
    [Fact]
    public void ErrorMessageUsesProviderLabelAndFeature()
    {
        UnsupportedProviderFeatureException error = new(SourceControlProvider.GitLab, "traffic stats");

        Assert.Equal(SourceControlProvider.GitLab, error.Provider);
        Assert.Equal("traffic stats", error.Feature);
        Assert.Equal("GitLab does not support traffic stats.", error.Message);
    }
}
