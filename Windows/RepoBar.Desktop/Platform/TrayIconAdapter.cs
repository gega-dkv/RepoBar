using System.Windows.Input;
using Avalonia.Controls;

namespace RepoBar.Desktop.Platform;

public sealed record TrayMenuItemSpec(string Header, ICommand Command);

public sealed record TrayIconSpec(string IconPath, string ToolTipText, ICommand LeftClickCommand, IReadOnlyList<TrayMenuItemSpec> RightClickItems);

public interface ITrayIconAdapter : IDisposable
{
    bool IsInitialized { get; }

    void Initialize(TrayIconSpec spec);
}

public sealed class AvaloniaTrayIconAdapter : ITrayIconAdapter
{
    private TrayIcon? trayIcon;

    public bool IsInitialized => trayIcon is not null;

    public void Initialize(TrayIconSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        TrayIconAssetLoader.ValidateIconPath(spec.IconPath);

        NativeMenu menu = new();
        foreach (TrayMenuItemSpec item in spec.RightClickItems)
        {
            menu.Items.Add(new NativeMenuItem(item.Header)
            {
                Command = item.Command,
            });
        }

        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(spec.IconPath),
            ToolTipText = spec.ToolTipText,
            Command = spec.LeftClickCommand,
            Menu = menu,
        };
    }

    public void Dispose()
    {
        trayIcon?.Dispose();
        trayIcon = null;
    }
}

public static class TrayIconAssetLoader
{
    public static void ValidateIconPath(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            throw new ArgumentException("Tray icon path is required.", nameof(iconPath));
        }

        if (!iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Windows tray icon assets must use .ico format.", nameof(iconPath));
        }
    }
}
