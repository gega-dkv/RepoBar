# RepoBar Windows

This directory contains the Windows/Linux-capable .NET implementation scaffold for RepoBar.

The current Swift macOS app remains the production macOS app. This implementation uses the Swift app and `windows-parity-matrix.md` as behavior references.

## Projects

- `RepoBar.Core`: shared provider models, service contracts, settings, cache abstractions, local Git abstractions, formatting, sorting, filtering, and business logic.
- `RepoBar.Desktop`: Avalonia desktop shell, tray/dashboard/settings UI, Windows platform adapters, notifications, autostart, browser/shell launch, and packaging entry points.
- `RepoBar.Cli`: diagnostics and automation CLI sharing `RepoBar.Core`.
- `RepoBar.Tests`: focused unit tests for the Windows port.

## Commands

```sh
dotnet restore RepoBar.Windows.sln
dotnet build RepoBar.Windows.sln --no-restore
dotnet test RepoBar.Windows.sln --no-build
dotnet run --project RepoBar.Desktop
dotnet run --project RepoBar.Cli -- --help
```

The scaffold targets `net10.0` because .NET 10 is the active LTS release in 2026. Package versions are centralized in `Directory.Packages.props`.

## Supported V1 Behavior

- GitHub.com and GitLab.com read-only provider clients.
- GitHub Enterprise custom host settings and API URL validation.
- PAT login for the desktop shell and CLI.
- Provider-specific credential account keys for GitHub.com, GitHub Enterprise, and GitLab.com.
- Browser OAuth login for the desktop shell and CLI using GitHub authorization code flow, PKCE, loopback callback, token exchange, refresh-token persistence, and refresh-before-use.
- Windows Credential Manager for release credentials; explicit file-backed credentials for debug and tests.
- Settings under `%AppData%\RepoBar` and cache under `%LocalAppData%\RepoBar`.
- SQLite REST, GraphQL, ETag, rate-limit, stale-startup, and cache-clear surfaces.
- Refresh scheduling policy with configurable intervals, manual refresh override, and backoff-delayed next refresh.
- Discrawl-compatible archive source configuration, validation, import metadata, status, and CLI management.
- Local Git discovery, scan, scan-time `local --sync`, status, clean/dirty/ahead/behind/diverged/missing-upstream states, branch listing with upstream/tracking data, worktree listing, checkout/open commands, clean behind-only fast-forward sync, and explicit-confirmation CLI rebase/reset actions.
- Avalonia tray-first shell with compact provider-backed dashboard, repository detail panel, settings tabs, capability-aware unsupported provider labels, and cache-first rendering.
- Repository browser rows with autocomplete-style scoring and filtering.
- Local checkout changelog preview using common changelog filenames and release-tag selection.
- CLI commands for repositories with documented filtering/sorting flags plus age, release, event, owner, mine, scope, forks, archived, and pinned filters; repo detail traffic/heatmap/release flags; recent items including discussions; contribution heatmap; manual refresh; changelog and markdown rendering; local status/actions; checkout/open; cache; rate limits; documented settings keys for appearance/activity/heatmap/local controls; repository visibility; login/logout/status; and archives.

## Explicitly Unsupported Or Deferred

- Bitbucket Cloud and Forgejo/Gitea desktop parity are deferred. Provider and capability extension points remain in the core model.
- The global typed GitHub reference monitor is deferred for Windows v1.
- Sparkle is macOS-only. Windows updates use signed MSI major upgrades from GitHub Releases.
- There is no Windows telemetry or crash-upload service in v1. Diagnostics are local and redacted.

See `../windows-phase9-parity-audit.md` for the current parity status and Windows 11 runtime checklist.

## Packaging

Windows v1 uses a conventional WiX MSI plus ZIP artifact. MSIX is deferred until Windows runtime verification proves tray, autostart, local Git folder access, shell launch, and Credential Manager behavior under package identity.

```powershell
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64
pwsh Windows/Packaging/Scripts/validate-windows-package.ps1 -ArtifactsDir Windows/artifacts
```

See `Windows/Packaging/README.md` and `Windows/Packaging/windows-release-checklist.md`.
