using RepoBar.Core.LocalProjects;
using RepoBar.Core.Models;
using Xunit;

namespace RepoBar.Tests;

public sealed class LocalProjectsTests
{
    [Fact]
    public void GitExecutableLocatorPrefersConfiguredPathThenPathThenDefaults()
    {
        FakeFileSystem fileSystem = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Custom\git.exe",
            @"C:\Git\cmd/git.exe".Replace('/', Path.DirectorySeparatorChar),
        });
        GitExecutableLocator locator = new(fileSystem, new FakeProcessRunner());

        string? configured = locator.Locate(@"C:\Custom\git.exe", @"C:\Git\cmd");
        string? fromPath = locator.Locate(null, @"C:\Git\cmd;C:\Other");

        Assert.Equal(@"C:\Custom\git.exe", configured);
        Assert.Equal(@"C:\Git\cmd/git.exe".Replace('/', Path.DirectorySeparatorChar), fromPath);
    }

    [Fact]
    public async Task GitExecutableInfoRunsVersionCommand()
    {
        FakeFileSystem fileSystem = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Git\cmd/git.exe".Replace('/', Path.DirectorySeparatorChar),
        });
        FakeProcessRunner runner = new();
        runner.Set(["--version"], new ProcessRunResult(0, "git version 2.50.0\n", ""));
        GitExecutableLocator locator = new(fileSystem, runner);

        GitExecutableInfo info = await locator.GetInfoAsync(null, @"C:\Git\cmd;C:\Other");

        Assert.Equal("git version 2.50.0", info.Version);
        Assert.Null(info.Error);
    }

    [Fact]
    public void ScannerFindsGitRepositoriesWithinDepthAndSkipsHiddenDirectories()
    {
        string root = CreateGitTree();
        LocalProjectsService service = new(new PhysicalFileSystem(), new FakeProcessRunner(), "git");

        IReadOnlyList<string> repos = service.FindGitRepositories(root, maxDepth: 3);

        Assert.Contains(Path.Combine(root, "RepoBar"), repos);
        Assert.DoesNotContain(Path.Combine(root, ".hidden", "HiddenRepo"), repos);
    }

    [Fact]
    public async Task LocalRepoStatusParsesBranchRemoteDirtyCountsAndAheadBehind()
    {
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");
        FakeProcessRunner runner = new();
        runner.Set(["branch", "--show-current"], new ProcessRunResult(0, "main\n", ""));
        runner.Set(["status", "--porcelain=v1"], new ProcessRunResult(0, " M README.md\n?? new-file.txt\nD  old.txt\n", ""));
        runner.Set(["rev-list", "--left-right", "--count", "HEAD...@{u}"], new ProcessRunResult(0, "1\t2\n", ""));
        runner.Set(["config", "--get", "remote.origin.url"], new ProcessRunResult(0, "git@github.com:owner/RepoBar.git\n", ""));
        runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], new ProcessRunResult(0, "origin/main\n", ""));
        LocalProjectsService service = new(new PhysicalFileSystem(), runner, "git");

        LocalRepoStatus status = await service.GetStatusAsync(repo);

        Assert.Equal("main", status.Branch);
        Assert.Equal("owner/RepoBar", status.FullName);
        Assert.False(status.IsClean);
        Assert.Equal(LocalSyncState.Dirty, status.SyncState);
        Assert.Equal(1, status.AheadCount);
        Assert.Equal(2, status.BehindCount);
        Assert.Equal("+1 -1 ~1", status.DirtyCounts?.Summary);
        Assert.Equal(["README.md", "new-file.txt", "old.txt"], status.DirtyFiles);
        Assert.Equal("origin/main", status.UpstreamBranch);
    }

    [Fact]
    public async Task FastForwardSyncOnlyRunsForCleanBehindRepositories()
    {
        string root = CreateGitTree();
        string repo = Path.Combine(root, "RepoBar");
        FakeProcessRunner runner = new();
        runner.Set(["branch", "--show-current"], new ProcessRunResult(0, "main\n", ""));
        runner.Set(["status", "--porcelain=v1"], new ProcessRunResult(0, "", ""));
        runner.Set(["rev-list", "--left-right", "--count", "HEAD...@{u}"], new ProcessRunResult(0, "0\t2\n", ""));
        runner.Set(["config", "--get", "remote.origin.url"], new ProcessRunResult(0, "https://github.com/owner/RepoBar.git\n", ""));
        runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], new ProcessRunResult(0, "origin/main\n", ""));
        runner.Set(["pull", "--ff-only"], new ProcessRunResult(0, "Fast-forward\n", ""));
        LocalProjectsService service = new(new PhysicalFileSystem(), runner, "git");

        LocalGitSyncResult result = await service.FastForwardSyncAsync(repo);

        Assert.True(result.Attempted);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RebaseAndHardResetRequireConfirmationBeforeRunningGit()
    {
        string repo = Path.Combine(CreateGitTree(), "RepoBar");
        FakeProcessRunner runner = new();
        runner.Set(["rebase", "@{u}"], new ProcessRunResult(0, "Current branch main is up to date.\n", ""));
        runner.Set(["reset", "--hard", "@{u}"], new ProcessRunResult(0, "HEAD is now at abc\n", ""));
        LocalProjectsService service = new(new PhysicalFileSystem(), runner, "git");

        LocalGitSyncResult rebaseSkipped = await service.RebaseAsync(repo, confirmed: false);
        LocalGitSyncResult resetSkipped = await service.HardResetAsync(repo, confirmed: false);
        LocalGitSyncResult rebase = await service.RebaseAsync(repo, confirmed: true);
        LocalGitSyncResult reset = await service.HardResetAsync(repo, confirmed: true);

        Assert.False(rebaseSkipped.Attempted);
        Assert.False(resetSkipped.Attempted);
        Assert.True(rebase.Succeeded);
        Assert.True(reset.Succeeded);
    }

    [Fact]
    public async Task WorktreesParsePorcelainOutput()
    {
        string repo = Path.Combine(CreateGitTree(), "RepoBar");
        FakeProcessRunner runner = new();
        runner.Set(
            ["worktree", "list", "--porcelain"],
            new ProcessRunResult(
                0,
                """
                worktree C:/Projects/RepoBar
                HEAD abc123
                branch refs/heads/main

                worktree C:/Projects/RepoBar-feature
                HEAD def456
                detached
                locked

                """,
                ""));
        LocalProjectsService service = new(new PhysicalFileSystem(), runner, "git");

        IReadOnlyList<LocalWorktree> worktrees = await service.WorktreesAsync(repo);

        Assert.Equal(2, worktrees.Count);
        Assert.Equal("main", worktrees[0].Branch);
        Assert.False(worktrees[0].IsDetached);
        Assert.True(worktrees[1].IsDetached);
        Assert.True(worktrees[1].IsLocked);
    }

    [Fact]
    public async Task BranchesParseCurrentRemoteAndTracking()
    {
        string repo = Path.Combine(CreateGitTree(), "RepoBar");
        FakeProcessRunner runner = new();
        runner.Set(
            ["branch", "--all", "--format=%(HEAD)%09%(refname:short)%09%(upstream:short)%09%(upstream:track)"],
            new ProcessRunResult(
                0,
                """
                *	main	origin/main	[ahead 1]
                 	feature	origin/feature	
                 	remotes/origin/main		
                """,
                ""));
        LocalProjectsService service = new(new PhysicalFileSystem(), runner, "git");

        IReadOnlyList<LocalBranch> branches = await service.BranchesAsync(repo);

        Assert.Equal(3, branches.Count);
        Assert.True(branches[0].IsCurrent);
        Assert.Equal("main", branches[0].Name);
        Assert.Equal("origin/main", branches[0].Upstream);
        Assert.Equal("[ahead 1]", branches[0].Tracking);
        Assert.False(branches[1].IsRemote);
        Assert.True(branches[2].IsRemote);
    }

    [Theory]
    [InlineData(true, 0, 0, LocalSyncState.Synced, "Up to date", false)]
    [InlineData(true, 0, 2, LocalSyncState.Behind, "Behind 2", true)]
    [InlineData(true, 3, 0, LocalSyncState.Ahead, "Ahead 3", false)]
    [InlineData(true, 1, 2, LocalSyncState.Diverged, "Diverged", false)]
    [InlineData(false, 0, 2, LocalSyncState.Dirty, "Dirty", false)]
    public void LocalSyncStateCoversCleanDirtyAheadBehindAndDivergedStates(
        bool isClean,
        int? ahead,
        int? behind,
        LocalSyncState expected,
        string detail,
        bool canAutoSync)
    {
        LocalSyncState state = LocalSyncStateResolver.Resolve(isClean, ahead, behind);
        LocalRepoStatus status = new(
            @"C:\Projects\RepoBar",
            "RepoBar",
            "owner/RepoBar",
            "main",
            isClean,
            ahead,
            behind,
            state);

        Assert.Equal(expected, state);
        Assert.Equal(detail, status.SyncDetail);
        Assert.Equal(canAutoSync, status.CanAutoSync);
    }

    [Fact]
    public void LocalSyncStateMarksMissingUpstreamAndDetachedReposUnsyncable()
    {
        LocalRepoStatus missingUpstream = new(
            @"C:\Projects\RepoBar",
            "RepoBar",
            "owner/RepoBar",
            "main",
            true,
            null,
            null,
            LocalSyncStateResolver.Resolve(true, null, null));
        LocalRepoStatus detachedBehind = new(
            @"C:\Projects\RepoBar",
            "RepoBar",
            "owner/RepoBar",
            "detached",
            true,
            0,
            1,
            LocalSyncState.Behind);

        Assert.Equal(LocalSyncState.Unknown, missingUpstream.SyncState);
        Assert.Equal("No upstream", missingUpstream.SyncDetail);
        Assert.False(missingUpstream.CanAutoSync);
        Assert.False(detachedBehind.CanAutoSync);
    }

    [Fact]
    public void LocalRepoIndexMatchesByFullNameThenUniqueRepositoryName()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Tests", Guid.NewGuid().ToString("N"));
        LocalRepoStatus repobar = new(Path.Combine(root, "RepoBar"), "RepoBar", "owner/RepoBar", "main", true, 0, 0, LocalSyncState.Synced);
        LocalRepoStatus other = new(Path.Combine(root, "other"), "other", null, "main", true, null, null, LocalSyncState.Unknown);
        LocalRepoIndex index = new([repobar, other]);
        Repository repository = new("1", "RepoBar", "owner");

        Assert.Equal(repobar, index.StatusFor(repository));
        Assert.Equal(other, index.StatusForFullName("someone/other"));
        Assert.Equal(repobar, index.StatusContainingPath(Path.Combine(root, "RepoBar", "README.md")));
    }

    [Theory]
    [InlineData("https://github.com/openai/codex.git", "openai/codex")]
    [InlineData("git@github.com:openai/codex.git", "openai/codex")]
    public void RemoteParserExtractsFullName(string remote, string expected)
    {
        Assert.Equal(expected, LocalProjectsService.ParseRemoteFullName(remote));
    }

    private static string CreateGitTree()
    {
        string root = Path.Combine(Path.GetTempPath(), "RepoBar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "RepoBar", ".git"));
        Directory.CreateDirectory(Path.Combine(root, ".hidden", "HiddenRepo", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "nested", "child", "not-repo"));
        return root;
    }
}

internal sealed class FakeFileSystem(IReadOnlySet<string> files) : IFileSystem
{
    public bool FileExists(string path) => files.Contains(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessRunResult> results = new(StringComparer.Ordinal);

    public void Set(IReadOnlyList<string> arguments, ProcessRunResult result) => results[Key(arguments)] = result;

    public Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(results.TryGetValue(Key(arguments), out ProcessRunResult? result)
            ? result
            : new ProcessRunResult(1, "", "not configured"));
    }

    private static string Key(IReadOnlyList<string> arguments) => string.Join('\u001f', arguments);
}
