# Linux Migration Plan

## Summary

Migrate RepoBar to Linux as a new C#/.NET desktop application using Avalonia UI, while keeping the current macOS SwiftUI/AppKit app separate. The Linux port should share as much .NET core and UI code as possible with the Windows port, but it must use Linux-native adapters for tray, credentials, autostart, file locations, notifications, and packaging.

This is the recommended path because RepoBar is currently a SwiftPM macOS/iOS package (`Package.swift:6`) with a macOS executable target (`Package.swift:40`) that depends on AppKit, SwiftUI, Sparkle, AppAuth-iOS, MenuBarExtraAccess, and Keychain APIs. Several core services also still include Apple-only implementation details, such as `Security` in `Sources/RepoBarCore/Auth/TokenStore.swift:3`, and local project services are currently guarded for macOS in `Sources/RepoBarCore/LocalProjects/LocalProjectsService.swift:1`.

## Target Stack

- **Language**: C# on current LTS .NET.
- **UI framework**: Avalonia UI.
- **App shape**: Linux desktop app with tray integration where supported, plus a normal desktop-window fallback when tray support is unavailable.
- **Primary desktops**: Ubuntu LTS GNOME with AppIndicator extension, Fedora GNOME, and KDE Plasma.
- **Initial providers**: GitHub and GitLab.com first, then other providers after the shared abstraction is stable.

## Solution Shape

Create a shared .NET solution that can serve both Linux and Windows:

- `RepoBar.Core`: provider models, API clients, repository/service contracts, settings models, cache abstractions, local Git abstractions, formatting, sorting, filtering, and testable business logic.
- `RepoBar.Desktop`: Avalonia UI, tray/window behavior, settings UI, provider/account wiring, notifications, autostart, browser launch, platform adapters, and packaging entry points.
- `RepoBar.Tests`: unit and integration tests ported from the Swift test suite.

The .NET domain model should mirror the current provider-neutral Swift direction:

- Port `SourceControlProvider` behavior from `Sources/RepoBarCore/Models/SourceControlProvider.swift:3`.
- Port `RepositoryService` as the main service contract from `Sources/RepoBarCore/API/RepositoryService.swift:30`.
- Port router-style provider dispatch from `Sources/RepoBarCore/API/RepositoryServiceRouter.swift:3`.
- Port provider capability gates from `Sources/RepoBarCore/Models/ProviderCapabilities.swift:3`.
- Use `Sources/RepoBarCore/API/GitLabClient.swift:3` as the GitLab.com behavior reference.

## Implementation Plan

### Phase 1: Shared Core Port

- Define provider-neutral models for repositories, repository identity, users, releases, issues, merge/pull requests, commits, branches, tags, contributors, CI runs, activity, rate-limit diagnostics, and unsupported features.
- Preserve RepoBar’s current identity model semantics: provider, host, stable ID, namespace path, repository name, path with namespace, web URL, and API URL.
- Implement feature capability checks so unavailable Linux/GitLab sections are hidden or disabled instead of rendered as errors.
- Port sorting, filtering, pin/hidden repository rules, repository display limits, local-project matching rules, and menu/dashboard view-model logic.
- Keep GitHub-specific terms out of shared abstractions except where the feature is actually GitHub-only.

### Phase 2: Provider Clients

- Implement GitHub.com first with current dashboard parity: repository list, repo details, issues, pull requests, releases, branches, tags, commits, workflow runs, contributors, contents, traffic where available, and rate-limit diagnostics.
- Implement GitLab.com next using GitLab REST API behavior already represented in `GitLabClient`: projects, issues, merge requests, releases, branches, tags, commits, contributors, repository tree, raw file contents, and current user.
- Represent GitLab unsupported features explicitly: contribution calendar, traffic stats, discussions, and any unavailable pipeline details.
- Share HTTP concerns through a common request runner: auth headers, JSON decoding, pagination, conditional requests where available, retry/backoff, diagnostics, and redacted logging.
- Keep provider clients read-only for v1.

### Phase 3: Linux Platform Adapters

- Store tokens with Secret Service/libsecret by default; allow an explicit debug file-backed store for local development and tests.
- Follow XDG paths: settings under `$XDG_CONFIG_HOME/RepoBar` or `~/.config/RepoBar`, cache under `$XDG_CACHE_HOME/RepoBar` or `~/.cache/RepoBar`, and app data under `$XDG_DATA_HOME/RepoBar` or `~/.local/share/RepoBar`.
- Use StatusNotifierItem/AppIndicator for tray integration where available.
- Provide a normal launcher/window fallback when tray support is missing, disabled, or blocked by the desktop environment.
- Implement launch-at-login with a `.desktop` file in the XDG autostart directory.
- Discover `git` from `PATH` and allow a user-configured executable path.
- Open repository URLs and local paths through `xdg-open`.

### Phase 4: Avalonia Desktop UI

- Build a tray-first experience for supported desktops: click tray icon to show a compact dashboard; right-click to show account, refresh, settings, update/help, and quit actions.
- Build a no-tray fallback: a normal desktop window with the same dashboard and a background refresh loop only while the app is running.
- Implement a dashboard window with repository cards, issue/MR counts, CI status, releases, local status, activity, provider labels, and capability-aware section visibility.
- Implement settings for provider selection, host/account configuration, token method, refresh interval, visible/pinned/hidden repositories, local projects root, auto-sync, cache controls, diagnostics, startup behavior, and tray fallback preferences.
- Implement login flows for PAT first, then OAuth if the provider setup requires it.
- Keep the UI responsive by loading from cache first and refreshing in the background.

### Phase 5: Packaging And Distribution

- Ship AppImage first because it is the lowest-friction cross-distro artifact for early testing.
- Add `.deb` and `.rpm` packages after AppImage behavior is stable.
- Keep Flatpak optional because sandboxing can complicate local Git discovery, arbitrary project-folder access, credential-store access, and background/tray behavior.
- Include desktop metadata: `.desktop` launcher, app icon, MIME-free install, and clean uninstall behavior.
- Decide update mechanism after packaging is proven; do not port Sparkle because it is macOS-specific.

## Acceptance Criteria

- Ubuntu LTS GNOME with AppIndicator extension shows a working RepoBar tray icon.
- KDE Plasma shows a working RepoBar tray icon through native StatusNotifier support.
- GNOME without tray support falls back to a normal desktop window without crashing or hiding the app.
- The user can authenticate with GitHub.com and GitLab.com using PAT, with tokens stored securely through Secret Service where available.
- The app lists accessible repositories and shows repo details, issues/MRs, releases, branches, tags, commits, and local Git status.
- Unsupported provider features are hidden or labeled unavailable without noisy errors.
- Settings and cache persist across restarts in XDG-appropriate locations.
- Autostart works after login when enabled.
- AppImage install/run works first; `.deb` and `.rpm` packages work once added.

## Test Plan

- Port Swift tests as behavior specs for provider mapping, repository identity, settings migration/defaults, cache behavior, local Git parsing, filtering, sorting, and unsupported-feature handling.
- Add unit tests for GitHub and GitLab request mapping using deterministic fixtures.
- Add integration tests for Secret Service availability and fallback behavior, XDG paths, local Git discovery, `xdg-open`, `.desktop` autostart, tray initialization, no-tray fallback, notifications, AppImage launch, and package uninstall.
- Add UI smoke tests for first launch, login, refresh, repository dashboard, settings save, provider switching, GNOME fallback, and KDE tray behavior.

## Assumptions

- The macOS Swift app remains the production macOS app.
- The Linux port is a new implementation, not an attempt to compile the existing SwiftUI/AppKit app on Linux.
- C#/.NET with Avalonia UI is the default migration stack unless a future requirement forces a webview-based stack.
- v1 is read-only and provider-focused; write operations are out of scope.
