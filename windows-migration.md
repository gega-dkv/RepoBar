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
