using System.Net;

namespace RepoBar.Core.Api;

public sealed class ProviderApiException : Exception
{
    public ProviderApiException(HttpStatusCode statusCode, string? message, string? responseBody = null)
        : base(message ?? $"Provider request failed with HTTP {(int)statusCode}.")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }
}
