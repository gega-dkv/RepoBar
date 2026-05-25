using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoBar.Core.Auth;

public sealed record OAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset? ExpiresAt)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool NeedsRefresh(DateTimeOffset now, bool force = false) =>
        !string.IsNullOrWhiteSpace(RefreshToken)
        && (force || ExpiresAt is null || ExpiresAt <= now.AddMinutes(1));

    public string ToSecret() => JsonSerializer.Serialize(this, JsonOptions);

    public static OAuthTokens FromSecret(string secret) =>
        JsonSerializer.Deserialize<OAuthTokens>(secret, JsonOptions)
        ?? throw new InvalidOperationException("OAuth credential payload is empty.");
}

public sealed record OAuthClientCredentials(string ClientId, string ClientSecret)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToSecret() => JsonSerializer.Serialize(this, JsonOptions);

    public static OAuthClientCredentials FromSecret(string secret) =>
        JsonSerializer.Deserialize<OAuthClientCredentials>(secret, JsonOptions)
        ?? throw new InvalidOperationException("OAuth client credential payload is empty.");
}

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);
