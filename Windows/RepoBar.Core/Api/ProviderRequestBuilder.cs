using System.Net.Http.Headers;
using System.Text;
using RepoBar.Core.Models;

namespace RepoBar.Core.Api;

public static class ProviderRequestBuilder
{
    private static readonly ProductInfoHeaderValue UserAgent = new("RepoBar", "1.0");

    public static HttpRequestMessage Get(
        SourceControlProvider provider,
        Uri apiHost,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        ProviderCredential? credential = null,
        string? etag = null)
    {
        Uri uri = BuildUri(apiHost, path, query);
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(UserAgent);

        if (provider == SourceControlProvider.GitHub)
        {
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        }
        else
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        credential?.Apply(request);
        return request;
    }

    public static Uri BuildUri(
        Uri apiHost,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null)
    {
        UriBuilder builder = new(new Uri(EnsureTrailingSlash(apiHost), path.TrimStart('/')));
        List<string> pairs = [];

        foreach (KeyValuePair<string, string?> item in query ?? [])
        {
            if (item.Value is null)
            {
                continue;
            }

            pairs.Add($"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}");
        }

        builder.Query = string.Join('&', pairs);
        return builder.Uri;
    }

    public static HttpRequestMessage PostJson(
        SourceControlProvider provider,
        Uri apiHost,
        string path,
        string json,
        ProviderCredential? credential = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, BuildUri(apiHost, path));
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (provider == SourceControlProvider.GitHub)
        {
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        }

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        credential?.Apply(request);
        return request;
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri[^1] == '/'
            ? uri
            : new Uri($"{uri.AbsoluteUri}/");
}
