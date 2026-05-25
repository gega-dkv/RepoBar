using System.Security.Cryptography;
using System.Text;

namespace RepoBar.Core.Auth;

public sealed record PkceChallenge(string Verifier, string Challenge)
{
    public static PkceChallenge Generate()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string verifier = Base64Url(bytes);
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return new PkceChallenge(verifier, Base64Url(digest));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
