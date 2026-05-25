using System.Text.Json;
using RepoBar.Core.Models;

namespace RepoBar.Core.Storage;

public sealed class SettingsStore(RepoBarPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFilePath))
        {
            return new UserSettings();
        }

        await using FileStream stream = File.OpenRead(paths.SettingsFilePath);
        UserSettings? settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return settings ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(paths.SettingsDirectory);

        string tempPath = $"{paths.SettingsFilePath}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(paths.SettingsFilePath))
        {
            File.Replace(tempPath, paths.SettingsFilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, paths.SettingsFilePath);
        }
    }
}
