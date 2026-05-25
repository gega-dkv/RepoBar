using System.Diagnostics;
using System.Net;
using System.Text.Json;
using RepoBar.Core.Api;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;

namespace RepoBar.Core.Auth;

public sealed record OAuthLoginRequest(
    Uri Host,
    string ClientId,
    string ClientSecret,
    int LoopbackPort,
    string? Scope = null,
    TimeSpan? Timeout = null);

public sealed record OAuthCallback(string? Code, string? State, string? Error);

public sealed record OAuthLoginResult(
    SourceControlProvider Provider,
    Uri Host,
    DateTimeOffset? ExpiresAt,
    CredentialStoreKind Store);

public interface IOAuthLoginService
{
    Task<OAuthLoginResult> LoginAsync(OAuthLoginRequest request, CancellationToken cancellationToken = default);

    Task<OAuthTokens?> RefreshIfNeededAsync(Uri host, bool force = false, CancellationToken cancellationToken = default);
}

public interface IOAuthCallbackServer
{
    Task<OAuthCallback> WaitForCallbackAsync(int port, string path, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public interface IBrowserLauncher
{
    void Open(Uri url);
}

public sealed class GitHubOAuthLoginService(
    HttpClient httpClient,
    ICredentialStore credentialStore,
    IOAuthCallbackServer callbackServer,
    IBrowserLauncher browserLauncher,
    Func<PkceChallenge>? pkceFactory = null,
    Func<string>? stateFactory = null,
    Func<DateTimeOffset>? nowProvider = null)
    : IOAuthLoginService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Func<PkceChallenge> pkceFactory = pkceFactory ?? PkceChallenge.Generate;
    private readonly Func<string> stateFactory = stateFactory ?? (() => Guid.NewGuid().ToString("N"));
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);

    public async Task<OAuthLoginResult> LoginAsync(OAuthLoginRequest request, CancellationToken cancellationToken = default)
    {
        Uri host = NormalizeHost(request.Host);
        Uri redirectUri = new($"http://127.0.0.1:{request.LoopbackPort}/callback");
        PkceChallenge pkce = pkceFactory();
        string state = stateFactory();
        Uri authorizeUrl = BuildAuthorizeUrl(host, request.ClientId, redirectUri, state, pkce.Challenge, request.Scope);

        Task<OAuthCallback> callbackTask = callbackServer.WaitForCallbackAsync(
            request.LoopbackPort,
            "/callback",
            request.Timeout ?? TimeSpan.FromMinutes(3),
            cancellationToken);
        browserLauncher.Open(authorizeUrl);
        OAuthCallback callback = await callbackTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(callback.Error))
        {
            throw new InvalidOperationException($"OAuth authorization failed: {callback.Error}");
        }

        if (callback.State != state)
        {
            throw new InvalidOperationException("OAuth state mismatch.");
        }

        if (string.IsNullOrWhiteSpace(callback.Code))
        {
            throw new InvalidOperationException("OAuth callback did not include an authorization code.");
        }

        OAuthTokens tokens = await ExchangeAuthorizationCodeAsync(
            host,
            request.ClientId,
            request.ClientSecret,
            callback.Code,
            redirectUri,
            pkce.Verifier,
            cancellationToken).ConfigureAwait(false);
        await SaveAsync(host, tokens, new OAuthClientCredentials(request.ClientId, request.ClientSecret), cancellationToken).ConfigureAwait(false);
        return new OAuthLoginResult(SourceControlProvider.GitHub, host, tokens.ExpiresAt, credentialStore.Kind);
    }

    public async Task<OAuthTokens?> RefreshIfNeededAsync(Uri host, bool force = false, CancellationToken cancellationToken = default)
    {
        Uri normalized = NormalizeHost(host);
        CredentialRecord? credential = await credentialStore.ReadAsync(TokenService, TokenAccount(normalized), cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return null;
        }

        OAuthTokens tokens = OAuthTokens.FromSecret(credential.Secret);
        if (!tokens.NeedsRefresh(nowProvider(), force))
        {
            return tokens;
        }

        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return tokens;
        }

        CredentialRecord? clientCredential = await credentialStore.ReadAsync(ClientService, TokenAccount(normalized), cancellationToken).ConfigureAwait(false);
        if (clientCredential is null)
        {
            return tokens;
        }

        OAuthClientCredentials client = OAuthClientCredentials.FromSecret(clientCredential.Secret);
        OAuthTokens refreshed = await RefreshAsync(normalized, client, tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
        await SaveAsync(normalized, refreshed, client, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    public static Uri NormalizeHost(Uri host)
    {
        UriBuilder builder = new(host);
        if (string.IsNullOrWhiteSpace(builder.Scheme))
        {
            builder.Scheme = Uri.UriSchemeHttps;
        }

        if (!builder.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(builder.Host))
        {
            throw new ArgumentException("GitHub OAuth hosts must be HTTPS URLs.", nameof(host));
        }

        builder.Path = string.Empty;
        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        return builder.Uri;
    }

    public static Uri BuildAuthorizeUrl(Uri host, string clientId, Uri redirectUri, string state, string codeChallenge, string? scope)
    {
        UriBuilder builder = new(new Uri(host, "/login/oauth/authorize"));
        List<string> query =
        [
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri.AbsoluteUri)}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
        ];
        if (!string.IsNullOrWhiteSpace(scope))
        {
            query.Add($"scope={Uri.EscapeDataString(scope)}");
        }

        builder.Query = string.Join("&", query);
        return builder.Uri;
    }

    public static string TokenService => "provider-token";

    public static string ClientService => "oauth-client";

    public static string TokenAccount(Uri host) => $"{SourceControlProvider.GitHub}:{host.Host}:oauth";

    private async Task<OAuthTokens> ExchangeAuthorizationCodeAsync(
        Uri host,
        string clientId,
        string clientSecret,
        string code,
        Uri redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> form = new(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        };
        return await SendTokenRequestAsync(host, form, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthTokens> RefreshAsync(
        Uri host,
        OAuthClientCredentials client,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> form = new(StringComparer.Ordinal)
        {
            ["client_id"] = client.ClientId,
            ["client_secret"] = client.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        return await SendTokenRequestAsync(host, form, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthTokens> SendTokenRequestAsync(Uri host, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(host, "/login/oauth/access_token"))
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.ParseAdd("application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OAuth token exchange failed with HTTP {(int)response.StatusCode}: {SecretRedactor.Redact(body)}");
        }

        OAuthTokenResponse decoded = JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions)
                                     ?? throw new InvalidOperationException("OAuth token response was empty.");
        return new OAuthTokens(
            decoded.AccessToken,
            decoded.RefreshToken ?? string.Empty,
            nowProvider().AddSeconds(decoded.ExpiresIn ?? 3600));
    }

    private async Task SaveAsync(Uri host, OAuthTokens tokens, OAuthClientCredentials client, CancellationToken cancellationToken)
    {
        await credentialStore.SaveAsync(new CredentialRecord(TokenService, TokenAccount(host), tokens.ToSecret()), cancellationToken).ConfigureAwait(false);
        await credentialStore.SaveAsync(new CredentialRecord(ClientService, TokenAccount(host), client.ToSecret()), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class HttpListenerOAuthCallbackServer : IOAuthCallbackServer
{
    public async Task<OAuthCallback> WaitForCallbackAsync(int port, string path, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using HttpListener listener = new();
        string prefix = $"http://127.0.0.1:{port}{path.TrimEnd('/')}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeoutSource.Token).ConfigureAwait(false);
        string response = "<html><body>RepoBar authentication complete. You can close this window.</body></html>";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(response);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, timeoutSource.Token).ConfigureAwait(false);
        context.Response.Close();
        return new OAuthCallback(
            context.Request.QueryString["code"],
            context.Request.QueryString["state"],
            context.Request.QueryString["error"]);
    }
}

public sealed class SystemBrowserLauncher : IBrowserLauncher
{
    public void Open(Uri url)
    {
        ProcessStartInfo start = new(url.AbsoluteUri)
        {
            UseShellExecute = true,
        };
        Process.Start(start);
    }
}
