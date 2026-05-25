using System.Net;
using RepoBar.Core.Auth;
using RepoBar.Core.Storage;
using Xunit;

namespace RepoBar.Tests;

public sealed class OAuthLoginServiceTests
{
    [Fact]
    public void PkceChallengeUsesBase64UrlVerifierAndSha256Challenge()
    {
        PkceChallenge pkce = PkceChallenge.Generate();

        Assert.InRange(pkce.Verifier.Length, 43, 128);
        Assert.DoesNotContain("=", pkce.Verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("+", pkce.Challenge, StringComparison.Ordinal);
        Assert.DoesNotContain("/", pkce.Challenge, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizeUrlIncludesPkceStateRedirectAndOptionalScope()
    {
        Uri authorize = GitHubOAuthLoginService.BuildAuthorizeUrl(
            new Uri("https://github.com"),
            "client-id",
            new Uri("http://127.0.0.1:53682/callback"),
            "state-1",
            "challenge-1",
            "repo read:org");

        string query = authorize.Query;
        Assert.Equal("https://github.com/login/oauth/authorize", authorize.GetLeftPart(UriPartial.Path));
        Assert.Contains("client_id=client-id", query, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A53682%2Fcallback", query, StringComparison.Ordinal);
        Assert.Contains("state=state-1", query, StringComparison.Ordinal);
        Assert.Contains("code_challenge=challenge-1", query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", query, StringComparison.Ordinal);
        Assert.Contains("scope=repo%20read%3Aorg", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginOpensBrowserExchangesCodeAndStoresTokensWithoutPrintingSecrets()
    {
        StubHandler handler = new("""{"access_token":"gho_access","token_type":"bearer","expires_in":3600,"refresh_token":"ghr_refresh"}""");
        using HttpClient httpClient = new(handler);
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.OAuth.Tests", Guid.NewGuid().ToString("N"));
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        FileCredentialStore store = new(paths.DebugAuthDirectory);
        FakeBrowserLauncher browser = new();
        GitHubOAuthLoginService service = new(
            httpClient,
            store,
            new FakeCallbackServer(new OAuthCallback("oauth-code", "state-1", null)),
            browser,
            () => new PkceChallenge("verifier-1", "challenge-1"),
            () => "state-1",
            () => new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero));

        OAuthLoginResult result = await service.LoginAsync(new OAuthLoginRequest(
            new Uri("https://github.com/path?q=1"),
            "client-id",
            "client-secret",
            53682));

        CredentialRecord? token = await store.ReadAsync(GitHubOAuthLoginService.TokenService, "GitHub:github.com:oauth");
        CredentialRecord? client = await store.ReadAsync(GitHubOAuthLoginService.ClientService, "GitHub:github.com:oauth");
        OAuthTokens saved = OAuthTokens.FromSecret(token!.Secret);
        string form = handler.LastRequestBody!;

        Assert.Equal(CredentialStoreKind.File, result.Store);
        Assert.NotNull(browser.LastOpened);
        Assert.Contains("code_challenge=challenge-1", browser.LastOpened!.Query, StringComparison.Ordinal);
        Assert.Contains("code=oauth-code", form, StringComparison.Ordinal);
        Assert.Contains("code_verifier=verifier-1", form, StringComparison.Ordinal);
        Assert.Equal("gho_access", saved.AccessToken);
        Assert.Equal("ghr_refresh", saved.RefreshToken);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task RefreshUsesStoredRefreshTokenAndClientCredentials()
    {
        StubHandler handler = new("""{"access_token":"gho_new","token_type":"bearer","expires_in":3600,"refresh_token":"ghr_new"}""");
        using HttpClient httpClient = new(handler);
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.OAuth.Tests", Guid.NewGuid().ToString("N"));
        RepoBarPaths paths = RepoBarPaths.ForTestRoot(root);
        FileCredentialStore store = new(paths.DebugAuthDirectory);
        Uri host = new("https://github.com");
        await store.SaveAsync(new CredentialRecord(
            GitHubOAuthLoginService.TokenService,
            GitHubOAuthLoginService.TokenAccount(host),
            new OAuthTokens("gho_old", "ghr_old", new DateTimeOffset(2026, 5, 25, 11, 59, 0, TimeSpan.Zero)).ToSecret()));
        await store.SaveAsync(new CredentialRecord(
            GitHubOAuthLoginService.ClientService,
            GitHubOAuthLoginService.TokenAccount(host),
            new OAuthClientCredentials("client-id", "client-secret").ToSecret()));
        GitHubOAuthLoginService service = new(
            httpClient,
            store,
            new FakeCallbackServer(new OAuthCallback(null, null, null)),
            new FakeBrowserLauncher(),
            nowProvider: () => new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero));

        OAuthTokens? refreshed = await service.RefreshIfNeededAsync(host);
        string form = handler.LastRequestBody!;

        Assert.Equal("gho_new", refreshed?.AccessToken);
        Assert.Contains("grant_type=refresh_token", form, StringComparison.Ordinal);
        Assert.Contains("refresh_token=ghr_old", form, StringComparison.Ordinal);
    }

    private sealed class FakeCallbackServer(OAuthCallback callback) : IOAuthCallbackServer
    {
        public Task<OAuthCallback> WaitForCallbackAsync(int port, string path, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            Task.FromResult(callback);
    }

    private sealed class FakeBrowserLauncher : IBrowserLauncher
    {
        public Uri? LastOpened { get; private set; }

        public void Open(Uri url) => LastOpened = url;
    }
}
