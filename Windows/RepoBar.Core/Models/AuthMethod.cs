namespace RepoBar.Core.Models;

public enum AuthMethod
{
    OAuth,
    Pat,
    ApiToken,
}

public static class AuthMethodExtensions
{
    public static string Label(this AuthMethod method) =>
        method switch
        {
            AuthMethod.OAuth => "OAuth",
            AuthMethod.Pat => "Personal Access Token",
            AuthMethod.ApiToken => "API Token",
            _ => method.ToString(),
        };
}
