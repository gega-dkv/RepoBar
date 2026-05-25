using System.Net;
using System.Text.Json;

namespace RepoBar.Core.Api;

public sealed class ProviderJsonRequestRunner(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProviderRequestDiagnostics? LastDiagnostics { get; private set; }

    public async Task<T> SendJsonAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        return await SendJsonAsync<T>(request, cachedBody: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SendJsonAsync<T>(
        HttpRequestMessage request,
        string? cachedBody,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        LastDiagnostics = ProviderRequestDiagnostics.From(request, response, body);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            if (cachedBody is null)
            {
                throw new ProviderApiException(response.StatusCode, "Provider response was not modified and no cached body was supplied.", body);
            }

            return Decode<T>(cachedBody, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderApiException(response.StatusCode, ErrorMessage(body), body);
        }

        return Decode<T>(body, response.StatusCode);
    }

    public async Task<T> SendJsonWithRetryAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        int maxAttempts = 2,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ProviderApiException? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using HttpRequestMessage request = requestFactory();
            try
            {
                return await SendJsonAsync<T>(request, cancellationToken).ConfigureAwait(false);
            }
            catch (ProviderApiException error) when (attempt < maxAttempts && RetryPolicy.ShouldRetry(error.StatusCode))
            {
                lastError = error;
            }
        }

        throw lastError ?? new ProviderApiException(HttpStatusCode.ServiceUnavailable, "Provider request retry failed.");
    }

    public async Task<byte[]> SendBytesAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        LastDiagnostics = ProviderRequestDiagnostics.From(request, response, $"<binary:{body.Length}>");

        if (!response.IsSuccessStatusCode)
        {
            string textBody = System.Text.Encoding.UTF8.GetString(body);
            throw new ProviderApiException(response.StatusCode, ErrorMessage(textBody), textBody);
        }

        return body;
    }

    private static T Decode<T>(string body, HttpStatusCode statusCode)
    {
        T? decoded = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return decoded ?? throw new ProviderApiException(statusCode, "Provider returned an empty or invalid JSON response.", body);
    }

    private static string? ErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out JsonElement message))
            {
                return message.GetString();
            }

            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }
}

public static class RetryPolicy
{
    public static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests
        || statusCode == HttpStatusCode.ServiceUnavailable
        || statusCode == HttpStatusCode.GatewayTimeout
        || (int)statusCode == 202;
}

public sealed record ProviderRequestDiagnostics(
    Uri? Url,
    HttpStatusCode StatusCode,
    string? RateLimitResource,
    int? RateLimitLimit,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetAt,
    TimeSpan? Backoff,
    string SanitizedResponse)
{
    public static ProviderRequestDiagnostics From(HttpRequestMessage request, HttpResponseMessage response, string responseBody)
    {
        int? remaining = HeaderInt(response, "X-RateLimit-Remaining");
        DateTimeOffset? resetAt = HeaderUnixTime(response, "X-RateLimit-Reset");
        TimeSpan? backoff = BackoffPolicy.Calculate(response.StatusCode, remaining, resetAt, DateTimeOffset.UtcNow, response.Headers.RetryAfter?.Delta);

        return new ProviderRequestDiagnostics(
            request.RequestUri,
            response.StatusCode,
            HeaderValue(response, "X-RateLimit-Resource"),
            HeaderInt(response, "X-RateLimit-Limit"),
            remaining,
            resetAt,
            backoff,
            SecretRedactor.Redact(responseBody));
    }

    private static string? HeaderValue(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static int? HeaderInt(HttpResponseMessage response, string name) =>
        int.TryParse(HeaderValue(response, name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
            ? value
            : null;

    private static DateTimeOffset? HeaderUnixTime(HttpResponseMessage response, string name) =>
        long.TryParse(HeaderValue(response, name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}

public static class BackoffPolicy
{
    public static TimeSpan? Calculate(
        HttpStatusCode statusCode,
        int? remaining,
        DateTimeOffset? resetAt,
        DateTimeOffset now,
        TimeSpan? retryAfter = null)
    {
        if (retryAfter is not null)
        {
            return retryAfter;
        }

        if ((statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.TooManyRequests) && remaining == 0 && resetAt is not null)
        {
            TimeSpan wait = resetAt.Value - now;
            return wait <= TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        if ((int)statusCode == 202)
        {
            return TimeSpan.FromSeconds(30);
        }

        return null;
    }
}

public static class SecretRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = System.Text.RegularExpressions.Regex.Replace(
            value,
            "(?i)(authorization|token|access_token|refresh_token|client_secret|private_token)\"?\\s*[:=]\\s*\"?[^\"\\s,}]+",
            "$1:<redacted>");
        redacted = System.Text.RegularExpressions.Regex.Replace(
            redacted,
            "(?i)\\b(?:gh[pousr]_[A-Za-z0-9_]+|glpat-[A-Za-z0-9_\\-]+)\\b",
            "<redacted>");
        return redacted;
    }
}
