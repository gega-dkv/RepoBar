using System.Diagnostics;
using RepoBar.Desktop.Platform;
using Xunit;

namespace RepoBar.Tests;

public sealed class WindowsAdapterTests
{
    [Fact]
    public void TrayIconAssetLoaderRequiresIcoFiles()
    {
        TrayIconAssetLoader.ValidateIconPath("Assets/RepoBar.ico");

        Assert.Throws<ArgumentException>(() => TrayIconAssetLoader.ValidateIconPath("Assets/RepoBar.png"));
    }

    [Fact]
    public void LaunchAtLoginWritesAndDeletesRunKey()
    {
        FakeStartupRegistry registry = new();
        WindowsLaunchAtLoginService service = new(registry);

        service.SetEnabled(true, @"C:\Program Files\RepoBar\RepoBar.exe");
        bool enabled = service.IsEnabled();
        service.SetEnabled(false, @"C:\Program Files\RepoBar\RepoBar.exe");

        Assert.True(enabled);
        Assert.Null(registry.GetValue(WindowsLaunchAtLoginService.RunKeyPath, WindowsLaunchAtLoginService.AppName));
    }

    [Fact]
    public void LaunchAtLoginQuotesExecutablePath()
    {
        string command = WindowsLaunchAtLoginService.BuildCommandLine(@"C:\Program Files\RepoBar\RepoBar.exe", "--background");

        Assert.Equal(@"""C:\Program Files\RepoBar\RepoBar.exe"" --background", command);
    }

    [Fact]
    public void ShellLauncherUsesOperatingSystemShellForUrlsAndPaths()
    {
        CapturingProcessStarter starter = new();
        WindowsShellLauncher launcher = new(starter);

        launcher.OpenUrl(new Uri("https://github.com/openai/codex"));
        launcher.OpenPath(@"C:\Projects\RepoBar");

        Assert.Equal("https://github.com/openai/codex", starter.Started[0].FileName);
        Assert.True(starter.Started[0].UseShellExecute);
        Assert.Equal(@"C:\Projects\RepoBar", starter.Started[1].FileName);
        Assert.True(starter.Started[1].UseShellExecute);
    }

    [Fact]
    public async Task NativeNotificationGatewayBuildsEscapedToastCommand()
    {
        string command = PowerShellToastGateway.BuildToastCommand("Sync complete", "Owner's RepoBar updated");

        Assert.Contains("ToastNotificationManager", command, StringComparison.Ordinal);
        Assert.Contains("Owner''s RepoBar updated", command, StringComparison.Ordinal);

        CapturingToastGateway gateway = new();
        WindowsNativeNotificationService service = new(gateway);
        await service.ShowAsync(new NotificationRequest("Sync complete", "RepoBar updated"));

        Assert.Equal("Sync complete", gateway.Title);
        Assert.Equal("RepoBar updated", gateway.Body);
    }

    [Fact]
    public async Task OAuthLoopbackCapturesCodeStateAndError()
    {
        int port = FreeTcpPort();
        HttpListenerOAuthLoopbackServer server = new();
        Task<OAuthCallbackResult> callback = server.WaitForCallbackAsync(port, "/callback");
        using HttpClient client = new();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"http://127.0.0.1:{port}/callback/?code=abc&state=xyz"));
        OAuthCallbackResult result = await callback;

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("abc", result.Code);
        Assert.Equal("xyz", result.State);
        Assert.Null(result.Error);
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string keyPath, string name) => values.TryGetValue($"{keyPath}\\{name}", out string? value) ? value : null;

        public void SetValue(string keyPath, string name, string value) => values[$"{keyPath}\\{name}"] = value;

        public void DeleteValue(string keyPath, string name) => values.Remove($"{keyPath}\\{name}");
    }

    private sealed class CapturingProcessStarter : IProcessStarter
    {
        public List<ProcessStartInfo> Started { get; } = [];

        public void Start(ProcessStartInfo startInfo) => Started.Add(startInfo);
    }

    private sealed class CapturingToastGateway : IWindowsToastGateway
    {
        public string? Title { get; private set; }

        public string? Body { get; private set; }

        public Task ShowAsync(string title, string body, CancellationToken cancellationToken = default)
        {
            Title = title;
            Body = body;
            return Task.CompletedTask;
        }
    }

    private static int FreeTcpPort()
    {
        using System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
