using System.Collections.Specialized;
using System.Net;

namespace RepoBar.Desktop.Platform;

public sealed record OAuthCallbackResult(string? Code, string? State, string? Error);

public interface IOAuthLoopbackServer
{
    Task<OAuthCallbackResult> WaitForCallbackAsync(int port, string path, CancellationToken cancellationToken = default);
}

public sealed class HttpListenerOAuthLoopbackServer : IOAuthLoopbackServer
{
    public async Task<OAuthCallbackResult> WaitForCallbackAsync(
        int port,
        string path,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = path.Length > 0 && path[0] == '/' ? path : $"/{path}";
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}{normalizedPath.TrimEnd('/')}/");
        listener.Start();

        using CancellationTokenRegistration registration = cancellationToken.Register(listener.Stop);
        HttpListenerContext context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        NameValueCollection query = context.Request.QueryString;
        byte[] response = System.Text.Encoding.UTF8.GetBytes("RepoBar sign-in complete. You can close this tab.");
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.ContentLength64 = response.Length;
        await context.Response.OutputStream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        context.Response.Close();

        return new OAuthCallbackResult(query["code"], query["state"], query["error"]);
    }
}
