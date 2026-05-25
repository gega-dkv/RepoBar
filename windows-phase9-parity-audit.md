# Windows Phase 9 Parity Audit

Phase: `GOAL_PROMPT_WINDOWS_PHASE_9_PARITY_HARDENING`

Status: repository-verifiable hardening passed; Windows 11 runtime verification split into `windows-runtime-verification-plan.md`.

## Repository-Verified V1 Surface

| Area | Resolution | Evidence |
| --- | --- | --- |
| Separate macOS boundary | Windows port lives under `Windows/`; no Swift product files are part of the Windows build. | `Windows/RepoBar.Windows.sln`; build/test commands below. |
| .NET/Avalonia solution | Implemented as `RepoBar.Core`, `RepoBar.Desktop`, `RepoBar.Cli`, and `RepoBar.Tests`, targeting `net10.0`. | `Windows/Directory.Build.props`; `Windows/README.md`. |
| Provider model and identity | Implemented with provider, host, capability, identity, stats, repository, and unsupported-feature models. | Unit tests for provider capabilities, identity, filters, sorting, unsupported features. |
| GitHub.com read-only client | Implemented for repositories, search, user, issues, PRs, releases, branches, tags, commits, contributors, workflow runs, contents, traffic, heatmap, discussions, and rate limits. | Fixture-backed provider tests. |
| GitHub Enterprise host support | Implemented through custom `RepositoryHost` and host/API URL validation. | User settings and provider request tests. |
| GitHub OAuth/PKCE | Implemented browser OAuth command orchestration with PKCE, loopback callback, authorization code token exchange, client credential persistence, refresh-token persistence, and refresh-before-use for desktop and CLI. | OAuth service, CLI, and shell view-model tests. |
| GitLab.com read-only client | Implemented for projects, search, user, issues, MRs, releases, branches, tags, commits, contributors, tree/raw contents, and pipelines. | Fixture-backed provider tests. |
| Provider unsupported states | GitLab traffic, discussions, contribution calendar, and rate-limit diagnostics are explicit unavailable states. Bitbucket and Forgejo/Gitea remain capability-gated extension points. | Capability and unsupported-feature tests. |
| Changelog preview | Local checkout changelog preview reads common changelog filenames and uses the shared markdown section parser with release-tag selection. | Parser and preview service tests. |
| Cache-first/offline behavior | SQLite REST/GraphQL/rate-limit cache supports stale startup reads and `304` cached-body handoff. | `PersistentCacheReturnsStaleStartupBodyBeforeRefresh`; `UsesCachedBodyForNotModifiedResponses`. |
| Rate-limit and backoff behavior | `403`/`429` exhausted limits and `Retry-After` produce backoff diagnostics; persisted rate limits power CLI/status output. | Provider runner, cache, and CLI tests. |
| Refresh scheduler policy | Configurable intervals, manual refresh override, and rate-limit/backoff-delayed next refresh are modeled in shared core. | Refresh scheduler tests. |
| Settings/cache paths | Settings use `%AppData%\RepoBar`; cache uses `%LocalAppData%\RepoBar`. | Storage path tests and Windows docs. |
| Credentials | Release mode uses Windows Credential Manager on Windows; debug/test mode uses explicit file-backed credentials; PAT account keys use the selected provider host for GitHub, GitHub Enterprise, and GitLab. | Credential factory, file store, shell, and CLI tests. |
| Secret redaction | Diagnostics redact structured secret fields and raw GitHub/GitLab token shapes; CLI login/status/logout do not echo PATs. | Provider runner and CLI tests. |
| Local Git states | Clean, dirty, ahead, behind, diverged, missing upstream, detached, and safe fast-forward eligibility are modeled. | Local project tests. |
| Local project scanning | Scans selected roots, skips hidden/reparse-point directories, matches local repos to provider repos. | Local project tests. |
| Local branch/worktree/open/checkout/sync and destructive CLI actions | `local --sync` and `local sync` use safe fast-forward guardrails; `local branches` lists local/remote branches with upstream/tracking data; `worktrees` lists local Git worktrees from porcelain output; `open finder`, `open terminal`, and `checkout` are wired through testable shell/Git boundaries; `local rebase` and `local reset` require explicit `--confirm`/`--yes` before Git commands run. | Local project and CLI tests. |
| Tray/UI shell | Avalonia shell builds with tray-first commands, compact dashboard, repo detail panel, settings tabs, PAT login, provider-backed cache-first dashboard source, and capability labels. | Compiled XAML build and shell view-model tests. |
| Repository browser/autocomplete | Shared autocomplete scoring/suggestions are ported from the Swift behavior and wired into the settings repository browser rows. | Autocomplete and shell view-model tests. |
| CLI parity | Implemented diagnostics/automation command surface for repos with documented age/release/event/forks/archived/scope/filter/owner/mine/pinned-only/only-with/sort flags, repo details with traffic/heatmap/release flags, recent items including discussions, contribution heatmap, manual refresh, changelog and markdown rendering, local, cache, rate-limits, documented settings keys for appearance/activity/heatmap/local controls, pin/unpin/hide/show visibility commands, login/logout/status, and archives. | CLI application tests. |
| Archives | Discrawl-compatible archive source configuration, validation, import metadata, status, and CLI management are implemented. | Storage and CLI archive tests. |
| Packaging route | Windows v1 uses WiX MSI plus ZIP; MSIX deferred. Generated WiX file list now preserves nested publish directories such as `cli/`. | Packaging scripts and tests. |
| Updates | Windows v1 uses signed MSI major upgrades from GitHub Releases; Sparkle remains macOS-only. | `docs/release.md`; packaging docs/tests. |
| Telemetry/crash policy | No telemetry or crash upload service is implemented for Windows v1. Diagnostics are local and redacted. | Documentation and absence of telemetry dependencies. |

## Deferred Or Runtime-Required Items

These items cannot be fully proven from this macOS checkout and require a Windows 11 VM/device:

- Target-system execution plan: `windows-runtime-verification-plan.md`.
- Tray icon presence, left-click/right-click behavior, and high-DPI rendering at 100%, 125%, 150%, and 200% scaling.
- Native notification presentation.
- Launch-at-login registry behavior after reboot.
- Windows Credential Manager read/write/delete in a release build.
- Shell URL/path launch from an installed package.
- MSI clean install, major upgrade, uninstall, and settings/cache preservation.
- Live GitHub.com, GitHub Enterprise, and GitLab.com happy paths with real tokens and provider permissions.
- Offline, expired-token, missing-permission, and provider-rate-limited behavior against live APIs.
- Settings/cache/credential persistence across real app restart and installed-package upgrade.

## Explicit Deferred Product Scope

- Bitbucket Cloud desktop parity: deferred; provider/capability extension point retained.
- Forgejo/Gitea desktop parity: deferred; provider/capability extension point retained.
- Global typed GitHub reference monitor: deferred; Windows permission model needs separate design.

## Commands Run

```sh
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Result:

- Build: succeeded with 0 warnings and 0 errors.
- Tests: 131 passed, 0 failed, 0 skipped.
