using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RepoBar.Core.Models;

namespace RepoBar.Core.Storage;

public sealed record ArchiveValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<ArchiveTableManifest> Tables);

public sealed record ArchiveTableManifest(string Name, IReadOnlyList<string> Files, IReadOnlyList<string> Columns, long Rows);

public sealed record ArchiveImportResult(
    string SourceId,
    string DatabasePath,
    DateTimeOffset ImportedAt,
    int TableCount,
    long RowCount);

public sealed record ArchiveStatus(
    string SourceId,
    string Name,
    string DatabasePath,
    bool Enabled,
    bool HasImportedDatabase,
    DateTimeOffset? LastImportAt,
    long ImportedRows);

public sealed class GitHubArchiveStore(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public static async Task<ArchiveValidationResult> ValidateAsync(
        GitHubArchiveSource source,
        CancellationToken cancellationToken = default)
    {
        if (source.Format != GitHubArchiveFormat.DiscrawlSnapshot)
        {
            return new ArchiveValidationResult(false, [$"Unsupported archive format '{source.Format}'."], []);
        }

        if (string.IsNullOrWhiteSpace(source.LocalRepositoryPath))
        {
            return new ArchiveValidationResult(false, ["Archive source does not have a local repository path."], []);
        }

        string manifestPath = Path.Combine(source.LocalRepositoryPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new ArchiveValidationResult(false, [$"Archive manifest is missing at {manifestPath}."], []);
        }

        await using FileStream stream = File.OpenRead(manifestPath);
        DiscrawlManifest? manifest = await JsonSerializer.DeserializeAsync<DiscrawlManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (manifest?.Tables is null || manifest.Tables.Count == 0)
        {
            return new ArchiveValidationResult(false, ["Archive manifest does not contain table entries."], []);
        }

        List<string> errors = [];
        List<ArchiveTableManifest> tables = [];
        foreach (DiscrawlTableManifest table in manifest.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.Name))
            {
                errors.Add("Archive manifest contains a table without a name.");
                continue;
            }

            if (table.Files.Count == 0)
            {
                errors.Add($"Archive table '{table.Name}' does not list files.");
                continue;
            }

            foreach (string file in table.Files)
            {
                string dataPath = Path.Combine(source.LocalRepositoryPath, "tables", table.Name, file);
                if (!File.Exists(dataPath))
                {
                    errors.Add($"Archive table file is missing: {dataPath}.");
                }
            }

            tables.Add(new ArchiveTableManifest(table.Name, table.Files, table.Columns, table.Rows));
        }

        return new ArchiveValidationResult(errors.Count == 0, errors, tables);
    }

    public async Task<ArchiveImportResult> ImportAsync(
        GitHubArchiveSource source,
        CancellationToken cancellationToken = default)
    {
        ArchiveValidationResult validation = await ValidateAsync(source, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(source.ImportedDatabasePath) ?? ".");
        await using SqliteConnection connection = new($"Data Source={source.ImportedDatabasePath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await InitializeArchiveDatabaseAsync(connection, cancellationToken).ConfigureAwait(false);

        long rowCount = 0;
        foreach (ArchiveTableManifest table in validation.Tables)
        {
            foreach (string file in table.Files)
            {
                string dataPath = Path.Combine(source.LocalRepositoryPath!, "tables", table.Name, file);
                rowCount += await CountJsonLinesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            }
        }

        DateTimeOffset importedAt = timeProvider.GetUtcNow();
        await RecordImportAsync(connection, source, importedAt, validation.Tables.Count, rowCount, cancellationToken).ConfigureAwait(false);
        return new ArchiveImportResult(source.Id, source.ImportedDatabasePath, importedAt, validation.Tables.Count, rowCount);
    }

    public static async Task<ArchiveStatus> GetStatusAsync(GitHubArchiveSource source, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(source.ImportedDatabasePath))
        {
            return new ArchiveStatus(source.Id, source.Name, source.ImportedDatabasePath, source.Enabled, false, null, 0);
        }

        await using SqliteConnection connection = new($"Data Source={source.ImportedDatabasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT imported_at, row_count
            FROM repo_bar_archive_imports
            WHERE source_id = $source_id
            ORDER BY imported_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$source_id", source.Id);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ArchiveStatus(source.Id, source.Name, source.ImportedDatabasePath, source.Enabled, true, null, 0);
        }

        return new ArchiveStatus(
            source.Id,
            source.Name,
            source.ImportedDatabasePath,
            source.Enabled,
            true,
            DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.GetInt64(1));
    }

    public static async Task<string?> ReadFirstNonEmptyArchiveValueAsync(
        IEnumerable<GitHubArchiveSource> sources,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        foreach (GitHubArchiveSource source in sources.Where(source => source.Enabled))
        {
            if (!File.Exists(source.ImportedDatabasePath))
            {
                continue;
            }

            await using SqliteConnection connection = new($"Data Source={source.ImportedDatabasePath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {QuoteIdentifier(columnName)} FROM {QuoteIdentifier(tableName)} WHERE {QuoteIdentifier(columnName)} IS NOT NULL AND {QuoteIdentifier(columnName)} <> '' LIMIT 1;";
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static async Task InitializeArchiveDatabaseAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA user_version = 1;
            CREATE TABLE IF NOT EXISTS repo_bar_archive_imports (
                source_id TEXT NOT NULL,
                source_name TEXT NOT NULL,
                imported_at TEXT NOT NULL,
                table_count INTEGER NOT NULL,
                row_count INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RecordImportAsync(
        SqliteConnection connection,
        GitHubArchiveSource source,
        DateTimeOffset importedAt,
        int tableCount,
        long rowCount,
        CancellationToken cancellationToken)
    {
        string instant = importedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO repo_bar_archive_imports (source_id, source_name, imported_at, table_count, row_count)
            VALUES ($source_id, $source_name, $imported_at, $table_count, $row_count);
            INSERT INTO sync_state (key, value)
            VALUES ('repobar:last_import', $imported_at)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$source_id", source.Id);
        command.Parameters.AddWithValue("$source_name", source.Name);
        command.Parameters.AddWithValue("$imported_at", instant);
        command.Parameters.AddWithValue("$table_count", tableCount);
        command.Parameters.AddWithValue("$row_count", rowCount);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CountJsonLinesAsync(string path, CancellationToken cancellationToken)
    {
        await using Stream fileStream = File.OpenRead(path);
        await using Stream dataStream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : Stream.Null;
        Stream stream = dataStream == Stream.Null ? fileStream : dataStream;
        using StreamReader reader = new(stream, leaveOpen: true);

        long count = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                count++;
            }
        }

        return count;
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("SQLite identifier contains unsupported characters.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record DiscrawlManifest(IReadOnlyList<DiscrawlTableManifest> Tables);

    private sealed record DiscrawlTableManifest(
        string Name,
        IReadOnlyList<string> Files,
        IReadOnlyList<string> Columns,
        long Rows);
}
