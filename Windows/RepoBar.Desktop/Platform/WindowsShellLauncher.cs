using System.Diagnostics;

namespace RepoBar.Desktop.Platform;

public interface IShellLauncher
{
    void OpenUrl(Uri url);

    void OpenPath(string path);
}

public sealed class WindowsShellLauncher(IProcessStarter processStarter) : IShellLauncher
{
    public void OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only http and https URLs can be opened in the browser.", nameof(url));
        }

        processStarter.Start(new ProcessStartInfo(url.AbsoluteUri)
        {
            UseShellExecute = true,
        });
    }

    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        processStarter.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
        });
    }
}

public interface IProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}

public sealed class ProcessStarter : IProcessStarter
{
    public void Start(ProcessStartInfo startInfo)
    {
        using Process? process = Process.Start(startInfo);
    }
}
