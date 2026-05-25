using System.Net;

namespace RepoBar.Tests;

internal sealed class SequenceHandler(params (HttpStatusCode StatusCode, string Body)[] responses) : HttpMessageHandler
{
    private int index;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (HttpStatusCode statusCode, string body) = responses[Math.Min(index, responses.Length - 1)];
        index++;
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
