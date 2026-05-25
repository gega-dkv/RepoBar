using System.Net.Http.Headers;
using System.Text;
using RepoBar.Core.Api;
using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class ProviderRequestBuilderTests
{
    [Fact]
    public void GitHubRequestsUseCurrentApiVersionAndBearerAuth()
    {
        ProviderCredential credential = new(
            SourceControlProvider.GitHub,
            new Uri("https://api.github.com"),
            AuthMethod.OAuth,
            CredentialHeaderStyle.AuthorizationBearer,
            Token: "redacted");

        using HttpRequestMessage request = ProviderRequestBuilder.Get(
            SourceControlProvider.GitHub,
            new Uri("https://api.github.com"),
            "repos/octocat/hello-world/issues",
            [new KeyValuePair<string, string?>("per_page", "20")],
            credential,
            etag: "\"etag\"");

        Assert.Equal(new Uri("https://api.github.com/repos/octocat/hello-world/issues?per_page=20"), request.RequestUri);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "redacted"), request.Headers.Authorization);
        Assert.Contains("application/vnd.github+json", request.Headers.GetValues("Accept"));
        Assert.Contains("2026-03-10", request.Headers.GetValues("X-GitHub-Api-Version"));
        Assert.Contains("\"etag\"", request.Headers.GetValues("If-None-Match"));
    }

    [Fact]
    public void GitLabPatRequestsUsePrivateTokenHeader()
    {
        ProviderCredential credential = new(
            SourceControlProvider.GitLab,
            new Uri("https://gitlab.com/api/v4"),
            AuthMethod.Pat,
            CredentialHeaderStyle.PrivateToken,
            Token: "redacted");

        using HttpRequestMessage request = ProviderRequestBuilder.Get(
            SourceControlProvider.GitLab,
            new Uri("https://gitlab.com/api/v4/"),
            "projects",
            credential: credential);

        Assert.Equal(new Uri("https://gitlab.com/api/v4/projects"), request.RequestUri);
        Assert.Contains("redacted", request.Headers.GetValues("PRIVATE-TOKEN"));
    }

    [Fact]
    public void BasicCredentialUsesUsernameAndToken()
    {
        ProviderCredential credential = new(
            SourceControlProvider.BitbucketCloud,
            new Uri("https://api.bitbucket.org/2.0"),
            AuthMethod.ApiToken,
            CredentialHeaderStyle.Basic,
            Token: "api-token",
            Username: "user@example.com");

        using HttpRequestMessage request = ProviderRequestBuilder.Get(
            SourceControlProvider.BitbucketCloud,
            new Uri("https://api.bitbucket.org/2.0/"),
            "user",
            credential: credential);

        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.com:api-token"));
        Assert.Equal(new AuthenticationHeaderValue("Basic", expected), request.Headers.Authorization);
    }
}
