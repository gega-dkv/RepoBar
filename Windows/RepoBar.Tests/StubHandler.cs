using System.Net;

namespace RepoBar.Tests;

internal sealed class StubHandler(
    string body,
    HttpStatusCode statusCode = HttpStatusCode.OK,
    Action<HttpResponseMessage>? configure = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        LastRequestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        HttpResponseMessage response = new(statusCode)
        {
            Content = new StringContent(body),
        };
        configure?.Invoke(response);
        return Task.FromResult(response);
    }
}
