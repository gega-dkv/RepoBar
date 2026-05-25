# Windows Port Parity Matrix

Phase: `GOAL_PROMPT_WINDOWS_PHASE_0_DISCOVERY`

Purpose: define the Windows v1 behavior target before product code is written. The Windows port is a new C#/.NET + Avalonia implementation. The current Swift macOS app remains separate and is the behavior reference.

## Scope Decisions

| Decision | Windows v1 status | Rationale |
| --- | --- | --- |
| CLI parity | Include v1 as `RepoBar.Cli` | `docs/cli.md` states the CLI covers menubar/submenu data surfaces plus scriptable local actions/settings. It is central for diagnostics, cache inspection, archive management, and testable behavior. |
| GitHub Enterprise | Include v1 | README and spec list GitHub Enterprise support with custom host and trusted TLS. Keep it in scope so Windows does not regress an existing account mode. |
| GitHub App OAuth/PKCE | Include v1 after PAT bootstrap | `windows-migration.md` says PAT first, then OAuth if needed. The product reference supports GitHub App user-token OAuth with PKCE, refresh tokens, loopback callback, and GitHub App installation-bound access. PAT can be first implementation order, but OAuth remains v1. |
| GitLab.com | Include v1 | It is an initial provider in `windows-migration.md` and current `RepoBarCore` has a dedicated `GitLabClient`. |
| Bitbucket Cloud | Defer from Windows v1 UI, keep core extension point | Current Swift core has a `BitbucketClient`, but `windows-migration.md` names GitHub and GitLab.com first. Keep the provider model/capability shape ready, but do not block v1 on Bitbucket UI parity. |
| Forgejo/Gitea | Defer from Windows v1 UI, keep provider enum/capability shape | Current core models these providers, but no dedicated v1 migration requirement exists. Mark as planned later with capability gates. |
| Cache archives | Include v1 | README and `docs/cache.md` document archive-backed fallback as current behavior, and CLI exposes archive commands. Excluding it would weaken offline/rate-limited behavior. |
| Typed GitHub reference monitor | Defer from Windows v1 | Current README marks it optional and macOS Accessibility-dependent. Windows global-monitor permissions need a separate design. Keep translation/cache lookup models portable where cheap, but do not ship global monitoring in v1. |
| Packaging route | Decide in Phase 8 after proof | Test MSIX first against tray, autostart, local Git, arbitrary folder access, shell launch, Credential Manager, update behavior, and uninstall persistence. Use WiX/Inno Setup if MSIX blocks required behavior. |
| Update mechanism | Defer decision to Phase 8 | Do not port Sparkle. Pick a Windows update mechanism only after packaging is proven. |
| Write operations | Keep provider clients read-only for v1 | Matches `windows-migration.md`. Local Git safe-sync remains allowed because it is local workflow behavior, not provider mutation. |

## Parity Matrix

| Feature | Swift/product reference | Windows implementation target | Test target | Status |
| --- | --- | --- | --- | --- |
| Separate macOS app boundary | `Package.swift`; `windows-migration.md`; `linux-migration.md` | New `.NET` solution beside existing Swift app; no conversion of SwiftUI/AppKit app | Repo layout check; no Swift product target regressions | v1 hard constraint |
| Solution shape | `windows-migration.md`; `linux-migration.md` | `RepoBar.Core`, `RepoBar.Desktop`, `RepoBar.Cli`, `RepoBar.Tests` | Build/test script smoke checks | v1 |
| Provider model | `SourceControlProvider.swift`; `ProviderCapabilities.swift`; `RepositoryHost.swift` | C# provider enum, host model, capabilities model, unsupported feature model | Unit tests for labels, defaults, gates | v1 |
| Repository identity | `RepositoryIdentity.swift`; `Repository.swift`; `RepositoryFactoryTests.swift`; `RepositoryIdentityTests.swift` | Preserve provider, host, stable ID, namespace path, repo name, path with namespace, web URL, API URL | Equality/mapping tests | v1 |
| Repository service contract | `RepositoryService.swift`; `RepositoryServiceRouter.swift`; `RepositoryServiceTests.swift` | Shared async service interface and provider router | Router dispatch and unsupported-feature tests | v1 |
| GitHub.com repositories | `GitHubClient.swift`; `GitHubRestAPI.swift`; `RepositoryMappingTests.swift` | Read accessible repos, details, search, default/recent/activity lists | Fixture-backed mapping and pagination tests | v1 |
| GitHub Enterprise | README auth section; `UserSettings.swift`; `OAuthLoginFlow.swift`; `GitHubClient` host support | Custom web/API host, trusted TLS, PAT and OAuth host persistence | Host validation, API URL, auth tests | v1 |
| GitHub App OAuth/PKCE | `PKCE.swift`; `LoopbackServer.swift`; `OAuthLoginFlow.swift`; `OAuthTokenRefresher.swift`; `TokenStore.swift` | Browser OAuth with PKCE, loopback callback, refresh token handling | PKCE, callback parse/state, refresh tests | v1 |
| PAT auth | `PATAuthenticator.swift`; `ProviderTokenAuthenticator.swift`; `TokenStorePATTests.swift` | PAT login for GitHub/GitLab first implementation path | PAT storage/auth tests | v1 |
| GitLab.com client | `GitLabClient.swift`; `GitLabModels.swift`; `GitLabRequestRunner.swift`; `GitLabClientTests.swift` | Projects, current user, issues, MRs, releases, branches, tags, commits, contributors, tree, raw content, pipelines where supported | Fixture mapping, pagination, auth header, unsupported tests | v1 |
| Bitbucket Cloud | `BitbucketClient.swift`; `BitbucketRequestRunner.swift`; `BitbucketClientTests.swift` | Keep models/provider extension point; defer full desktop parity | Capability-gate tests only in v1 | Deferred |
| Forgejo/Gitea | `SourceControlProvider.swift`; `ProviderCapabilities.swift`; memory from provider work | Keep provider enum and compatible capability definitions; no v1 client/UI | Capability defaults tests | Deferred |
| GitHub repo card data | README "What It Shows"; `RepositoryDisplayModel.swift`; `RepoCardView.swift` | Avalonia repo cards with issues, PRs, stars, forks, latest activity, CI, release, traffic, heatmap, local state, errors | View-model tests and UI smoke | v1 |
| Rich repo details/submenus | README repo submenu; `RepoSubmenuBuilder.swift`; recent menu coordinators/views | Compact detail panels or submenus for recent issues, PRs/MRs, releases, CI, discussions if supported, tags, branches, contributors, commits, activity, changelog | View-model and UI smoke tests | v1 |
| Discussions | `recentDiscussions` in `RepositoryService`; `DiscussionMenuItemView.swift` | GitHub only; capability-gated unavailable for GitLab/Bitbucket | Capability and UI unavailable tests | v1 GitHub only |
| Changelog preview | `ChangelogMenuCoordinator.swift`; `ChangelogParser.swift`; `MarkdownBlockParser.swift`; `ChangelogParserTests.swift` | Local/remote changelog preview and markdown parsing | Parser and display tests | v1 |
| Contribution header | README; `ContributionHeaderView.swift`; `AppState+Contributions.swift`; `ContributionHeaderTests.swift` | GitHub contribution header if enabled; hide for unsupported providers | Cache/parse/display tests | v1 GitHub only |
| Heatmap | `HeatmapView.swift`; `HeatmapRasterView.swift`; `HeatmapSpan.swift`; heatmap tests | Provider-aware heatmap with GitHub data and hidden/unavailable state elsewhere | Binning, span, sizing tests | v1 GitHub only |
| Rate-limit status | README; `RateLimitStatusIconRenderer.swift`; `RateLimitDisplayState.swift`; `RateLimitStatusFormatter.swift`; `RateLimitJuice.swift`; cache docs | Tray/menu rate-limit meter and status panel using live + persisted state | Formatter, persistence, UI smoke tests | v1 |
| Refresh scheduler | `RefreshScheduler.swift`; `AppState+Refresh.swift`; `docs/spec.md` refresh strategy | Configurable interval, manual refresh, backoff-aware background refresh | Scheduler/backoff tests | v1 |
| Repository visibility | README repository browser; `RepoVisibility.swift`; `RepoSettingsView.swift`; `RepositoryFilter.swift`; `VisibilityTests.swift` | Visible/pinned/hidden states, manual rules, filters, display limit, owner filter | Filtering/visibility tests | v1 |
| Repository autocomplete/browser | `RepoAutocompleteWindowView.swift`; `RepoAutocompleteScoring.swift`; `RepoAutocompleteSuggestions.swift`; tests | Searchable repo browser/settings UI | Scoring and UI smoke tests | v1 |
| Local project scanning | README local projects; `docs/reposync.md`; `LocalProjectsService.swift`; `LocalGitService.swift` | Scan selected root, skip hidden/symlink dirs, map by folder/origin, cache discovery/status | Local service tests with fixtures | v1 |
| Local Git status | `LocalRepoStatus.swift`; `LocalGitMenuCoordinator.swift`; local git tests | Current branch, local/remote branch list, upstream, ahead/behind, dirty summary, worktree state, sync state | Local status tests | v1 |
| Local safe sync | `docs/reposync.md`; `LocalRepoManager.swift`; `LocalActionsCommands.swift` | Fast-forward-only sync for clean repos; no force push/reset/discard in automatic flows | Sync guardrail tests | v1 |
| Local CLI actions | `docs/cli.md`; `LocalActionsCommands.swift` | Include branch/worktree listing, checkout/open commands, and explicit confirmation for destructive operations | CLI parsing/confirmation tests | v1 CLI |
| Windows credential storage | `docs/auth-storage.md`; `TokenStore.swift`; `TokenStoreTests.swift` | Windows Credential Manager for release; file-backed debug store via explicit setting/env | Credential selection and file permission tests | v1 |
| Settings storage | `UserSettings.swift`; `SettingsStore.swift`; `SettingsStoreTests.swift` | JSON/settings store under `%AppData%\RepoBar` | Defaults/migration/path tests | v1 |
| Cache storage | `docs/cache.md`; `RepoBarCacheDatabase.swift`; cache tests | SQLite under `%LocalAppData%\RepoBar`; REST, GraphQL, rate-limit tables | DB read/write/migration tests | v1 |
| Cache-first startup | README cache section; `RepoDetailCacheStore.swift`; `RepositoryPipeline.swift` | Render cached repo rows first, then refresh in background | Pipeline/cache fallback tests | v1 |
| Archive sources | `docs/cache.md`; `GitHubArchiveStore.swift`; `GitHubArchiveImporter.swift`; `ArchiveCommands.swift` | Discrawl-compatible source config, validate, import/update, stale issue/PR fallback | Import, validation, fallback, CLI tests | v1 |
| CLI command surface | `docs/cli.md`; `Sources/repobarcli`; `repobarcliTests` | `RepoBar.Cli` sharing core services and Windows storage paths | CLI parsing, JSON/plain output, e2e tests | v1 |
| Typed GitHub reference monitor | README; `GitHubReferenceMonitor.swift`; `GitHubRestAPI+ReferenceLookup.swift`; `GitHubReferenceMonitorTests.swift`; `ReferenceTranslateCommand.swift` | Defer global monitor; keep CLI translation/reference lookup if cheap and cache-backed | Deferred global monitor; optional CLI tests | Deferred |
| Settings UI | `SettingsView.swift`; `GeneralSettingsView.swift`; `AccountSettingsView.swift`; `RepoSettingsView.swift`; `DisplaySettingsView.swift`; `AdvancedSettingsView.swift`; `GitHubArchiveSettingsSection.swift` | Avalonia tabs for general, accounts, repositories, display, local projects, cache/archive, diagnostics, startup/update | Settings save/load UI smoke | v1 |
| Tray behavior | README; `StatusBarMenuManager.swift`; `StatusBarMenuBuilder.swift`; `MenuBarExtraAccess` use | Windows tray icon with left-click dashboard and right-click command menu | Manual/integration smoke | v1 |
| Native notifications | `MenuNotifications.swift`; `LocalSyncNotifier.swift`; `docs/reposync.md` | Windows notifications for successful local sync and relevant app events | Adapter integration smoke | v1 |
| Launch at login | `LaunchAtLoginHelper.swift`; `docs/spec.md` | Windows autostart compatible with chosen packaging route | Adapter/integration test | v1 |
| Shell/browser launch | `AppActions.swift`; `RepoWebURLBuilder.swift`; local menu actions | Open URLs and local paths through Windows shell APIs | Adapter tests | v1 |
| Diagnostics/logging | `DiagnosticsLogger.swift`; `RepoBarLogging.swift`; settings docs | Redacted diagnostics, file logging toggle, no secret leakage | Redaction and diagnostics tests | v1 |
| Accessibility | `docs/spec.md` TODO; current macOS a11y gaps | Basic keyboard/focus/accessibility labels for Windows UI; global reference monitor permissions deferred | UI smoke/manual checklist | v1 basic, monitor deferred |
| Packaging | `windows-migration.md`; `docs/release.md` | Signed Windows installer; MSIX only if required behaviors work, otherwise WiX/Inno Setup | Install/upgrade/uninstall checklist | v1 |
| Updates | Sparkle references in `docs/release.md`; `SparkleController.swift` | Do not port Sparkle; choose Windows updater after packaging proof | Phase 8 decision record | Deferred to Phase 8 |
| Release artifacts | `Scripts/release.sh`; `docs/release.md` | Publish Windows artifacts alongside macOS without changing macOS release flow | Release checklist verification | v1 |
| Security/privacy | `docs/spec.md`; `docs/auth-storage.md`; logging/token tests | No secrets in logs, diagnostics, CLI, crash output, cache files, installer logs | Security review checklist/tests | v1 |

## Phase 0 Gate

Phase 0 is satisfied when this file exists, `windows-port-progress.md` records the commands and decision log, and the v1/deferred scope above is accepted as the implementation target for Phase 1.

## Phase 9 Resolution

Phase 9 repository-verifiable hardening is recorded in `windows-phase9-parity-audit.md`.

Resolved for Windows v1 in this checkout:

- New .NET/Avalonia solution with shared core, desktop shell, CLI, and tests.
- GitHub.com, GitHub Enterprise host configuration, and GitLab.com read-only provider surfaces.
- Capability gates and explicit unsupported states for deferred providers/features.
- Settings, cache, rate-limit, archive, and credential storage semantics.
- Cache-first startup, stale cache fallback, `304` cached-body handoff, and persisted rate-limit diagnostics.
- Configurable refresh scheduler policy with manual refresh and backoff handling.
- CLI coverage for documented repository list filtering/sorting flags plus age, release, event, owner, mine, scope, forks, archived, and pinned filters; repo traffic/heatmap/release detail flags; discussions; contribution heatmap; manual refresh; changelog/markdown rendering; local actions; cache/rate-limit diagnostics; documented appearance/activity/heatmap/local settings keys; repository visibility; auth; and archives.
- Repository browser/autocomplete scoring and settings-view rows.
- Provider-backed cache-first desktop dashboard data source.
- Local checkout changelog preview using the shared parser.
- Secret redaction for provider diagnostics and CLI auth flows.
- Local Git scan/status/branch/worktree/checkout/open/scan-sync/safe-sync state coverage.
- Explicit-confirmation local rebase/reset CLI guardrails.
- WiX MSI plus ZIP package route and GitHub Releases MSI-upgrade update route.

Deferred from Windows v1:

- Bitbucket Cloud desktop parity.
- Forgejo/Gitea desktop parity.
- Global typed GitHub reference monitor.
- Windows telemetry or crash upload service; none is implemented.

Requires Windows 11 runtime evidence before release:

- Tray display and high-DPI icon rendering.
- Native notifications and launch-at-login after reboot.
- Windows Credential Manager in release mode.
- Installed shell URL/path launch.
- MSI clean install, upgrade, uninstall, and data-preservation behavior.
- Live GitHub.com, GitHub Enterprise, and GitLab.com provider checks with real tokens.
- Real restart/upgrade persistence for settings, cache, and credentials.
