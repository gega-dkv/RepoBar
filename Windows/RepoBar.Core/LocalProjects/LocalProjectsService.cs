namespace RepoBar.Core.LocalProjects;

public sealed class LocalProjectsService(IFileSystem fileSystem, IProcessRunner processRunner, string gitExecutablePath)
{
    public IReadOnlyList<string> FindGitRepositories(string rootPath, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !fileSystem.DirectoryExists(rootPath))
        {
            return [];
        }

        List<string> repositories = [];
        Visit(System.IO.Path.GetFullPath(rootPath), 0);
        return repositories;

        void Visit(string directory, int depth)
        {
            if (IsGitRepository(directory))
            {
                repositories.Add(directory);
                return;
            }

            if (depth >= maxDepth)
            {
                return;
            }

            foreach (string child in fileSystem.EnumerateDirectories(directory))
            {
                string name = System.IO.Path.GetFileName(child);
                FileAttributes attributes = fileSystem.GetAttributes(child);
                if ((name.Length > 0 && name[0] == '.') || attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                Visit(child, depth + 1);
            }
        }
    }

    public async Task<LocalRepoStatus> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string branch = await CurrentBranchAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        string? statusOutput = await GitOutputOrNullAsync(["status", "--porcelain=v1"], repositoryPath, cancellationToken, trimOutput: false).ConfigureAwait(false);
        bool isClean = string.IsNullOrWhiteSpace(statusOutput);
        LocalDirtyCounts? dirtyCounts = statusOutput is null ? null : ParseDirtyCounts(statusOutput);
        IReadOnlyList<string> dirtyFiles = statusOutput is null ? [] : ParseDirtyFiles(statusOutput, 10);
        (int? ahead, int? behind) = await AheadBehindAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        string? fullName = await RemoteFullNameAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        string? upstream = await GitOutputOrNullAsync(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], repositoryPath, cancellationToken).ConfigureAwait(false);
        string? worktreeName = WorktreeName(repositoryPath);

        return new LocalRepoStatus(
            repositoryPath,
            System.IO.Path.GetFileName(repositoryPath),
            fullName,
            branch,
            isClean,
            ahead,
            behind,
            LocalSyncStateResolver.Resolve(isClean, ahead, behind),
            dirtyCounts,
            dirtyFiles,
            worktreeName,
            string.IsNullOrWhiteSpace(upstream) ? null : upstream.Trim());
    }

    public async Task<LocalGitSyncResult> FastForwardSyncAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        LocalRepoStatus status = await GetStatusAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        if (!status.CanAutoSync)
        {
            return new LocalGitSyncResult(false, false, "Repository is not eligible for safe fast-forward sync.");
        }

        ProcessRunResult pull = await processRunner.RunAsync(
            gitExecutablePath,
            ["pull", "--ff-only"],
            repositoryPath,
            cancellationToken).ConfigureAwait(false);

        return pull.ExitCode == 0
            ? new LocalGitSyncResult(true, true, null)
            : new LocalGitSyncResult(true, false, string.IsNullOrWhiteSpace(pull.StandardError) ? pull.StandardOutput : pull.StandardError);
    }

    public async Task<LocalGitSyncResult> RebaseAsync(string repositoryPath, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return new LocalGitSyncResult(false, false, "Rebase requires explicit confirmation.");
        }

        ProcessRunResult rebase = await processRunner.RunAsync(
            gitExecutablePath,
            ["rebase", "@{u}"],
            repositoryPath,
            cancellationToken).ConfigureAwait(false);
        return rebase.ExitCode == 0
            ? new LocalGitSyncResult(true, true, null)
            : new LocalGitSyncResult(true, false, string.IsNullOrWhiteSpace(rebase.StandardError) ? rebase.StandardOutput : rebase.StandardError);
    }

    public async Task<LocalGitSyncResult> HardResetAsync(string repositoryPath, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return new LocalGitSyncResult(false, false, "Hard reset requires explicit confirmation.");
        }

        ProcessRunResult reset = await processRunner.RunAsync(
            gitExecutablePath,
            ["reset", "--hard", "@{u}"],
            repositoryPath,
            cancellationToken).ConfigureAwait(false);
        return reset.ExitCode == 0
            ? new LocalGitSyncResult(true, true, null)
            : new LocalGitSyncResult(true, false, string.IsNullOrWhiteSpace(reset.StandardError) ? reset.StandardOutput : reset.StandardError);
    }

    public async Task<LocalGitSyncResult> CloneAsync(string cloneUrl, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cloneUrl))
        {
            return new LocalGitSyncResult(false, false, "Clone URL is required.");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return new LocalGitSyncResult(false, false, "Destination path is required.");
        }

        string? parent = System.IO.Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        ProcessRunResult clone = await processRunner.RunAsync(
            gitExecutablePath,
            ["clone", cloneUrl, destinationPath],
            parent ?? Directory.GetCurrentDirectory(),
            cancellationToken).ConfigureAwait(false);
        return clone.ExitCode == 0
            ? new LocalGitSyncResult(true, true, null)
            : new LocalGitSyncResult(true, false, string.IsNullOrWhiteSpace(clone.StandardError) ? clone.StandardOutput : clone.StandardError);
    }

    public async Task<IReadOnlyList<LocalWorktree>> WorktreesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string? output = await GitOutputOrNullAsync(["worktree", "list", "--porcelain"], repositoryPath, cancellationToken, trimOutput: false).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(output) ? [] : ParseWorktrees(output);
    }

    public async Task<IReadOnlyList<LocalBranch>> BranchesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string? output = await GitOutputOrNullAsync(
            ["branch", "--all", "--format=%(HEAD)%09%(refname:short)%09%(upstream:short)%09%(upstream:track)"],
            repositoryPath,
            cancellationToken,
            trimOutput: false).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(output) ? [] : ParseBranches(output);
    }

    public static IReadOnlyList<LocalBranch> ParseBranches(string output)
    {
        List<LocalBranch> branches = [];
        foreach (string rawLine in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = rawLine.TrimEnd('\r').Split('\t');
            if (parts.Length < 2)
            {
                continue;
            }

            string name = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string? upstream = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null;
            string? tracking = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null;
            branches.Add(new LocalBranch(
                name,
                parts[0].Trim() == "*",
                name.StartsWith("remotes/", StringComparison.Ordinal),
                upstream,
                tracking));
        }

        return branches;
    }

    public static IReadOnlyList<LocalWorktree> ParseWorktrees(string output)
    {
        List<LocalWorktree> worktrees = [];
        string? path = null;
        string? head = null;
        string? branch = null;
        bool isBare = false;
        bool isDetached = false;
        bool isLocked = false;

        foreach (string rawLine in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                head = line["HEAD ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..].Replace("refs/heads/", "", StringComparison.Ordinal);
            }
            else if (line.Equals("bare", StringComparison.Ordinal))
            {
                isBare = true;
            }
            else if (line.Equals("detached", StringComparison.Ordinal))
            {
                isDetached = true;
            }
            else if (line.StartsWith("locked", StringComparison.Ordinal))
            {
                isLocked = true;
            }
        }

        Flush();
        return worktrees;

        void Flush()
        {
            if (path is null)
            {
                return;
            }

            worktrees.Add(new LocalWorktree(path, branch, head, isBare, isDetached, isLocked));
            path = null;
            head = null;
            branch = null;
            isBare = false;
            isDetached = false;
            isLocked = false;
        }
    }

    public static LocalDirtyCounts ParseDirtyCounts(string output)
    {
        int added = 0;
        int modified = 0;
        int deleted = 0;

        foreach (string rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 2)
            {
                continue;
            }

            char indexStatus = line[0];
            char worktreeStatus = line[1];
            if (indexStatus == 'A' || worktreeStatus == 'A' || indexStatus == '?' || worktreeStatus == '?')
            {
                added++;
            }

            if (indexStatus == 'M' || worktreeStatus == 'M' || indexStatus == 'R' || worktreeStatus == 'R')
            {
                modified++;
            }

            if (indexStatus == 'D' || worktreeStatus == 'D')
            {
                deleted++;
            }
        }

        return new LocalDirtyCounts(added, modified, deleted);
    }

    public static IReadOnlyList<string> ParseDirtyFiles(string output, int limit) =>
        output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 3)
            .Select(line => line[3..])
            .Take(limit)
            .ToList();

    public static string? ParseRemoteFullName(string remote)
    {
        string value = remote.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            string[] parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[^2]}/{StripGitSuffix(parts[^1])}" : null;
        }

        int colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && value[..colon].Contains('@', StringComparison.Ordinal))
        {
            string[] parts = value[(colon + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[^2]}/{StripGitSuffix(parts[^1])}" : null;
        }

        return null;
    }

    private bool IsGitRepository(string directory) =>
        fileSystem.DirectoryExists(System.IO.Path.Combine(directory, ".git"))
        || fileSystem.FileExists(System.IO.Path.Combine(directory, ".git"));

    private async Task<string> CurrentBranchAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        string? branch = await GitOutputOrNullAsync(["branch", "--show-current"], repositoryPath, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(branch) ? "detached" : branch.Trim();
    }

    private async Task<(int? ahead, int? behind)> AheadBehindAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        string? output = await GitOutputOrNullAsync(["rev-list", "--left-right", "--count", "HEAD...@{u}"], repositoryPath, cancellationToken).ConfigureAwait(false);
        if (output is null)
        {
            return (null, null);
        }

        string[] parts = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[0], out int ahead) && int.TryParse(parts[1], out int behind)
            ? (ahead, behind)
            : (null, null);
    }

    private async Task<string?> RemoteFullNameAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        string? remote = await GitOutputOrNullAsync(["config", "--get", "remote.origin.url"], repositoryPath, cancellationToken).ConfigureAwait(false);
        return remote is null ? null : ParseRemoteFullName(remote);
    }

    private async Task<string?> GitOutputOrNullAsync(
        IReadOnlyList<string> arguments,
        string repositoryPath,
        CancellationToken cancellationToken,
        bool trimOutput = true)
    {
        ProcessRunResult result = await processRunner.RunAsync(gitExecutablePath, arguments, repositoryPath, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0
            ? trimOutput ? result.StandardOutput.Trim() : result.StandardOutput
            : null;
    }

    private static string? WorktreeName(string repositoryPath)
    {
        string gitFile = System.IO.Path.Combine(repositoryPath, ".git");
        if (!File.Exists(gitFile))
        {
            return null;
        }

        string contents = File.ReadAllText(gitFile);
        const string marker = "worktrees/";
        int markerIndex = contents.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        string suffix = contents[(markerIndex + marker.Length)..].Trim();
        return suffix.Split(System.IO.Path.DirectorySeparatorChar, '/', '\\').FirstOrDefault();
    }

    private static string StripGitSuffix(string value) =>
        value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}

public sealed record LocalGitSyncResult(bool Attempted, bool Succeeded, string? Error);

public sealed record LocalBranch(
    string Name,
    bool IsCurrent,
    bool IsRemote,
    string? Upstream,
    string? Tracking);

public sealed record LocalWorktree(
    string Path,
    string? Branch,
    string? Head,
    bool IsBare,
    bool IsDetached,
    bool IsLocked);
