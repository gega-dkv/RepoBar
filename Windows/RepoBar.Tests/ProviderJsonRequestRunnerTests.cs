using System.Net;
using RepoBar.Core.Api;
using Xunit;

namespace RepoBar.Tests;

public sealed class ProviderJsonRequestRunnerTests
{
    [Fact]
    public async Task DecodesSuccessfulJsonResponses()
    {
        using HttpClient httpClient = new(new StubHandler("""{"value":"ok"}"""));
        ProviderJsonRequestRunner runner = new(httpClient);

        SampleDto result = await runner.SendJsonAsync<SampleDto>(new HttpRequestMessage(HttpMethod.Get, "https://example.com"));

        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task ThrowsProviderApiExceptionWithMessage()
    {
        using HttpClient httpClient = new(new StubHandler("""{"message":"rate limited"}""", HttpStatusCode.Forbidden));
        ProviderJsonRequestRunner runner = new(httpClient);

        ProviderApiException error = await Assert.ThrowsAsync<ProviderApiException>(
            () => runner.SendJsonAsync<SampleDto>(new HttpRequestMessage(HttpMethod.Get, "https://example.com")));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal("rate limited", error.Message);
    }

    [Fact]
    public async Task UsesCachedBodyForNotModifiedResponses()
    {
        using HttpClient httpClient = new(new StubHandler("", HttpStatusCode.NotModified));
        ProviderJsonRequestRunner runner = new(httpClient);

        SampleDto result = await runner.SendJsonAsync<SampleDto>(
            new HttpRequestMessage(HttpMethod.Get, "https://example.com"),
            cachedBody: """{"value":"cached"}""");

        Assert.Equal("cached", result.Value);
    }

    [Fact]
    public async Task CapturesDiagnosticsAndRedactsSecrets()
    {
        StubHandler handler = new(
            """{"message":"bad","access_token":"secret","note":"ghp_rawSecret123 glpat-rawSecret456"}""",
            HttpStatusCode.Forbidden,
            configure: response =>
            {
                response.Headers.TryAddWithoutValidation("X-RateLimit-Resource", "core");
                response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "5000");
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
                response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            });
        using HttpClient httpClient = new(handler);
        ProviderJsonRequestRunner runner = new(httpClient);

        await Assert.ThrowsAsync<ProviderApiException>(
            () => runner.SendJsonAsync<SampleDto>(new HttpRequestMessage(HttpMethod.Get, "https://example.com")));

        Assert.NotNull(runner.LastDiagnostics);
        Assert.Equal("core", runner.LastDiagnostics.RateLimitResource);
        Assert.Equal(0, runner.LastDiagnostics.RateLimitRemaining);
        Assert.DoesNotContain("secret", runner.LastDiagnostics.SanitizedResponse, StringComparison.Ordinal);
        Assert.DoesNotContain("rawSecret", runner.LastDiagnostics.SanitizedResponse, StringComparison.Ordinal);
        Assert.Contains("<redacted>", runner.LastDiagnostics.SanitizedResponse, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretRedactorCoversStructuredAndRawProviderTokens()
    {
        string redacted = SecretRedactor.Redact(
            """authorization=Bearer ghp_secret123 refresh_token:"ghr_refresh123" private_token=glpat-secret456 client_secret=s3cr3t""");

        Assert.DoesNotContain("ghp_secret123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("ghr_refresh123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("glpat-secret456", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetriesTransientProviderResponses()
    {
        using HttpClient httpClient = new(new SequenceHandler(
            (HttpStatusCode.ServiceUnavailable, """{"message":"try later"}"""),
            (HttpStatusCode.OK, """{"value":"ok"}""")));
        ProviderJsonRequestRunner runner = new(httpClient);

        SampleDto result = await runner.SendJsonWithRetryAsync<SampleDto>(
            () => new HttpRequestMessage(HttpMethod.Get, "https://example.com"),
            maxAttempts: 2);

        Assert.Equal("ok", result.Value);
    }

    private sealed record SampleDto(string Value);
}
