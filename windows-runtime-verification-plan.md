# Windows Runtime Verification Plan

Use this plan on a Windows 11 VM or physical Windows 11 machine to close the system-dependent runtime checks split out from the repository-side Windows implementation goal.

The current macOS-hosted implementation goal is considered passed when repository-verifiable build/test/package/static checks are green and the remaining blockers are only target-system checks. This file is the follow-up goal implementation plan for those Windows-only checks.

## Goal Prompt

<!-- GOAL_PROMPT_WINDOWS_RUNTIME_VERIFICATION -->

```text
Run the RepoBar Windows runtime verification plan on Windows 11.

Use the current repository state as the implementation source. Verify the Windows port end to end: build, tests, package, install, tray behavior, high-DPI behavior, native notifications, launch-at-login after reboot, Windows Credential Manager release auth, shell URL/path launch, live GitHub/GitLab provider paths, offline/rate-limit/error paths, settings/cache/credential persistence across restart and upgrade, and MSI uninstall behavior.

Do not change the macOS Swift app. If a runtime check fails, fix the Windows implementation or packaging scripts, rerun the relevant check, and update windows-port-progress.md plus windows-phase9-parity-audit.md with exact evidence.
```

## Prerequisites

- Windows 11, standard user account, no administrator-only assumptions.
- PowerShell 7+ (`pwsh`).
- .NET SDK matching the repository target framework.
- WiX CLI installed and available on `PATH`.
- Optional signing environment for signed release validation:
  - `REPOBAR_SIGNTOOL`
  - `REPOBAR_SIGN_CERT_SHA1`
  - timestamp URL/configuration used by `Windows/Packaging/Scripts/sign-windows-artifacts.ps1`
- Test credentials:
  - GitHub.com PAT with read access to at least one repository.
  - GitLab.com PAT with read access to at least one project.
  - Optional GitHub Enterprise host and PAT/OAuth app credentials.
  - Optional OAuth client ID/secret for GitHub browser OAuth verification.
- A local Git root containing at least one clean repo, one dirty repo, one behind repo, and one repo with a missing upstream.

## Phase A: Build And Test

```powershell
dotnet restore Windows/RepoBar.Windows.sln
dotnet build Windows/RepoBar.Windows.sln --no-restore
dotnet test Windows/RepoBar.Windows.sln --no-build
```

Pass criteria:

- Build succeeds with 0 errors.
- Tests pass.
- No Swift/macOS product files are modified.

## Phase B: Package And Static Validation

```powershell
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64
pwsh Windows/Packaging/Scripts/validate-windows-package.ps1 -ArtifactsDir Windows/artifacts
```

If signing credentials are available:

```powershell
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64 -Sign
```

Pass criteria:

- ZIP artifact exists.
- MSI artifact exists when WiX is available.
- `RepoBar.Desktop.exe` and `cli/RepoBar.Cli.exe` exist in the ZIP.
- MSI preserves the CLI subdirectory.
- Signing step does not log secrets.

## Phase C: Install, Launch, Tray, And DPI

1. Install the MSI as the standard user.
2. Start RepoBar from the Start menu.
3. Verify the process path points to the installed Windows artifact.
4. Verify the tray icon appears.
5. Verify left-click opens the compact dashboard.
6. Verify right-click opens the command menu.
7. Repeat visual checks at 100%, 125%, 150%, and 200% display scaling.

Pass criteria:

- Tray icon is visible and not blurry/cropped at each scale.
- Dashboard is readable and compact.
- Text does not overlap.
- Right-click menu commands are available.

## Phase D: Authentication And Credential Storage

### PAT

```powershell
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe login --token $env:GITHUB_PAT --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe status
```

Repeat for GitLab by setting the selected provider in settings or using the app settings surface, then logging in with the GitLab PAT.

### OAuth

```powershell
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe login --oauth --client-id $env:REPOBAR_GITHUB_CLIENT_ID --client-secret $env:REPOBAR_GITHUB_CLIENT_SECRET --loopback-port 53682 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe status
```

Pass criteria:

- Tokens are stored in Windows Credential Manager for release builds.
- Debug file-backed auth is only used when explicitly configured.
- CLI/status output does not contain PATs, OAuth access tokens, refresh tokens, client secrets, or credential blobs.
- Desktop account UI can save PAT and start browser OAuth without retaining the client secret in the UI.

## Phase E: Live Provider Paths

Run with real credentials:

```powershell
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe repos --json --limit 5
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe repo owner/name --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe issues owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe pulls owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe releases owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe ci owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe branches owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe tags owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe commits owner/name --limit 5 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe contributors owner/name --limit 5 --json
```

Pass criteria:

- GitHub.com happy paths return expected repository data.
- GitLab.com happy paths return expected project data.
- GitHub Enterprise works with the configured custom host, if available.
- GitLab unsupported features are labeled unavailable, not noisy failures.

## Phase F: Offline, Expired Token, Missing Permission, And Rate Limit

Run these with controlled credentials/network:

- Disable network and launch the desktop app.
- Run CLI commands while offline.
- Use an expired/invalid token.
- Use a token without traffic/private-repo permissions.
- Exercise a known low-rate-limit token or test fixture/proxy that returns `403`/`429` with rate-limit headers.

Pass criteria:

- Cached dashboard rows render first when cache exists.
- Offline/live failures do not erase useful cached data.
- Expired-token and missing-permission states are clear and redacted.
- Rate-limit state is visible and persisted.
- Logs and CLI output contain no secrets.

## Phase G: Local Git And Shell Integration

```powershell
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe local --root C:\Path\To\Projects --depth 4 --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe local branches C:\Path\To\Repo --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe worktrees C:\Path\To\Repo --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe local sync C:\Path\To\CleanBehindRepo --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe local rebase C:\Path\To\Repo --confirm --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe local reset C:\Path\To\Repo --confirm --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe checkout owner/name --root C:\Path\To\Projects --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe open finder C:\Path\To\Repo --json
Windows/artifacts/publish/win-x64/cli/RepoBar.Cli.exe open terminal C:\Path\To\Repo --json
```

Pass criteria:

- `git.exe` is discovered from `PATH` or Git for Windows default paths.
- Clean, dirty, ahead, behind, diverged, worktree, and missing-upstream states display correctly.
- Local/remote branch, upstream, and tracking state display correctly.
- Auto-sync only fast-forwards clean behind repos.
- Rebase/reset require explicit confirmation.
- Checkout clones into the selected local projects root.
- Desktop shell URL/path launch opens the default browser or file manager.
- CLI folder and terminal open commands launch the expected Windows shell targets.

## Phase H: Notifications And Startup

1. Trigger a native notification from the desktop app.
2. Enable launch at login.
3. Reboot.
4. Verify RepoBar starts automatically and uses the installed binary.

Pass criteria:

- Native notification appears in Windows notification UI.
- Startup entry is created for the installed app path.
- App starts after reboot without prompting for credentials again.

## Phase I: Restart, Upgrade, And Uninstall

1. Save settings, credentials, cache, and archive config.
2. Quit and restart the app.
3. Verify state persists.
4. Install a newer MSI over the current MSI.
5. Verify settings, cache, and credentials persist after upgrade.
6. Uninstall.
7. Verify app binaries are removed.
8. Verify user settings/cache are not deleted unless an explicit removal flow is added.

Pass criteria:

- Restart preserves settings/cache/credentials.
- MSI major upgrade works.
- Uninstall removes installed files and shortcuts.
- User data preservation matches release docs.

## Evidence To Record

Update `windows-port-progress.md` and `windows-phase9-parity-audit.md` with:

- Windows version/build.
- .NET SDK version.
- WiX version.
- Exact commands run.
- Test result counts.
- Artifact names and paths.
- Installer version and UpgradeCode.
- Screenshots or notes for tray/high-DPI states.
- Live-provider hosts tested.
- Credential storage mode verified.
- Any failures and fixes.

## Completion Rule

Only mark the Windows master implementation complete after every pass criterion above is satisfied or an item is explicitly moved out of v1 scope in `windows-parity-matrix.md` with a documented product decision.
