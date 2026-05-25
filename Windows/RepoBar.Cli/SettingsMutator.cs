using RepoBar.Core.Models;
using RepoBar.Core.Support;

namespace RepoBar.Cli;

public static class SettingsMutator
{
    public static UserSettings Set(UserSettings settings, string key, string value) =>
        key switch
        {
            "refresh-interval" => settings with { RefreshInterval = ParseDuration(value) },
            "repo-limit" => settings with { RepoList = settings.RepoList with { DisplayLimit = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) } },
            "show-forks" => settings with { RepoList = settings.RepoList with { ShowForks = ParseBool(value) } },
            "show-archived" => settings with { RepoList = settings.RepoList with { ShowArchived = ParseBool(value) } },
            "menu-sort" => settings with { RepoList = settings.RepoList with { MenuSortKey = ParseSort(value) } },
            "show-contribution-header" => settings with { Appearance = settings.Appearance with { ShowContributionHeader = ParseBool(value) } },
            "show-rate-limit-meter" => settings with { Appearance = settings.Appearance with { ShowRateLimitMeterInMenuBar = ParseBool(value) } },
            "card-density" => settings with { Appearance = settings.Appearance with { CardDensity = ParseCardDensity(value) } },
            "accent-tone" => settings with { Appearance = settings.Appearance with { AccentTone = ParseAccentTone(value) } },
            "activity-scope" => settings with { Appearance = settings.Appearance with { ActivityScope = ParseActivityScope(value) } },
            "heatmap-display" => settings with { Heatmap = settings.Heatmap with { Display = ParseHeatmapDisplay(value) } },
            "heatmap-span" => settings with { Heatmap = settings.Heatmap with { Span = ParseHeatmapSpan(value) } },
            "local-root" => settings with { LocalProjects = settings.LocalProjects with { RootPath = value } },
            "local-auto-sync" => settings with { LocalProjects = settings.LocalProjects with { AutoSyncEnabled = ParseBool(value) } },
            "local-fetch-interval" => settings with { LocalProjects = settings.LocalProjects with { FetchInterval = ParseDuration(value) } },
            "local-worktree-folder" => settings with { LocalProjects = settings.LocalProjects with { WorktreeFolderName = value } },
            "local-preferred-terminal" => settings with { LocalProjects = settings.LocalProjects with { PreferredTerminal = value } },
            "local-show-dirty-files" => settings with { LocalProjects = settings.LocalProjects with { ShowDirtyFilesInMenu = ParseBool(value) } },
            "launch-at-login" => settings with { LaunchAtLogin = ParseBool(value) },
            _ => throw new CliUsageException($"Unsupported settings key '{key}'."),
        };

    public static string Plain(UserSettings settings) =>
        string.Join(
            Environment.NewLine,
            [
                $"Provider: {settings.SelectedProvider.Label()}",
                $"Refresh interval: {settings.RefreshInterval}",
                $"Repository limit: {settings.RepoList.DisplayLimit}",
                $"Show forks: {settings.RepoList.ShowForks}",
                $"Show archived: {settings.RepoList.ShowArchived}",
                $"Card density: {settings.Appearance.CardDensity}",
                $"Accent tone: {settings.Appearance.AccentTone}",
                $"Activity scope: {settings.Appearance.ActivityScope}",
                $"Heatmap display: {settings.Heatmap.Display}",
                $"Heatmap span: {settings.Heatmap.Span}",
                $"Launch at login: {settings.LaunchAtLogin}",
                $"Local root: {settings.LocalProjects.RootPath ?? ""}",
                $"Diagnostics: {settings.DiagnosticsEnabled}",
            ]);

    private static TimeSpan ParseDuration(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.EndsWith("m", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed[..^1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return TimeSpan.FromMinutes(int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out bool parsed)
            ? parsed
            : value switch
            {
                "1" or "yes" or "on" => true,
                "0" or "no" or "off" => false,
                _ => throw new CliUsageException($"Expected boolean value, got '{value}'."),
            };

    private static RepositorySortKey ParseSort(string value) =>
        value switch
        {
            "activity" => RepositorySortKey.Activity,
            "issues" => RepositorySortKey.Issues,
            "prs" => RepositorySortKey.PullRequests,
            "stars" => RepositorySortKey.Stars,
            "repo" => RepositorySortKey.Name,
            "event" => RepositorySortKey.Event,
            _ => throw new CliUsageException($"Unsupported sort key '{value}'."),
        };

    private static CardDensity ParseCardDensity(string value) =>
        value switch
        {
            "comfortable" => CardDensity.Comfortable,
            "compact" => CardDensity.Compact,
            _ => throw new CliUsageException($"Unsupported card density '{value}'."),
        };

    private static AccentTone ParseAccentTone(string value) =>
        value switch
        {
            "system" => AccentTone.System,
            "github-green" => AccentTone.GitHubGreen,
            _ => throw new CliUsageException($"Unsupported accent tone '{value}'."),
        };

    private static GlobalActivityScope ParseActivityScope(string value) =>
        value switch
        {
            "all" => GlobalActivityScope.AllActivity,
            "my" => GlobalActivityScope.MyActivity,
            _ => throw new CliUsageException($"Unsupported activity scope '{value}'."),
        };

    private static HeatmapDisplay ParseHeatmapDisplay(string value) =>
        value switch
        {
            "inline" => HeatmapDisplay.Inline,
            "submenu" => HeatmapDisplay.Submenu,
            _ => throw new CliUsageException($"Unsupported heatmap display '{value}'."),
        };

    private static HeatmapSpan ParseHeatmapSpan(string value) =>
        value switch
        {
            "1m" => HeatmapSpan.OneMonth,
            "3m" => HeatmapSpan.ThreeMonths,
            "6m" => HeatmapSpan.SixMonths,
            "12m" => HeatmapSpan.TwelveMonths,
            _ => throw new CliUsageException($"Unsupported heatmap span '{value}'."),
        };
}
