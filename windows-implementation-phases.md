# Windows Implementation Phases

This file expands `windows-migration.md` into implementation-sized phases. Each phase includes a goal prompt marker that can be copied into a fresh agent run. The Windows port remains a new C#/.NET + Avalonia implementation; the current Swift macOS app is the behavior reference, not a runtime dependency.

## Master Goal Prompt

<!-- GOAL_PROMPT_WINDOWS_MASTER_IMPLEMENTATION -->

Use this prompt to drive the full Windows implementation from start to finish:

```text
Implement the RepoBar Windows port from windows-implementation-phases.md, using windows-migration.md as the migration direction and the current Swift macOS app as the product behavior reference.

Work phase by phase in this order:

1. GOAL_PROMPT_WINDOWS_PHASE_0_DISCOVERY
2. GOAL_PROMPT_WINDOWS_PHASE_1_SOLUTION
3. GOAL_PROMPT_WINDOWS_PHASE_2_CORE_DOMAIN
4. GOAL_PROMPT_WINDOWS_PHASE_3_PROVIDER_CLIENTS
5. GOAL_PROMPT_WINDOWS_PHASE_4_STORAGE_AND_CACHE
6. GOAL_PROMPT_WINDOWS_PHASE_5_WINDOWS_ADAPTERS
7. GOAL_PROMPT_WINDOWS_PHASE_6_AVALONIA_UI
8. GOAL_PROMPT_WINDOWS_PHASE_7_CLI_AND_DIAGNOSTICS
9. GOAL_PROMPT_WINDOWS_PHASE_8_PACKAGING_AND_RELEASE
10. GOAL_PROMPT_WINDOWS_PHASE_9_PARITY_HARDENING

Hard constraints:

- Keep the macOS Swift app separate and production-ready; do not convert the existing SwiftUI/AppKit app into a cross-platform app.
- Build the Windows port as a new C#/.NET application using Avalonia UI, with platform boundaries that can later support Linux.
- Treat README.md, docs/spec.md, docs/cache.md, docs/cli.md, docs/reposync.md, docs/auth-storage.md, docs/release.md, and the current Sources/ and Tests/ trees as the behavior reference.
- Before writing product code, complete Phase 0 and make explicit v1/deferred decisions for CLI parity, GitHub Enterprise, GitHub App OAuth/PKCE, cache archives, Bitbucket, Forgejo/Gitea, the typed GitHub reference monitor, packaging route, and update mechanism.
- Keep provider clients read-only for v1 unless the phase 0 parity matrix explicitly says otherwise.
- Use capability gates for provider/platform gaps; unavailable features should be hidden or clearly labeled unavailable, not rendered as noisy errors.
- Keep tokens, client secrets, PATs, OAuth refresh tokens, installer logs, diagnostics, CLI output, and crash output free of secrets.
- Use Windows Credential Manager for release credentials and an explicit file-backed debug credential store for local development and tests.
- Use Windows-appropriate settings/cache paths: %AppData%\RepoBar for settings and %LocalAppData%\RepoBar for cache.
- Prefer shared RepoBar.Core behavior over duplicating business logic in the Avalonia UI or CLI.
- Add focused tests with each phase. Do not move to the next implementation phase until the current phase completion gate is satisfied or the gap is explicitly documented as blocked/deferred.

Execution rules:

- Start each phase by reading the corresponding section in windows-implementation-phases.md.
- Keep a phase progress checklist in the repo and update it as work lands.
- After each phase, record the commands run, the tests that passed, the known gaps, and whether the phase gate is satisfied.
- When package, update, or external API details are needed, fetch current official documentation before implementing those details.
- Preserve unrelated files and existing user/agent changes.
- At the end of Phase 9, update windows-migration.md and project docs with the implementation decisions that were proven during the port.
```

## Goal Prompt Markers

Use these markers to start a phase:

- `GOAL_PROMPT_WINDOWS_MASTER_IMPLEMENTATION`
- `GOAL_PROMPT_WINDOWS_PHASE_0_DISCOVERY`
- `GOAL_PROMPT_WINDOWS_PHASE_1_SOLUTION`
- `GOAL_PROMPT_WINDOWS_PHASE_2_CORE_DOMAIN`
- `GOAL_PROMPT_WINDOWS_PHASE_3_PROVIDER_CLIENTS`
- `GOAL_PROMPT_WINDOWS_PHASE_4_STORAGE_AND_CACHE`
- `GOAL_PROMPT_WINDOWS_PHASE_5_WINDOWS_ADAPTERS`
- `GOAL_PROMPT_WINDOWS_PHASE_6_AVALONIA_UI`
- `GOAL_PROMPT_WINDOWS_PHASE_7_CLI_AND_DIAGNOSTICS`
- `GOAL_PROMPT_WINDOWS_PHASE_8_PACKAGING_AND_RELEASE`
- `GOAL_PROMPT_WINDOWS_PHASE_9_PARITY_HARDENING`

## Phase 0: Discovery And Current-Parity Audit

<!-- GOAL_PROMPT_WINDOWS_PHASE_0_DISCOVERY -->

Goal prompt:

```text
Read windows-migration.md, README.md, docs/spec.md, docs/cache.md, docs/cli.md, docs/reposync.md, docs/auth-storage.md, and docs/release.md. Produce a Windows parity matrix for the C#/.NET Avalonia port: feature, Swift source reference, Windows implementation target, test target, and v1/in-later status. Do not write product code in this phase.
```

Implementation tasks:

- Inventory current macOS app surfaces: tray/menu dashboard, repo cards, repo submenus, settings tabs, auth, local projects, cache/archive, CLI, rate-limit status, reference monitor, and release flow.
- Map Swift source references from `Sources/RepoBar/`, `Sources/RepoBarCore/`, and `Sources/repobarcli/` to planned .NET areas.
- Decide v1 scope explicitly for GitHub.com, GitHub Enterprise, GitLab.com, Bitbucket, Forgejo/Gitea, CLI commands, archive support, and typed GitHub reference monitoring.
- Record which features are capability-gated, unavailable, or deferred.

Completion gate:

- A parity matrix exists and identifies every v1 feature before scaffolding begins.

## Phase 1: .NET Solution Scaffold

<!-- GOAL_PROMPT_WINDOWS_PHASE_1_SOLUTION -->

Goal prompt:

```text
Create the Windows/Linux-capable .NET solution described by windows-migration.md. Scaffold RepoBar.Core, RepoBar.Desktop, RepoBar.Cli if included in v1, and RepoBar.Tests. Use current LTS .NET and Avalonia for the desktop project. Add build/test scripts and CI-friendly commands, but do not port behavior yet.
```

Implementation tasks:

- Create solution layout:
  - `RepoBar.Core`
  - `RepoBar.Desktop`
  - `RepoBar.Cli` if Phase 0 keeps CLI parity in v1
  - `RepoBar.Tests`
- Add common project settings: nullable references, analyzers, deterministic builds, centralized package versions, and formatting.
- Add Avalonia desktop entry point and a minimal empty window/tray placeholder.
- Add script wrappers equivalent to the existing `pnpm` workflow for restore, format, lint/analyze, test, build, run, and package.
- Add initial test harness and fixture conventions.

Completion gate:

- Restore, build, and test commands pass on a Windows development machine.
- No macOS Swift files are modified except documentation if needed.

## Phase 2: Core Domain Port

<!-- GOAL_PROMPT_WINDOWS_PHASE_2_CORE_DOMAIN -->

Goal prompt:

```text
Port RepoBar's provider-neutral domain model into RepoBar.Core. Use the Swift files listed in windows-migration.md plus the current Models, Settings, RepositoryService, RepositoryServiceRouter, ProviderCapabilities, sorting/filtering, heatmap, rate-limit, and local Git models as behavior references. Add unit tests for every mapper and rule.
```

Implementation tasks:

- Port provider-neutral models for repository identity, host, provider, user identity, releases, issues, pull/merge requests, commits, branches, tags, contributors, CI runs, traffic, activity, heatmap, rate-limit state, and unsupported features.
- Preserve identity semantics: provider, host, stable ID, namespace path, repo name, path with namespace, web URL, API URL.
- Port user settings models, repository visibility rules, pinned/hidden state, display limits, filters, sort keys, appearance options, refresh intervals, local-project settings, logging/diagnostics settings, and archive settings if in v1.
- Port capability gates so unavailable provider sections are hidden or explicitly marked unavailable.
- Port formatting helpers for relative dates, statistics, paths, rate-limit state, releases, heatmap spans, markdown/changelog parsing, and menu row display data.

Completion gate:

- Unit tests cover identity equality, provider capabilities, settings defaults/migration, repo filtering/sorting, heatmap bucketing, changelog parsing, rate-limit display, and unsupported-feature behavior.

## Phase 3: Provider Clients

<!-- GOAL_PROMPT_WINDOWS_PHASE_3_PROVIDER_CLIENTS -->

Goal prompt:

```text
Implement read-only GitHub.com and GitLab.com provider clients in RepoBar.Core. Mirror the current Swift RepositoryService contract, RepositoryServiceRouter behavior, GitHub REST/GraphQL behavior, GitLab REST behavior, pagination, auth headers, retry/backoff, diagnostics, and redacted logging. Use deterministic fixtures for tests.
```

Implementation tasks:

- Implement a common HTTP request runner with auth header injection, JSON decoding, pagination, ETag support where available, retry/backoff, rate-limit capture, diagnostics, and redacted logging.
- Implement GitHub.com:
  - repository list and search
  - repository details
  - issues and pull requests
  - releases, branches, tags, commits, contributors
  - workflow runs/checks
  - contents/changelog fetch
  - traffic where permissions allow
  - contribution heatmap if kept in v1
  - rate-limit diagnostics
- Implement GitHub Enterprise if kept in v1: custom host, trusted TLS, OAuth/PAT handling, API path differences, and user-facing host validation.
- Implement GitLab.com:
  - projects, current user, issues, merge requests, releases, branches, tags, commits, contributors, tree, raw file contents, and pipelines where supported
  - explicit unsupported states for traffic, contribution calendar, discussions, and incomplete pipeline details
- Wire provider dispatch through a router-style service.

Completion gate:

- Fixture-backed tests cover GitHub and GitLab mapping, pagination, auth header semantics, rate-limit/backoff state, unavailable features, and router dispatch.

## Phase 4: Storage, Settings, Cache, And Archives

<!-- GOAL_PROMPT_WINDOWS_PHASE_4_STORAGE_AND_CACHE -->

Goal prompt:

```text
Implement Windows storage for settings, credentials, persistent API cache, rate-limit state, and optional GitHub archive imports. Use windows-migration.md plus docs/cache.md and docs/auth-storage.md as the behavior reference. Keep release credentials in Windows Credential Manager and debug credentials file-backed.
```

Implementation tasks:

- Store release tokens in Windows Credential Manager.
- Support explicit file-backed debug auth storage for local development and tests.
- Store settings under `%AppData%\RepoBar`.
- Store cache data under `%LocalAppData%\RepoBar`.
- Implement persistent REST response/ETag storage, GraphQL response storage, and rate-limit storage.
- Preserve cache-first startup behavior: render from cache first, refresh in the background.
- If archive support is v1, port Discrawl-compatible snapshot configuration, validation, import, status, stale fallback reads, and explicit update policy.
- Add cache clear, diagnostics, and import status APIs for UI/CLI consumption.

Completion gate:

- Tests cover storage path selection, credential store selection, debug file store behavior, settings persistence, cache read/write, stale fallback behavior, archive validation/import if included, and rate-limit persistence.

## Phase 5: Windows Platform Adapters

<!-- GOAL_PROMPT_WINDOWS_PHASE_5_WINDOWS_ADAPTERS -->

Goal prompt:

```text
Implement Windows-specific platform adapters for RepoBar.Desktop: tray icon, native notifications, autostart, git.exe discovery, shell URL/path launch, local project scanning, and safe local sync behavior. Keep the adapters behind interfaces so Linux can use different implementations later.
```

Implementation tasks:

- Implement `.ico` tray asset loading.
- Implement Windows tray behavior for left click and right click.
- Implement native Windows notifications.
- Implement launch-at-login using the mechanism compatible with the selected package route.
- Discover `git.exe` from `PATH`, Git for Windows default install paths, and a user-configured executable path.
- Port local project scanning, local repo status, branch/upstream/ahead/behind/dirty/worktree state, checkout/open actions, and safe auto-sync policy.
- Open repository URLs and local paths with Windows shell APIs.
- Add adapters for browser launch and OAuth loopback callback.

Completion gate:

- Integration tests or manual verification cover tray initialization, notifications, autostart, Git discovery, URL/path launch, local repo scan, local repo status, and debug auth login flow.

## Phase 6: Avalonia Desktop UI

<!-- GOAL_PROMPT_WINDOWS_PHASE_6_AVALONIA_UI -->

Goal prompt:

```text
Build the Avalonia tray-first UI with behavior parity to the current macOS RepoBar menu and settings. Use cache-first rendering, capability-aware sections, provider labels, and the same repository visibility/filtering concepts. Keep UI logic thin over RepoBar.Core services.
```

Implementation tasks:

- Build tray-first behavior:
  - left click opens compact dashboard
  - right click opens account, refresh, settings, update/help, and quit actions
- Build repository dashboard with cards for provider, repo identity, issue/MR counts, CI status, latest release, recent activity, traffic if available, heatmap if enabled, local status, errors, and rate-limit state.
- Build rich repository submenus or equivalent compact detail panels for issues, PRs/MRs, releases, CI runs, discussions where available, tags, branches, contributors, commits, activity, changelog preview, pin/hide actions, and local actions.
- Build settings:
  - General
  - Accounts/providers/hosts
  - Repositories and visibility
  - Display/appearance
  - Local projects
  - Cache/archive
  - Diagnostics
  - Startup/update
- Implement PAT login first; add OAuth if Phase 0 keeps it in v1.
- Ensure unsupported provider features are hidden or clearly unavailable without noisy errors.

Completion gate:

- UI smoke tests or scripted manual checks cover first launch, login, refresh, repository dashboard, repo details, settings save, provider switching, cached startup, and unsupported GitLab feature display.

## Phase 7: CLI And Diagnostics

<!-- GOAL_PROMPT_WINDOWS_PHASE_7_CLI_AND_DIAGNOSTICS -->

Goal prompt:

```text
If CLI parity is in v1, implement a Windows RepoBar.Cli that mirrors the useful Swift repobar commands for diagnostics and automation. It must share RepoBar.Core services, credential selection rules, cache paths, settings, and provider routing with the desktop app.
```

Implementation tasks:

- Implement `repos`, `repo`, `issues`, `pulls`/`merge-requests`, `releases`, `ci`, `tags`, `branches`, `contributors`, `commits`, `activity`, `local`, `cache status`, `cache clear`, `rate-limits`, `settings show/set`, `login`, `logout`, and `status` as scoped by Phase 0.
- Implement `archives` commands if archive support is v1.
- Support `--json`, `--plain`, and no-color output.
- Keep output deterministic enough for test fixtures.
- Share auth, settings, cache, and provider routing with the desktop app.

Completion gate:

- CLI tests cover command parsing, JSON/plain output, auth selection, cache diagnostics, provider routing, local repo commands, and error output.

## Phase 8: Packaging, Signing, Updates, And Release Flow

<!-- GOAL_PROMPT_WINDOWS_PHASE_8_PACKAGING_AND_RELEASE -->

Goal prompt:

```text
Package RepoBar.Desktop for Windows 11+. Evaluate MSIX against tray, autostart, local Git access, shell launch, and credential behavior. Use MSIX only if it does not block required behavior; otherwise use WiX or Inno Setup. Add signing, installer upgrade/uninstall checks, and release documentation.
```

Implementation tasks:

- Pick package route after proving tray, autostart, local Git, shell launch, and Credential Manager behavior.
- Produce signed builds with a stable app ID.
- Include clean install, upgrade, and uninstall behavior.
- Do not delete user settings/cache on uninstall unless explicitly requested.
- Decide update mechanism after installer behavior is proven; do not port Sparkle.
- Publish Windows artifacts alongside macOS artifacts without changing the current macOS release flow.
- Add release checklist documentation for Windows.

Completion gate:

- A Windows 11 user can install, launch, authenticate, view repos, enable autostart, upgrade, and uninstall cleanly.

## Phase 9: Parity Hardening

<!-- GOAL_PROMPT_WINDOWS_PHASE_9_PARITY_HARDENING -->

Goal prompt:

```text
Run the Windows port against the parity matrix from Phase 0. Close v1 gaps, add missing tests, polish degraded states, verify cache-first behavior under offline/rate-limited conditions, and update documentation with exact supported and unsupported behavior.
```

Implementation tasks:

- Verify GitHub and GitLab happy paths.
- Verify offline, expired-token, rate-limited, missing-permission, empty-state, and unsupported-feature paths.
- Verify local Git behavior with clean, dirty, ahead, behind, divergent, worktree, missing upstream, and missing git executable states.
- Verify settings/cache/credential persistence across restart and upgrade.
- Verify high-DPI tray icon and compact dashboard layout on common Windows scaling factors.
- Verify no token or secret appears in logs, diagnostics, crash output, or CLI output.
- Update `windows-migration.md`, README/development docs, and release docs with actual decisions.

Completion gate:

- Phase 0 parity matrix is fully resolved for v1, with all deferred items documented.

## Missing Or Underspecified In windows-migration.md

These items are present in the current product/docs but are missing or too vague in `windows-migration.md`:

- **Current product parity matrix**: the migration plan names broad areas but does not require a feature-by-feature audit against `README.md`, `docs/spec.md`, and source modules before implementation.
- **CLI parity**: `Sources/repobarcli/` and `docs/cli.md` are not mentioned. The Windows plan should decide whether to ship `RepoBar.Cli` in v1 or explicitly defer it.
- **Persistent cache details**: the plan mentions cache abstractions but not the concrete REST ETag cache, GraphQL cache, persisted rate limits, cache-first menu seeding, stale fallback behavior, or cache status/clear surfaces from `docs/cache.md`.
- **GitHub archive support**: Discrawl-compatible archive sources, import/update policy, archive fallback reads, and archive settings are absent.
- **GitHub Enterprise**: current RepoBar supports GitHub Enterprise with custom host and trusted TLS. The Windows plan only says GitHub.com first.
- **GitHub App OAuth vs PAT scope**: the plan says PAT first and OAuth later, but it does not decide whether GitHub App user-token OAuth, loopback PKCE, refresh tokens, and GitHub App installation-bound access are v1 requirements.
- **Bitbucket and Forgejo/Gitea disposition**: the plan says other providers later, but current core has Bitbucket and provider-neutral routing. The Windows plan should explicitly mark these as deferred or planned phases.
- **Recent lists and rich repo submenus**: current UI includes recent issues, PRs, releases, CI runs, discussions, tags, branches, contributors, commits, activity, and changelog preview. The plan compresses this into "repo details" and should name the surfaces.
- **Contribution header and heatmap scope**: the plan mentions activity/heatmap only indirectly and does not decide whether account contribution headers, GitHub contribution heatmaps, and provider-specific heatmap availability are v1.
- **Typed GitHub reference monitor**: current README documents the optional reference monitor and Accessibility permission behavior. The Windows equivalent or explicit deferral is missing.
- **Local project safe-sync details**: the plan mentions local checkout state and Git discovery, but not fast-forward-only sync, dirty-file handling, worktrees, preferred paths, checkout/open actions, or destructive-action guardrails.
- **Settings model completeness**: the plan lists settings generally but omits archive settings, rate-limit meter display, activity scope, heatmap span/display, local worktree folder, terminal/open preferences, diagnostics logging, and repository host/account records.
- **Windows accessibility and global monitoring permissions**: there is no decision for keyboard shortcuts, global text/reference monitoring, or required Windows permissions if the reference monitor is ported.
- **Packaging decision criteria**: the plan says MSIX unless blocked, but should explicitly test tray behavior, autostart, local Git access, arbitrary local-project folders, shell launch, Credential Manager, update behavior, and uninstall persistence before committing.
- **Update mechanism candidates**: the plan correctly says not Sparkle, but does not list candidate Windows update approaches or decision criteria.
- **Telemetry/crash/error reporting stance**: diagnostics and redacted logging are mentioned, but there is no policy on crash reports or telemetry. If none, say none.
- **Security review checklist**: the plan says redacted logging, but does not require checks for token leakage in logs, CLI output, diagnostics, crash dumps, installer logs, or cache files.
- **Test environment matrix**: Windows 11 is named, but there is no matrix for Windows 11 versions, high-DPI scaling, ARM64/x64, standard vs admin user, corporate proxy/TLS, and Git for Windows installed/missing.
- **Linux sharing boundary**: the solution is Windows/Linux-capable, but the plan should say which projects are shared and which interfaces must remain platform-specific to avoid baking Windows assumptions into `RepoBar.Core`.
- **Documentation update gates**: the plan does not require updating `windows-migration.md`, README/development docs, release docs, or support docs once implementation decisions are proven.

## Suggested Update To windows-migration.md

Add a short "Scope Decisions Needed Before Phase 1" section to `windows-migration.md`:

- Is `RepoBar.Cli` part of Windows v1?
- Is GitHub Enterprise part of Windows v1?
- Is GitHub App OAuth/PKCE part of Windows v1, or is v1 PAT-only?
- Are cache archives part of Windows v1?
- Are Bitbucket and Forgejo/Gitea deferred?
- Is the typed GitHub reference monitor deferred?
- Which Windows package route passes tray/autostart/local Git/Credential Manager requirements?
- Which update mechanism replaces Sparkle?
