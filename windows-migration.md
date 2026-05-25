# Windows Migration Plan

## Summary

Migrate RepoBar to Windows as a new C#/.NET desktop application using Avalonia UI, while keeping the current macOS SwiftUI/AppKit app separate. The Windows port should treat the current Swift codebase as the product and behavior reference, not as a runtime dependency.

This is the recommended path because RepoBar is currently a SwiftPM macOS/iOS package (`Package.swift:6`) with a macOS executable target (`Package.swift:40`) that depends on AppKit, SwiftUI, Sparkle, AppAuth-iOS, MenuBarExtraAccess, and Keychain APIs. Several core services also still include Apple-only implementation details, such as `Security` in `Sources/RepoBarCore/Auth/TokenStore.swift:3`.

## Target Stack

- **Language**: C# on current LTS .NET.
- **UI framework**: Avalonia UI.
- **App shape**: Windows 11+ tray application with a compact dashboard window, settings window, account/provider status, refresh controls, local checkout state, and native notifications.
- **Architecture**: Keep the macOS Swift app unchanged and build a new Windows implementation with equivalent behavior.
- **Initial providers**: GitHub and GitLab.com first, then other providers after the shared abstraction is stable.

## Solution Shape

Create a new Windows/Linux-capable .NET solution that can later share code with the Linux port:

- `RepoBar.Core`: provider models, API clients, repository/service contracts, settings models, cache abstractions, local Git abstractions, formatting, sorting, filtering, and testable business logic.
- `RepoBar.Desktop`: Avalonia UI, tray icon, dashboard window, settings UI, provider/account wiring, notifications, autostart, browser launch, platform adapters, and packaging entry points.
- `RepoBar.Tests`: unit and integration tests ported from the Swift test suite.

The .NET domain model should mirror the current provider-neutral Swift direction:

- Port `SourceControlProvider` behavior from `Sources/RepoBarCore/Models/SourceControlProvider.swift:3`.
- Port `RepositoryService` as the main service contract from `Sources/RepoBarCore/API/RepositoryService.swift:30`.
- Port router-style provider dispatch from `Sources/RepoBarCore/API/RepositoryServiceRouter.swift:3`.
- Port provider capability gates from `Sources/RepoBarCore/Models/ProviderCapabilities.swift:3`.
- Use `Sources/RepoBarCore/API/GitLabClient.swift:3` as the GitLab.com behavior reference.

## Implementation Plan

### Phase 1: Core Domain Port

- Define provider-neutral models for repositories, repository identity, users, releases, issues, merge/pull requests, commits, branches, tags, contributors, CI runs, activity, rate-limit diagnostics, and unsupported features.
- Preserve RepoBar’s current identity model semantics: provider, host, stable ID, namespace path, repository name, path with namespace, web URL, and API URL.
- Implement feature capability checks so unavailable Windows/GitLab sections are hidden or disabled instead of rendered as errors.
- Port sorting, filtering, pin/hidden repository rules, repository display limits, local-project matching rules, and menu/dashboard view-model logic.
- Keep GitHub-specific terms out of shared abstractions except where the feature is actually GitHub-only.

### Phase 2: Provider Clients

- Implement GitHub.com first with current dashboard parity: repository list, repo details, issues, pull requests, releases, branches, tags, commits, workflow runs, contributors, contents, traffic where available, and rate-limit diagnostics.
- Implement GitLab.com next using GitLab REST API behavior already represented in `GitLabClient`: projects, issues, merge requests, releases, branches, tags, commits, contributors, repository tree, raw file contents, and current user.
- Represent GitLab unsupported features explicitly: contribution calendar, traffic stats, discussions, and any unavailable pipeline details.
- Share HTTP concerns through a common request runner: auth headers, JSON decoding, pagination, conditional requests where available, retry/backoff, diagnostics, and redacted logging.
- Keep provider clients read-only for v1.

### Phase 3: Windows Platform Adapters

- Store tokens in Windows Credential Manager by default; allow an explicit debug file-backed store for local development and tests.
- Store settings under `%AppData%\\RepoBar` and cache data under `%LocalAppData%\\RepoBar`.
- Use a `.ico` tray asset and native Windows notifications.
- Implement launch-at-login with the Windows startup mechanism used by the chosen packaging route.
- Discover `git.exe` from `PATH`, Git for Windows default locations, and user-configured paths.
- Open repository URLs and local paths through Windows shell APIs.
- Package as MSIX if it does not block tray/autostart/local Git behavior; otherwise use a conventional installer such as WiX or Inno Setup.

### Phase 4: Avalonia Desktop UI

- Build a tray-first experience matching the macOS menu behavior: click tray icon to show a compact dashboard; right-click to show account, refresh, settings, update/help, and quit actions.
- Implement a dashboard window with repository cards, issue/MR counts, CI status, releases, local status, activity, provider labels, and capability-aware section visibility.
- Implement settings for provider selection, host/account configuration, token method, refresh interval, visible/pinned/hidden repositories, local projects root, auto-sync, cache controls, diagnostics, and startup behavior.
- Implement login flows for PAT first, then OAuth if the provider setup requires it.
- Keep the UI responsive by loading from cache first and refreshing in the background.

### Phase 5: Packaging And Updates

- Produce signed Windows builds with a stable application ID and installer.
- Include uninstall behavior that removes the app cleanly but does not delete user settings/cache unless explicitly requested.
- Decide update mechanism after packaging is proven; do not port Sparkle because it is macOS-specific.
- Publish release artifacts alongside macOS artifacts without changing the current macOS release flow.

## Acceptance Criteria

- A Windows 11 user can install RepoBar, start it, and see a tray icon.
- The user can authenticate with GitHub.com and GitLab.com using PAT, with tokens stored securely.
- The app lists accessible repositories and shows repo details, issues/MRs, releases, branches, tags, commits, and local Git status.
- Unsupported provider features are hidden or labeled unavailable without noisy errors.
- Settings and cache persist across restarts in Windows-appropriate locations.
- Autostart works after reboot when enabled.
- The installer supports clean install, upgrade, and uninstall.
- Debug builds can use file-backed auth without touching Windows Credential Manager.

## Test Plan

- Port Swift tests as behavior specs for provider mapping, repository identity, settings migration/defaults, cache behavior, local Git parsing, filtering, sorting, and unsupported-feature handling.
- Add unit tests for GitHub and GitLab request mapping using deterministic fixtures.
- Add integration tests for Windows Credential Manager, settings/cache paths, local Git discovery, browser/path launch, autostart, tray initialization, notifications, installer upgrade, and uninstall.
- Add UI smoke tests for first launch, login, refresh, repository dashboard, settings save, and provider switching.

## Assumptions

- The macOS Swift app remains the production macOS app.
- The Windows port is a new implementation, not an attempt to compile the existing SwiftUI/AppKit app on Windows.
- C#/.NET with Avalonia UI is the default migration stack unless a future requirement forces a webview-based stack.
- v1 is read-only and provider-focused; write operations are out of scope.

## Implementation Decisions Proven During The Port

- The Windows solution was implemented under `Windows/` as `RepoBar.Core`, `RepoBar.Desktop`, `RepoBar.Cli`, and `RepoBar.Tests`.
- The Windows projects target `net10.0` with centralized package versions and Avalonia `12.0.3`.
- `RepoBar.Cli` is part of Windows v1 because it is required for diagnostics, cache/archive management, settings automation, and testable provider behavior.
- CLI parity includes the documented repository list filters/sorts plus age, release, event, owner, mine, scope, forks, archived, and pinned filters; repository detail traffic/heatmap/release flags; repository/recent-item commands; discussions where supported; contribution heatmap; manual refresh; changelog and markdown rendering; local Git actions; checkout/open; cache/rate-limit diagnostics; documented settings keys for appearance/activity/heatmap/local controls; repository visibility commands; login/logout/status; and archive management.
- GitHub.com and GitLab.com read-only clients are implemented. GitHub Enterprise remains in v1 through custom host/API URL support. Bitbucket Cloud and Forgejo/Gitea remain deferred extension points.
- PAT login and browser OAuth login are implemented for the desktop shell and CLI. OAuth uses GitHub authorization code flow, PKCE, loopback callback, token exchange, client credential persistence, refresh-token persistence, and refresh-before-use in the shared core service.
- PAT credential keys use the selected provider host for GitHub.com, GitHub Enterprise, and GitLab.com in both desktop and CLI paths.
- Settings use `%AppData%\RepoBar`; cache uses `%LocalAppData%\RepoBar`; release credentials use Windows Credential Manager on Windows; debug/test credentials use an explicit file-backed store.
- The persistent cache uses SQLite for REST responses, GraphQL responses, ETags, stale startup reads, and rate-limit state.
- Refresh scheduling policy is implemented in shared core with configurable intervals, manual refresh override, and backoff-delayed next refresh.
- Archive support is included through Discrawl-compatible source configuration, validation, import metadata, status, and CLI management.
- Local Git support covers git.exe discovery, selected-root scanning, scan-time `local --sync`, clean/dirty/ahead/behind/diverged/missing-upstream states, local/remote branch listing with upstream tracking data, worktree detection/listing, checkout into the local projects root, shell open commands, clean behind-only fast-forward sync, and explicit-confirmation CLI rebase/reset actions.
- Repository browser/autocomplete support is ported through shared scoring/suggestions logic and wired into the Avalonia settings view model.
- The Avalonia default dashboard source is provider-backed and cache-first, with credential state, persisted rate-limit diagnostics, and local checkout matching. Sample data remains only for deterministic tests and smoke paths.
- Changelog preview reads common local changelog filenames from matched checkouts and uses the shared parser with release-tag selection.
- Windows v1 packaging is WiX MSI plus ZIP. MSIX remains deferred until runtime checks prove tray, autostart, arbitrary local Git folder access, shell launch, and Credential Manager behavior under package identity.
- Windows updates use signed MSI major upgrades from GitHub Releases. Sparkle remains macOS-only.
- Windows v1 has no telemetry or crash-upload service. Diagnostics are local and secret-redacted.
- Repository-verifiable Phase 9 hardening is recorded in `windows-phase9-parity-audit.md`; final tray, installer, Credential Manager, live-provider, high-DPI, and restart/upgrade checks still require a Windows 11 VM/device and are captured as a separate target-system plan in `windows-runtime-verification-plan.md`.
