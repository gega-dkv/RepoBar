using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RepoBar.Desktop.Platform;

public interface ILaunchAtLoginService
{
    bool IsEnabled();

    void SetEnabled(bool enabled, string executablePath, string arguments = "--background");
}

public sealed class WindowsLaunchAtLoginService(IStartupRegistry registry) : ILaunchAtLoginService
{
    public const string AppName = "RepoBar";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled() => registry.GetValue(RunKeyPath, AppName) is not null;

    public void SetEnabled(bool enabled, string executablePath, string arguments = "--background")
    {
        if (enabled)
        {
            registry.SetValue(RunKeyPath, AppName, BuildCommandLine(executablePath, arguments));
        }
        else
        {
            registry.DeleteValue(RunKeyPath, AppName);
        }
    }

    public static string BuildCommandLine(string executablePath, string arguments)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        string command = $"\"{executablePath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        return string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";
    }
}

public interface IStartupRegistry
{
    string? GetValue(string keyPath, string name);

    void SetValue(string keyPath, string name, string value);

    void DeleteValue(string keyPath, string name);
}

public sealed class CurrentUserRunRegistry : IStartupRegistry
{
    [SupportedOSPlatform("windows")]
    public string? GetValue(string keyPath, string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    [SupportedOSPlatform("windows")]
    public void SetValue(string keyPath, string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    public void DeleteValue(string keyPath, string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
