namespace RepoBar.Core.Storage;

public sealed record CachedApiResponse(
    string RequestKey,
    string Url,
    string? ETag,
    int StatusCode,
    string HeadersJson,
    string Body,
    DateTimeOffset FetchedAt,
    RateLimitSnapshot? RateLimit = null,
    string? LastError = null);

public sealed record CachedGraphQlResponse(
    string Endpoint,
    string Operation,
    string RequestBodyKey,
    string Body,
    DateTimeOffset FetchedAt);

public sealed record RateLimitSnapshot(
    string Resource,
    int? Limit,
    int? Remaining,
    DateTimeOffset? ResetAt,
    string? LastError,
    DateTimeOffset UpdatedAt);

public sealed record CacheDiagnostics(
    string DatabasePath,
    int ApiResponseCount,
    int GraphQlResponseCount,
    int RateLimitCount,
    IReadOnlyList<CachedApiResponse> RecentApiResponses,
    IReadOnlyList<RateLimitSnapshot> RateLimits);

public sealed record CacheFirstResult<T>(T Value, bool IsStale, DateTimeOffset FetchedAt);
