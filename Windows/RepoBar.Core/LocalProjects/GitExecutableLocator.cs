using System.Diagnostics;

namespace RepoBar.Core.LocalProjects;

public sealed record GitExecutableInfo(string Path, string? Version, string? Error);

public sealed class GitExecutableLocator(IFileSystem fileSystem, IProcessRunner processRunner)
{
    public static readonly IReadOnlyList<string> DefaultWindowsCandidates =
    [
        @"C:\Program Files\Git\cmd\git.exe",
        @"C:\Program Files\Git\bin\git.exe",
        @"C:\Program Files (x86)\Git\cmd\git.exe",
        @"C:\Program Files (x86)\Git\bin\git.exe",
    ];

    public string? Locate(string? configuredPath, string? pathEnvironment)
    {
        foreach (string candidate in Candidates(configuredPath, pathEnvironment))
        {
            if (fileSystem.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<GitExecutableInfo> GetInfoAsync(
        string? configuredPath,
        string? pathEnvironment,
        CancellationToken cancellationToken = default)
    {
        string? path = Locate(configuredPath, pathEnvironment);
        if (path is null)
        {
            return new GitExecutableInfo("git.exe", null, "git.exe was not found.");
        }

        ProcessRunResult result = await processRunner.RunAsync(path, ["--version"], null, cancellationToken).ConfigureAwait(false);
        string version = result.StandardOutput.Trim();
        return result.ExitCode == 0
            ? new GitExecutableInfo(path, string.IsNullOrWhiteSpace(version) ? null : version, null)
            : new GitExecutableInfo(path, null, string.IsNullOrWhiteSpace(result.StandardError) ? "git --version failed." : result.StandardError.Trim());
    }

    private static IEnumerable<string> Candidates(string? configuredPath, string? pathEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            foreach (string pathPart in SplitPathEnvironment(pathEnvironment))
            {
                yield return System.IO.Path.Combine(pathPart, "git.exe");
            }
        }

        foreach (string candidate in DefaultWindowsCandidates)
        {
            yield return candidate;
        }
    }

    private static string[] SplitPathEnvironment(string pathEnvironment)
    {
        char separator = pathEnvironment.Contains(';', StringComparison.Ordinal)
            ? ';'
            : System.IO.Path.PathSeparator;
        return pathEnvironment.Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }
}

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    FileAttributes GetAttributes(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ProcessRunner(TimeSpan? timeout = null) : IProcessRunner
{
    private readonly TimeSpan timeout = timeout ?? TimeSpan.FromSeconds(20);

    public async Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            string stdout = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token).ConfigureAwait(false);
            string stderr = await process.StandardError.ReadToEndAsync(timeoutSource.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return new ProcessRunResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return new ProcessRunResult(-1, string.Empty, $"Git command timed out after {timeout}.");
        }
    }
}
