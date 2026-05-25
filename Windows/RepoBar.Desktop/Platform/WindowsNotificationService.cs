using System.Diagnostics;

namespace RepoBar.Desktop.Platform;

public sealed record NotificationRequest(string Title, string Body);

public interface INotificationService
{
    Task ShowAsync(NotificationRequest notification, CancellationToken cancellationToken = default);
}

public sealed class WindowsNativeNotificationService(IWindowsToastGateway gateway) : INotificationService
{
    public Task ShowAsync(NotificationRequest notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        return gateway.ShowAsync(notification.Title, notification.Body, cancellationToken);
    }
}

public interface IWindowsToastGateway
{
    Task ShowAsync(string title, string body, CancellationToken cancellationToken = default);
}

public sealed class PowerShellToastGateway(IProcessStarter processStarter) : IWindowsToastGateway
{
    public Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        processStarter.Start(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                BuildToastCommand(title, body),
            },
        });
        return Task.CompletedTask;
    }

    public static string BuildToastCommand(string title, string body)
    {
        string escapedTitle = EscapePowerShell(title);
        string escapedBody = EscapePowerShell(body);
        return
            "$template=[Windows.UI.Notifications.ToastTemplateType]::ToastText02;"
            + "$xml=[Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template);"
            + "$texts=$xml.GetElementsByTagName('text');"
            + $"$texts.Item(0).AppendChild($xml.CreateTextNode('{escapedTitle}'))>$null;"
            + $"$texts.Item(1).AppendChild($xml.CreateTextNode('{escapedBody}'))>$null;"
            + "$toast=[Windows.UI.Notifications.ToastNotification]::new($xml);"
            + "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('RepoBar').Show($toast);";
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
