using RepoBar.Core.Api;
using Xunit;

namespace RepoBar.Tests;

public sealed class PaginationTests
{
    [Fact]
    public void ParsesGitHubNextLink()
    {
        Uri? next = Pagination.GitHubNextLink(
            [
                "<https://api.github.com/user/repos?page=2>; rel=\"next\", <https://api.github.com/user/repos?page=5>; rel=\"last\"",
            ]);

        Assert.Equal(new Uri("https://api.github.com/user/repos?page=2"), next);
    }

    [Fact]
    public void ParsesGitLabNextPageHeader()
    {
        Assert.Equal(3, Pagination.GitLabNextPage(["3"]));
        Assert.Null(Pagination.GitLabNextPage([""]));
    }
}
