using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RepoBar.Core.Storage;

public sealed class PersistentCacheStore(string databasePath)
{
    public string DatabasePath { get; } = databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? ".");
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(
            connection,
            """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS api_responses (
                request_key TEXT PRIMARY KEY,
                url TEXT NOT NULL,
                etag TEXT,
                status_code INTEGER NOT NULL,
                headers_json TEXT NOT NULL,
                body TEXT NOT NULL,
                fetched_at TEXT NOT NULL,
                rate_limit_resource TEXT,
                rate_limit_limit INTEGER,
                rate_limit_remaining INTEGER,
                rate_limit_reset_at TEXT,
                last_error TEXT
            );
            CREATE TABLE IF NOT EXISTS graphql_responses (
                endpoint TEXT NOT NULL,
                operation TEXT NOT NULL,
                request_body_key TEXT NOT NULL,
                body TEXT NOT NULL,
                fetched_at TEXT NOT NULL,
                PRIMARY KEY (endpoint, operation, request_body_key)
            );
            CREATE TABLE IF NOT EXISTS rate_limits (
                resource TEXT PRIMARY KEY,
                limit_value INTEGER,
                remaining_value INTEGER,
                reset_at TEXT,
                last_error TEXT,
                updated_at TEXT NOT NULL
            );
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveApiResponseAsync(CachedApiResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO api_responses (
                request_key, url, etag, status_code, headers_json, body, fetched_at,
                rate_limit_resource, rate_limit_limit, rate_limit_remaining, rate_limit_reset_at, last_error
            )
            VALUES (
                $request_key, $url, $etag, $status_code, $headers_json, $body, $fetched_at,
                $rate_limit_resource, $rate_limit_limit, $rate_limit_remaining, $rate_limit_reset_at, $last_error
            )
            ON CONFLICT(request_key) DO UPDATE SET
                url = excluded.url,
                etag = excluded.etag,
                status_code = excluded.status_code,
                headers_json = excluded.headers_json,
                body = excluded.body,
                fetched_at = excluded.fetched_at,
                rate_limit_resource = excluded.rate_limit_resource,
                rate_limit_limit = excluded.rate_limit_limit,
                rate_limit_remaining = excluded.rate_limit_remaining,
                rate_limit_reset_at = excluded.rate_limit_reset_at,
                last_error = excluded.last_error;
            """;
        AddParameters(command, response);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (response.RateLimit is not null)
        {
            await SaveRateLimitAsync(response.RateLimit, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<CachedApiResponse?> ReadApiResponseAsync(string requestKey, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT request_key, url, etag, status_code, headers_json, body, fetched_at,
                   rate_limit_resource, rate_limit_limit, rate_limit_remaining, rate_limit_reset_at, last_error
            FROM api_responses
            WHERE request_key = $request_key;
            """;
        command.Parameters.AddWithValue("$request_key", requestKey);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadApiResponse(reader) : null;
    }

    public async Task<CacheFirstResult<string>?> ReadApiBodyForStartupAsync(
        string requestKey,
        TimeSpan freshFor,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        CachedApiResponse? response = await ReadApiResponseAsync(requestKey, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return null;
        }

        return new CacheFirstResult<string>(response.Body, now - response.FetchedAt > freshFor, response.FetchedAt);
    }

    public async Task SaveGraphQlResponseAsync(CachedGraphQlResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO graphql_responses (endpoint, operation, request_body_key, body, fetched_at)
            VALUES ($endpoint, $operation, $request_body_key, $body, $fetched_at)
            ON CONFLICT(endpoint, operation, request_body_key) DO UPDATE SET
                body = excluded.body,
                fetched_at = excluded.fetched_at;
            """;
        command.Parameters.AddWithValue("$endpoint", response.Endpoint);
        command.Parameters.AddWithValue("$operation", response.Operation);
        command.Parameters.AddWithValue("$request_body_key", response.RequestBodyKey);
        command.Parameters.AddWithValue("$body", response.Body);
        command.Parameters.AddWithValue("$fetched_at", FormatInstant(response.FetchedAt));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CachedGraphQlResponse?> ReadGraphQlResponseAsync(
        string endpoint,
        string operation,
        string requestBodyKey,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT endpoint, operation, request_body_key, body, fetched_at
            FROM graphql_responses
            WHERE endpoint = $endpoint AND operation = $operation AND request_body_key = $request_body_key;
            """;
        command.Parameters.AddWithValue("$endpoint", endpoint);
        command.Parameters.AddWithValue("$operation", operation);
        command.Parameters.AddWithValue("$request_body_key", requestBodyKey);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CachedGraphQlResponse(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ParseInstant(reader.GetString(4)));
    }

    public async Task SaveRateLimitAsync(RateLimitSnapshot rateLimit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rateLimit);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO rate_limits (resource, limit_value, remaining_value, reset_at, last_error, updated_at)
            VALUES ($resource, $limit_value, $remaining_value, $reset_at, $last_error, $updated_at)
            ON CONFLICT(resource) DO UPDATE SET
                limit_value = excluded.limit_value,
                remaining_value = excluded.remaining_value,
                reset_at = excluded.reset_at,
                last_error = excluded.last_error,
                updated_at = excluded.updated_at;
            """;
        AddParameters(command, rateLimit);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RateLimitSnapshot?> ReadRateLimitAsync(string resource, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT resource, limit_value, remaining_value, reset_at, last_error, updated_at
            FROM rate_limits
            WHERE resource = $resource;
            """;
        command.Parameters.AddWithValue("$resource", resource);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRateLimit(reader) : null;
    }

    public async Task<CacheDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        int apiCount = await CountAsync(connection, "api_responses", cancellationToken).ConfigureAwait(false);
        int graphQlCount = await CountAsync(connection, "graphql_responses", cancellationToken).ConfigureAwait(false);
        int rateLimitCount = await CountAsync(connection, "rate_limits", cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CachedApiResponse> recentResponses = await RecentApiResponsesAsync(connection, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RateLimitSnapshot> rateLimits = await RateLimitsAsync(connection, cancellationToken).ConfigureAwait(false);

        return new CacheDiagnostics(DatabasePath, apiCount, graphQlCount, rateLimitCount, recentResponses, rateLimits);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(
            connection,
            "DELETE FROM api_responses; DELETE FROM graphql_responses; DELETE FROM rate_limits;",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new($"Data Source={DatabasePath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"SELECT COUNT(*) FROM {table};");
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<CachedApiResponse>> RecentApiResponsesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT request_key, url, etag, status_code, headers_json, body, fetched_at,
                   rate_limit_resource, rate_limit_limit, rate_limit_remaining, rate_limit_reset_at, last_error
            FROM api_responses
            ORDER BY fetched_at DESC
            LIMIT 10;
            """;
        List<CachedApiResponse> responses = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            responses.Add(ReadApiResponse(reader));
        }

        return responses;
    }

    private static async Task<IReadOnlyList<RateLimitSnapshot>> RateLimitsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT resource, limit_value, remaining_value, reset_at, last_error, updated_at
            FROM rate_limits
            ORDER BY resource;
            """;
        List<RateLimitSnapshot> rateLimits = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rateLimits.Add(ReadRateLimit(reader));
        }

        return rateLimits;
    }

    private static CachedApiResponse ReadApiResponse(SqliteDataReader reader)
    {
        RateLimitSnapshot? rateLimit = reader.IsDBNull(7)
            ? null
            : new RateLimitSnapshot(
                reader.GetString(7),
                NullableInt(reader, 8),
                NullableInt(reader, 9),
                NullableInstant(reader, 10),
                NullableString(reader, 11),
                ParseInstant(reader.GetString(6)));

        return new CachedApiResponse(
            reader.GetString(0),
            reader.GetString(1),
            NullableString(reader, 2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseInstant(reader.GetString(6)),
            rateLimit,
            NullableString(reader, 11));
    }

    private static RateLimitSnapshot ReadRateLimit(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            NullableInt(reader, 1),
            NullableInt(reader, 2),
            NullableInstant(reader, 3),
            NullableString(reader, 4),
            ParseInstant(reader.GetString(5)));

    private static void AddParameters(SqliteCommand command, CachedApiResponse response)
    {
        command.Parameters.AddWithValue("$request_key", response.RequestKey);
        command.Parameters.AddWithValue("$url", response.Url);
        command.Parameters.AddWithValue("$etag", DbValue(response.ETag));
        command.Parameters.AddWithValue("$status_code", response.StatusCode);
        command.Parameters.AddWithValue("$headers_json", response.HeadersJson);
        command.Parameters.AddWithValue("$body", response.Body);
        command.Parameters.AddWithValue("$fetched_at", FormatInstant(response.FetchedAt));
        command.Parameters.AddWithValue("$rate_limit_resource", DbValue(response.RateLimit?.Resource));
        command.Parameters.AddWithValue("$rate_limit_limit", DbValue(response.RateLimit?.Limit));
        command.Parameters.AddWithValue("$rate_limit_remaining", DbValue(response.RateLimit?.Remaining));
        command.Parameters.AddWithValue("$rate_limit_reset_at", DbValue(response.RateLimit?.ResetAt is null ? null : FormatInstant(response.RateLimit.ResetAt.Value)));
        command.Parameters.AddWithValue("$last_error", DbValue(response.LastError ?? response.RateLimit?.LastError));
    }

    private static void AddParameters(SqliteCommand command, RateLimitSnapshot rateLimit)
    {
        command.Parameters.AddWithValue("$resource", rateLimit.Resource);
        command.Parameters.AddWithValue("$limit_value", DbValue(rateLimit.Limit));
        command.Parameters.AddWithValue("$remaining_value", DbValue(rateLimit.Remaining));
        command.Parameters.AddWithValue("$reset_at", DbValue(rateLimit.ResetAt is null ? null : FormatInstant(rateLimit.ResetAt.Value)));
        command.Parameters.AddWithValue("$last_error", DbValue(rateLimit.LastError));
        command.Parameters.AddWithValue("$updated_at", FormatInstant(rateLimit.UpdatedAt));
    }

    private static object DbValue<T>(T? value) => value is null ? DBNull.Value : value;

    private static string? NullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? NullableInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static DateTimeOffset? NullableInstant(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseInstant(reader.GetString(ordinal));

    private static string FormatInstant(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseInstant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
