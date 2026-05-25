using System.Net.Http.Headers;
using System.Text;
using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public sealed record ProviderCredential(
    SourceControlProvider Provider,
    Uri Host,
    AuthMethod Kind,
    CredentialHeaderStyle HeaderStyle,
    string Token,
    string? Username = null,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAt = null)
{
    public void Apply(HttpRequestMessage request)
    {
        switch (HeaderStyle)
        {
            case CredentialHeaderStyle.AuthorizationBearer:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
                break;
            case CredentialHeaderStyle.PrivateToken:
                request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", Token);
                break;
            case CredentialHeaderStyle.Basic:
                string raw = $"{Username ?? string.Empty}:{Token}";
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                break;
            default:
                throw new InvalidOperationException("Unknown credential header style.");
        }
    }
}

public enum CredentialHeaderStyle
{
    AuthorizationBearer,
    PrivateToken,
    Basic,
}
