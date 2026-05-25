using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class RepositoryIdentityTests
{
    [Fact]
    public void BuildsPathWithNamespaceFromOwnerAndName()
    {
        RepositoryIdentity identity = RepositoryIdentity.GitHub(
            id: "repo-id",
            owner: "openclaw",
            name: "openclaw");

        Assert.Equal(SourceControlProvider.GitHub, identity.Provider);
        Assert.Equal("openclaw", identity.NamespacePath);
        Assert.Equal("openclaw/openclaw", identity.PathWithNamespace);
        Assert.Equal("openclaw", identity.Slug);
    }

    [Fact]
    public void TrimsNamespaceSlashesWhenResolvingPath()
    {
        RepositoryIdentity identity = new(
            SourceControlProvider.GitLab,
            id: "42",
            name: "app",
            namespacePath: "/group/subgroup/");

        Assert.Equal("group/subgroup/app", identity.PathWithNamespace);
    }
}
