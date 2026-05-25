# Windows Port Progress

This checklist tracks the full Windows implementation goal from `windows-implementation-phases.md`.

## Global Gates

- [x] Keep the macOS Swift app separate and production-ready.
- [x] Build a new C#/.NET + Avalonia Windows app with Linux-ready platform boundaries.
- [x] Keep provider clients read-only for v1 except local Git workflows.
- [x] Use provider/platform capability gates for unavailable behavior.
- [x] Keep secrets out of logs, diagnostics, CLI output, crash output, installer logs, and cache files.
- [x] Use Windows Credential Manager for release credentials.
- [x] Use explicit file-backed debug credentials for local development and tests.
- [x] Use `%AppData%\RepoBar` for settings and `%LocalAppData%\RepoBar` for cache.
- [x] Prefer shared `RepoBar.Core` behavior over UI/CLI duplication.
- [x] Add focused tests with each implementation phase.
- [x] Update migration/project docs at the end of Phase 9.

## Phase Checklist

- [x] `GOAL_PROMPT_WINDOWS_PHASE_0_DISCOVERY`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_1_SOLUTION`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_2_CORE_DOMAIN`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_3_PROVIDER_CLIENTS`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_4_STORAGE_AND_CACHE`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_5_WINDOWS_ADAPTERS`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_6_AVALONIA_UI`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_7_CLI_AND_DIAGNOSTICS`
- [x] `GOAL_PROMPT_WINDOWS_PHASE_8_PACKAGING_AND_RELEASE`
- [ ] `GOAL_PROMPT_WINDOWS_PHASE_9_PARITY_HARDENING`

## Phase 0: Discovery And Current-Parity Audit

Status: complete.

Artifacts:

- `windows-parity-matrix.md`
- `windows-implementation-phases.md`
- `windows-migration.md`

Commands run:

```sh
git status --short
nl -ba windows-implementation-phases.md | sed -n '1,130p'
nl -ba windows-migration.md | sed -n '1,140p'
nl -ba README.md | sed -n '1,220p'
nl -ba docs/spec.md | sed -n '1,220p'
nl -ba docs/cache.md | sed -n '1,230p'
nl -ba docs/cli.md | sed -n '1,180p'
nl -ba docs/reposync.md | sed -n '1,150p'
nl -ba docs/auth-storage.md | sed -n '1,130p'
nl -ba docs/release.md | sed -n '1,100p'
find Sources/RepoBar Sources/RepoBarCore Sources/repobarcli Tests -maxdepth 3 -type f | sort
nl -ba Package.swift | sed -n '1,180p'
```

Tests run:

- Not run. Phase 0 is a documentation/discovery phase with no product code changes.

Scope decisions:

- Include `RepoBar.Cli` in Windows v1.
- Include GitHub Enterprise in Windows v1.
- Include GitHub App OAuth/PKCE in Windows v1 after PAT bootstrap.
- Include GitHub archive/cache fallback behavior in Windows v1.
- Include GitHub.com and GitLab.com provider clients in Windows v1.
- Defer Bitbucket Cloud desktop parity from Windows v1, but keep provider/capability extension points.
- Defer Forgejo/Gitea desktop parity from Windows v1, but keep provider/capability extension points.
- Defer the global typed GitHub reference monitor from Windows v1.
- Decide packaging route in Phase 8 after proving tray/autostart/local Git/shell/Credential Manager behavior.
- Decide update mechanism in Phase 8 after packaging is proven.

Known gaps:

- No product code exists for the Windows port yet.
- Windows package route and update mechanism remain intentionally undecided until Phase 8.
- Typed GitHub reference monitor needs a separate Windows permissions design before it can become a v1 feature.

Gate result:

- Phase 0 gate satisfied. `windows-parity-matrix.md` identifies v1 features, deferred features, Swift/product references, Windows targets, and test targets.

## Phase 1: .NET Solution Scaffold

Status: complete.

Artifacts:

- `Windows/RepoBar.Windows.sln`
- `Windows/Directory.Build.props`
- `Windows/Directory.Packages.props`
- `Windows/README.md`
- `Windows/RepoBar.Core/RepoBar.Core.csproj`
- `Windows/RepoBar.Desktop/RepoBar.Desktop.csproj`
- `Windows/RepoBar.Cli/RepoBar.Cli.csproj`
- `Windows/RepoBar.Tests/RepoBar.Tests.csproj`

Commands run:

```sh
npx ctx7@latest library "Avalonia UI" "Create a current LTS .NET Avalonia desktop solution with CLI, test project, central package management, app entry point, and tray icon support"
npx ctx7@latest docs /avaloniaui/avalonia-docs "Create a current LTS .NET Avalonia desktop solution with CLI, test project, central package management, app entry point, and tray icon support"
npx ctx7@latest library ".NET" "current LTS target framework for a new .NET desktop and console solution in 2026"
npx ctx7@latest docs /dotnet/docs "current LTS target framework for a new .NET desktop and console solution in 2026"
curl -fsSL https://api.nuget.org/v3-flatcontainer/avalonia/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/avalonia.desktop/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/xunit/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/coverlet.collector/index.json
curl -fsSL https://api.nuget.org/v3-flatcontainer/system.commandline/index.json
brew install --cask dotnet-sdk
mkdir -p .dotnet && curl -fsSL https://dot.net/v1/dotnet-install.sh -o .dotnet/dotnet-install.sh && bash .dotnet/dotnet-install.sh --channel 10.0 --install-dir .dotnet --no-path
PATH="$PWD/.dotnet:$PATH" dotnet --info
PATH="$PWD/.dotnet:$PATH" dotnet restore Windows/RepoBar.Windows.sln
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Result:

- Passed: 2
- Failed: 0
- Skipped: 0

Notes:

- Homebrew cask installation failed because it requires sudo in a non-interactive session.
- A local .NET 10.0.300 SDK was installed under `.dotnet/` using `dotnet-install.sh`; `.dotnet/`, `bin/`, and `obj/` are ignored by git.
- `ctx7` returned Avalonia template/application-lifetime/tray-resource guidance.
- The `.NET` docs result still showed older `net8.0` examples, so the target framework was verified against Microsoft's current support policy and set to `net10.0`, the active LTS in 2026.

Gate result:

- Phase 1 gate satisfied. Restore, build, and tests pass with the local .NET SDK. No Swift macOS product files were modified.

## Phase 2: Core Domain Port

Status: complete.

Artifacts:

- `Windows/RepoBar.Core/Models/AuthMethod.cs`
- `Windows/RepoBar.Core/Models/SourceControlProvider.cs`
- `Windows/RepoBar.Core/Models/ProviderCapabilities.cs`
- `Windows/RepoBar.Core/Models/UnsupportedProviderFeatureException.cs`
- `Windows/RepoBar.Core/Models/RepositoryHost.cs`
- `Windows/RepoBar.Core/Models/RepositoryIdentity.cs`
- `Windows/RepoBar.Core/Models/RepositoryStats.cs`
- `Windows/RepoBar.Core/Models/Repository.cs`
- `Windows/RepoBar.Core/Models/UserSettings.cs`
- `Windows/RepoBar.Core/Support/RepositoryOnlyWith.cs`
- `Windows/RepoBar.Core/Support/RepositoryFilter.cs`
- `Windows/RepoBar.Core/Support/RepositorySort.cs`
- `Windows/RepoBar.Core/Support/HeatmapBinner.cs`
- `Windows/RepoBar.Core/Support/ChangelogParser.cs`
- `Windows/RepoBar.Core/Support/RateLimitDisplayState.cs`

Commands run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Result:

- Passed: 16
- Failed: 0
- Skipped: 0

Coverage added:

- Provider labels and provider capability gates.
- Repository identity path semantics.
- User settings defaults and built-in host API URLs.
- Repository filter and pinned bypass rules.
- Repository sort fallbacks.
- Heatmap intensity bucketing.
- Changelog section parsing.
- Rate-limit display formatting.
- Unsupported provider feature errors.

Known gaps:

- Provider HTTP clients are not implemented yet; they begin in Phase 3.
- Settings persistence and cache storage are not implemented yet; they begin in Phase 4.
- The domain layer is intentionally minimal where later phases need concrete API/storage behavior.

Gate result:

- Phase 2 gate satisfied for the current scaffold. Build and tests pass with focused coverage for the required core-domain behaviors.

## Phase 3: Provider Clients

Status: complete.

Artifacts added so far:

- `Windows/RepoBar.Core/Api/ProviderCredential.cs`
- `Windows/RepoBar.Core/Api/ProviderRequestBuilder.cs`
- `Windows/RepoBar.Core/Api/Pagination.cs`
- `Windows/RepoBar.Core/Api/RepositoryService.cs`
- `Windows/RepoBar.Core/Api/ProviderApiException.cs`
- `Windows/RepoBar.Core/Api/ProviderJsonRequestRunner.cs`
- `Windows/RepoBar.Core/Api/RecentItems.cs`
- `Windows/RepoBar.Core/Api/GitHubDtos.cs`
- `Windows/RepoBar.Core/Api/GitHubMappers.cs`
- `Windows/RepoBar.Core/Api/GitHubClient.cs`
- `Windows/RepoBar.Core/Api/GitLabDtos.cs`
- `Windows/RepoBar.Core/Api/GitLabMappers.cs`
- `Windows/RepoBar.Core/Api/GitLabClient.cs`
- `Windows/RepoBar.Tests/SequenceHandler.cs`

Commands run:

```sh
npx ctx7@latest library "GitHub REST API" "GitHub REST API list repositories issues pull requests releases branches tags commits actions runs contents traffic rate limit pagination ETag authentication headers"
npx ctx7@latest docs /websites/github_en_rest "GitHub REST API list repositories issues pull requests releases branches tags commits actions runs contents traffic rate limit pagination ETag authentication headers"
npx ctx7@latest library "GitLab REST API" "GitLab REST API projects issues merge requests releases branches tags commits contributors repository tree raw file pipeline current user pagination PRIVATE-TOKEN authentication"
npx ctx7@latest docs /websites/gitlab "GitLab REST API projects issues merge requests releases branches tags commits contributors repository tree raw file pipeline current user pagination PRIVATE-TOKEN authentication"
npx ctx7@latest library "GitHub GraphQL API" "GitHub GraphQL API contributionsCollection contributionCalendar weeks contributionDays user query bearer authentication"
npx ctx7@latest docs /websites/github_en_graphql "GitHub GraphQL API contributionsCollection contributionCalendar weeks contributionDays user query bearer authentication"
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 47
- Failed: 0
- Skipped: 0

Coverage added so far:

- GitHub REST request headers: `Accept`, `X-GitHub-Api-Version`, bearer auth, ETag.
- GitLab PAT request header: `PRIVATE-TOKEN`.
- Basic auth credential composition for future Bitbucket extension points.
- GitHub `Link` next-page parsing.
- GitLab `X-Next-Page` parsing.
- GitHub repository list endpoint construction.
- GitLab project merge request endpoint construction with escaped project path.
- GitHub/GitLab HTTPS host validation.
- Shared JSON response decoding and provider error extraction.
- GitHub repository list, repository search, current user, issues, pull requests, releases, branches, tags, workflow runs, contents, and traffic request/mapping coverage.
- GitLab project list, project search, current user, issues, merge requests, releases, branches, tags, commits, contributors, raw file request, and mapper coverage.
- Fixture-backed GitHub/GitLab repository and recent-item mapping tests.
- GitHub repository detail, commits, contributors, contents DTOs, traffic DTO integration, repository commit-activity heatmap, GraphQL user contribution heatmap, discussions, and rate-limit resource DTOs.
- GitLab repository tree, raw file response handling, and pipelines.
- Shared JSON request runner with conditional `304` cache handoff, status-code errors, retry policy, backoff calculation, diagnostics snapshots, and redacted response helpers.
- `IRepositoryService` widened to cover provider repository, recent item, contents, traffic, heatmap, and rate-limit surfaces.
- Deterministic fixtures for success, error, retry, unavailable provider, and unsupported-feature paths.

Known gaps:

- Live API integration has not been exercised in this scaffold; current verification uses deterministic fixtures.
- GitHub discussions and contribution calendar depend on GitHub API availability/permissions and should be revalidated with live tokens in Phase 9.
- Provider clients are read-only by design for v1.

Gate result:

- Phase 3 gate satisfied. Build and tests pass with focused coverage for GitHub.com and GitLab.com read-only provider clients, provider-specific unsupported behavior, shared request concerns, and deterministic fixtures. Continue to Phase 4 next.

## Phase 4: Storage, Settings, Cache, And Archives

Status: complete.

Artifacts added:

- `Windows/RepoBar.Core/Storage/RepoBarPaths.cs`
- `Windows/RepoBar.Core/Storage/SettingsStore.cs`
- `Windows/RepoBar.Core/Storage/CredentialStores.cs`
- `Windows/RepoBar.Core/Storage/CacheRecords.cs`
- `Windows/RepoBar.Core/Storage/PersistentCacheStore.cs`
- `Windows/RepoBar.Core/Storage/GitHubArchiveStore.cs`
- `Windows/RepoBar.Tests/StorageTests.cs`

Artifacts updated:

- `Windows/Directory.Packages.props`
- `Windows/RepoBar.Core/RepoBar.Core.csproj`

Commands run:

```sh
npx ctx7@latest library "Microsoft.Data.Sqlite" "SQLite persistent cache tables .NET 10 Microsoft.Data.Sqlite create tables insert query"
npx ctx7@latest library "Microsoft.Data.Sqlite" "official Microsoft.Data.Sqlite ADO.NET SQLite provider documentation"
curl -fsSL https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite/index.json
PATH="$PWD/.dotnet:$PATH" dotnet restore Windows/RepoBar.Windows.sln
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 57
- Failed: 0
- Skipped: 0

Coverage added:

- Windows path selection for `%AppData%\RepoBar` settings and `%LocalAppData%\RepoBar` cache.
- Credential store selection for release Windows Credential Manager, debug file-backed storage, and explicit `REPOBAR_TOKEN_STORE=file`.
- Debug credential file save/read/delete behavior.
- User settings JSON persistence.
- SQLite REST response/ETag persistence.
- SQLite GraphQL response persistence.
- SQLite rate-limit persistence and cache diagnostics.
- Cache-first startup body reads with stale marking.
- Cache clear API.
- Discrawl-compatible archive manifest validation, import metadata, status, and missing-file errors.

Notes:

- `ctx7` did not return official Microsoft.Data.Sqlite documentation; it only matched `sqlite-net`, so implementation stayed on basic Microsoft.Data.Sqlite ADO-style APIs and NuGet metadata was used to pin `Microsoft.Data.Sqlite` `10.0.8`.
- Archive import currently records RepoBar import metadata and validates/counts snapshot rows. Full table materialization and richer archive query adapters remain for the CLI/UI/archive reader surfaces in later phases.

Gate result:

- Phase 4 gate satisfied. Storage paths, credential selection, settings persistence, cache read/write/stale/clear/diagnostics, rate-limit persistence, and archive validation/import/status have focused tests and the Windows solution builds cleanly.

## Phase 5: Windows Platform Adapters

Status: complete for code-level adapter coverage. Live Windows tray/notification/autostart manual verification is deferred to Phase 8/9 when the package route and Windows runtime checks are performed.

Artifacts added:

- `Windows/RepoBar.Core/LocalProjects/GitExecutableLocator.cs`
- `Windows/RepoBar.Core/LocalProjects/LocalRepoStatus.cs`
- `Windows/RepoBar.Core/LocalProjects/LocalProjectsService.cs`
- `Windows/RepoBar.Desktop/Platform/TrayIconAdapter.cs`
- `Windows/RepoBar.Desktop/Platform/WindowsNotificationService.cs`
- `Windows/RepoBar.Desktop/Platform/WindowsLaunchAtLoginService.cs`
- `Windows/RepoBar.Desktop/Platform/WindowsShellLauncher.cs`
- `Windows/RepoBar.Desktop/Platform/OAuthLoopbackServer.cs`
- `Windows/RepoBar.Tests/LocalProjectsTests.cs`
- `Windows/RepoBar.Tests/WindowsAdapterTests.cs`

Artifacts updated:

- `Windows/RepoBar.Tests/RepoBar.Tests.csproj`

Commands run:

```sh
npx ctx7@latest library "Avalonia UI" "Avalonia 12 tray icon TrayIcon NativeMenu left click right click .ico asset loading desktop app"
npx ctx7@latest docs /avaloniaui/avalonia-docs "Avalonia 12 tray icon TrayIcon NativeMenu left click right click .ico asset loading desktop app"
npx ctx7@latest library ".NET" "ProcessStartInfo UseShellExecute open URL folder file with default Windows shell browser"
npx ctx7@latest docs /dotnet/docs "ProcessStartInfo UseShellExecute open URL folder file with default Windows shell browser"
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 71
- Failed: 0
- Skipped: 0

Coverage added:

- `.ico` tray asset validation and Avalonia `TrayIcon` adapter shape using `NativeMenu` and left-click command.
- Launch-at-login Run key command construction, enable, disable, and registry abstraction.
- Shell URL/path launch through `ProcessStartInfo.UseShellExecute = true`.
- Native Windows toast command construction and notification service abstraction.
- OAuth loopback callback listener capturing `code`, `state`, and `error`.
- `git.exe` discovery from explicit path, PATH entries, and Git for Windows default locations.
- Local project scan with hidden directory skipping and `.git` folder detection.
- Local repo status parsing for branch, remote full name, dirty counts/files, upstream, ahead/behind, and sync state.
- Safe fast-forward sync policy using `git pull --ff-only` only for clean, behind, non-detached repos.
- Local repo index matching by full name, preferred path, unique repo name, and containing path.

Known gaps:

- Live Windows verification for actual tray icon display, native toast presentation, Registry Run behavior, Windows shell launch, and installed-package startup behavior must run on Windows hardware/VM in Phase 8/9.
- Notification implementation currently uses the Windows toast WinRT API through a PowerShell gateway to avoid binding the app to a Windows-only target framework before package route selection. Revisit this in Phase 8 when deciding MSIX versus conventional installer.
- The tray adapter validates and constructs Avalonia tray behavior, but final tray wiring and visual behavior belongs to the Phase 6 UI shell.

Gate result:

- Phase 5 gate satisfied for repository-verifiable adapter implementation and tests. OS-level manual verification is explicitly deferred to the packaging/parity phases where Windows runtime evidence can be collected.

## Phase 6: Avalonia Desktop UI

Status: complete for repository-verifiable UI shell, compiled XAML, and scripted view-model smoke coverage. Live Windows visual/tray smoke remains part of Phase 8/9 runtime verification.

Artifacts added:

- `Windows/RepoBar.Desktop/Assets/RepoBar.ico`
- `Windows/RepoBar.Desktop/ViewModels/RelayCommand.cs`
- `Windows/RepoBar.Desktop/ViewModels/ShellViewModel.cs`
- `Windows/RepoBar.Tests/ShellViewModelTests.cs`

Artifacts updated:

- `Windows/RepoBar.Desktop/App.axaml.cs`
- `Windows/RepoBar.Desktop/Views/MainWindow.axaml`
- `Windows/RepoBar.Desktop/RepoBar.Desktop.csproj`

Artifacts removed:

- `Windows/RepoBar.Desktop/ViewModels/MainWindowViewModel.cs`

Commands run:

```sh
npx ctx7@latest library "Avalonia UI" "Avalonia 12 XAML binding styles TabControl ItemsControl commands compiled bindings window tray application"
npx ctx7@latest docs /avaloniaui/avalonia-docs "Avalonia 12 XAML binding styles TabControl ItemsControl commands compiled bindings window tray application"
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 77
- Failed: 0
- Skipped: 0

Coverage added:

- Tray-first app wiring with left-click dashboard command and right-click menu actions for account, refresh, settings, updates/help, and quit.
- Compiled Avalonia XAML dashboard with repository cards for provider, identity, issue/MR counts, CI, release, activity, traffic, heatmap, local status, cache source, and rate-limit state.
- Compact repository detail panel for local state, issues, PRs/MRs, CI, releases, traffic, discussions, and changelog preview.
- Settings surface covering General, Accounts/providers/hosts, Repositories, Display, Local Projects, Cache, Diagnostics, and Startup/update.
- PAT login flow into the shared credential store with token input cleared and no secret echoed in status text.
- Settings persistence through `SettingsStore`.
- Cache-first startup using the dashboard data source before refresh.
- Provider capability labels for GitLab unavailable traffic/discussions/calendar sections without noisy errors.
- Repository pin/hide/open commands.

Known gaps:

- Phase 9 replaced the default sample dashboard source with a provider-backed cache-first data source; the sample source remains for deterministic tests and empty-state smoke coverage.
- Live visual smoke on Windows for tray positioning, high-DPI icon rendering, and native window behavior is deferred to Phase 8/9 with packaging/runtime checks.
- OAuth UI entry points were initially represented by loopback/platform adapters and account settings; Phase 9 now wires full desktop and CLI browser OAuth orchestration through the shared core service.

Gate result:

- Phase 6 gate satisfied for the current port stage. Build validates compiled XAML, tests cover first-launch cached dashboard state, PAT login, refresh, repository details, settings save, unsupported GitLab feature labeling, and repository visibility/open actions.

## Phase 7: CLI And Diagnostics

Status: complete for command surface, deterministic output, shared service wiring, and test coverage. Live provider/API verification remains for Phase 9.

Artifacts added:

- `Windows/RepoBar.Cli/CliProgram.cs`
- `Windows/RepoBar.Cli/CliOptions.cs`
- `Windows/RepoBar.Cli/CliRuntimeContext.cs`
- `Windows/RepoBar.Cli/SettingsMutator.cs`
- `Windows/RepoBar.Tests/CliApplicationTests.cs`

Artifacts updated:

- `Windows/RepoBar.Cli/Program.cs`
- `Windows/RepoBar.Tests/RepoBar.Tests.csproj`

Commands run:

```sh
npx ctx7@latest library "System.CommandLine" "System.CommandLine 2.0 command options arguments subcommands parse invoke async .NET 10"
npx ctx7@latest library ".NET System.CommandLine" "official System.CommandLine 2.0 command options arguments subcommands parse invoke async"
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 85
- Failed: 0
- Skipped: 0

Coverage added:

- Command parsing for `repos`, `repo`, recent-list commands, `local`, `cache`, `rate-limits`, `settings`, `login`, `logout`, `status`, and `archives`.
- JSON and plain output paths.
- Provider routing for GitHub/GitLab through `IRepositoryService`.
- PAT bootstrap login using the shared credential store without echoing the secret.
- Logout and status against the same credential selection rules.
- Cache diagnostics, persisted rate-limit output, and cache clear.
- Settings show/set persistence through `SettingsStore`.
- Local repo scan/status through shared local-project services.
- Archive add/list/status/validate/update/remove/enable/disable through shared archive settings and importer.
- Error output for invalid commands.

Notes:

- `ctx7` did not return official `System.CommandLine` documentation, so the CLI uses a small explicit parser over the existing package dependency rather than relying on uncertain current API examples.
- The default runtime context creates real GitHub/GitLab clients with shared settings, credential selection, cache paths, and local Git services; tests inject deterministic fake services to avoid live network calls.

Known gaps:

- Browser OAuth command orchestration is completed in Phase 9; Phase 7 initially shipped PAT bootstrap via `--token` or `REPOBAR_PAT`.
- Live GitHub/GitLab command verification with real tokens is deferred to Phase 9.
- Some advanced local actions from the macOS CLI docs were outside the original Phase 7 gate; Phase 9 later added `local rebase`, `local reset`, `local branches`, `worktrees`, `checkout`, and terminal/folder open coverage.

Gate result:

- Phase 7 gate satisfied. CLI tests cover command parsing, JSON/plain output, auth selection, cache diagnostics, provider routing, local repo commands, archive commands, settings persistence, and error output.

## Phase 8: Packaging, Signing, Updates, And Release Flow

Status: complete for package-route decision, publish/package scripts, signing hooks, release documentation, static package validation, and cross-publish verification. Live Windows install/upgrade/uninstall verification is deferred to Phase 9 runtime hardening.

Artifacts added:

- `Windows/Packaging/package.env`
- `Windows/Packaging/README.md`
- `Windows/Packaging/Wix/Package.wxs`
- `Windows/Packaging/Scripts/publish-windows.ps1`
- `Windows/Packaging/Scripts/package-windows.ps1`
- `Windows/Packaging/Scripts/write-wix-file-list.ps1`
- `Windows/Packaging/Scripts/sign-windows-artifacts.ps1`
- `Windows/Packaging/Scripts/validate-windows-package.ps1`
- `Windows/Packaging/windows-release-checklist.md`
- `Windows/RepoBar.Tests/PackagingTests.cs`

Artifacts updated:

- `.gitignore`
- `Windows/README.md`
- `docs/release.md`

Package decisions:

- Windows v1 package route: conventional WiX MSI plus ZIP artifact.
- Stable Windows app ID: `com.openclaw.repobar.windows`.
- Stable MSI UpgradeCode: `6C6D9F17-4A23-47F4-8E52-92AE57CF18C2`.
- Update route: signed MSI major upgrades from GitHub Releases. Sparkle remains macOS-only and is not ported.
- MSIX is deferred until Windows runtime checks prove tray, autostart, arbitrary local Git folder access, shell launch, and Credential Manager behavior under package identity.

Commands run:

```sh
npx ctx7@latest library "WiX Toolset" "WiX Toolset v5 .NET CLI installer MSI build command Package.wxs desktop app Windows service registry shortcut install uninstall upgrade"
npx ctx7@latest docs /wixtoolset/wix "WiX Toolset v5 .NET CLI installer MSI build command Package.wxs desktop app registry shortcut install uninstall upgrade"
npx ctx7@latest library "MSIX" "MSIX Windows app packaging desktop app package identity startup task file system access registry credential manager limitations"
npx ctx7@latest docs /microsoftdocs/msix-docs "MSIX Windows app packaging desktop app package identity startup task file system access registry credential manager limitations"
npx ctx7@latest library ".NET publish" "dotnet publish self-contained win-x64 single file desktop app ReadyToRun sign assembly publish profile"
npx ctx7@latest docs /dotnet/docs "dotnet publish self-contained win-x64 single file desktop app ReadyToRun sign assembly publish profile"
curl -fsSL https://api.nuget.org/v3-flatcontainer/wixtoolset.sdk/index.json
PATH="$PWD/.dotnet:$PATH" dotnet publish Windows/RepoBar.Desktop/RepoBar.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -o Windows/artifacts/publish/win-x64
PATH="$PWD/.dotnet:$PATH" dotnet publish Windows/RepoBar.Cli/RepoBar.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -o Windows/artifacts/publish/win-x64/cli
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 89
- Failed: 0
- Skipped: 0

Coverage added:

- Stable package identity and route configuration.
- WiX package authoring for per-user install, major upgrades, Start menu shortcut, install registry values, and no settings/cache removal.
- Publish/package/sign/validate script coverage.
- Release docs requiring manual Windows 11 checks for install, launch, auth, autostart, local Git, shell launch, upgrade, uninstall, and settings/cache preservation.
- Release docs verifying Windows artifacts publish alongside macOS artifacts without changing the macOS Sparkle/appcast flow.

Known gaps:

- `pwsh` and WiX CLI are not installed in this macOS environment, so MSI creation/signing scripts were statically validated but not executed here.
- Windows artifacts were cross-published for `win-x64`; the generated artifact directory was removed afterward and is ignored by `.gitignore`.
- Clean install, upgrade, uninstall, autostart-after-reboot, tray display, shell launch, and Credential Manager runtime checks require a Windows 11 VM/device and are deferred to Phase 9.

Gate result:

- Phase 8 gate satisfied for repository-verifiable packaging implementation and cross-publish proof. Final installer runtime proof is explicitly carried into Phase 9 parity hardening.

## Phase 9: Parity Hardening

Status: repository-verifiable hardening complete; Phase 9 remains open for Windows 11 runtime evidence.

Artifacts added:

- `windows-phase9-parity-audit.md`
- `windows-runtime-verification-plan.md`

Artifacts updated:

- `Windows/Packaging/Scripts/write-wix-file-list.ps1`
- `Windows/RepoBar.Core/Auth/PkceChallenge.cs`
- `Windows/RepoBar.Core/Auth/OAuthTokens.cs`
- `Windows/RepoBar.Core/Auth/OAuthLoginService.cs`
- `Windows/RepoBar.Core/Api/ProviderJsonRequestRunner.cs`
- `Windows/RepoBar.Cli/CliRuntimeContext.cs`
- `Windows/RepoBar.Cli/CliOptions.cs`
- `Windows/RepoBar.Cli/CliProgram.cs`
- `Windows/RepoBar.Core/Support/RepositoryAutocomplete.cs`
- `Windows/RepoBar.Core/Support/RefreshScheduler.cs`
- `Windows/RepoBar.Core/Support/ChangelogPreviewService.cs`
- `Windows/RepoBar.Core/Api/RepositoryServiceFactory.cs`
- `Windows/RepoBar.Tests/OAuthLoginServiceTests.cs`
- `Windows/RepoBar.Tests/RepositoryAutocompleteTests.cs`
- `Windows/RepoBar.Tests/RefreshSchedulerTests.cs`
- `Windows/RepoBar.Tests/ChangelogPreviewServiceTests.cs`
- `Windows/RepoBar.Tests/ProviderJsonRequestRunnerTests.cs`
- `Windows/RepoBar.Tests/LocalProjectsTests.cs`
- `Windows/RepoBar.Tests/PackagingTests.cs`
- `Windows/RepoBar.Tests/CliApplicationTests.cs`
- `Windows/RepoBar.Tests/ShellViewModelTests.cs`
- `Windows/RepoBar.Tests/StubHandler.cs`
- `Windows/RepoBar.Desktop/ViewModels/ShellViewModel.cs`
- `Windows/RepoBar.Desktop/Views/MainWindow.axaml`
- `Windows/README.md`
- `windows-migration.md`
- `windows-parity-matrix.md`
- `docs/release.md`

Commands run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Tests run:

```sh
PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build
```

Current result:

- Passed: 131
- Failed: 0
- Skipped: 0

Coverage/hardening added:

- Browser OAuth login now has shared-core PKCE generation, authorization URL construction, loopback callback handling, authorization-code token exchange, token/client credential persistence, refresh-before-use, desktop account command wiring, and CLI `login --oauth` wiring.
- PAT login now stores credentials under the selected provider host for GitHub, GitHub Enterprise, and GitLab in both desktop and CLI paths.
- Repository autocomplete scoring/suggestions are ported from the Swift behavior and the Avalonia settings view model now exposes searchable repository browser rows.
- Refresh scheduler policy now covers configurable intervals, manual refresh override, and rate-limit/backoff-delayed next refresh in shared core.
- CLI parity now includes `discussions`, `contributions`, and manual `refresh` commands using shared provider services.
- CLI settings parity now includes `card-density`, `accent-tone`, `activity-scope`, `heatmap-display`, `heatmap-span`, and documented `menu-sort event`.
- CLI repository visibility parity now includes `pin`, `unpin`, `hide`, and `show` commands backed by persisted settings.
- CLI markdown/changelog parity now includes `changelog` and `markdown` commands backed by shared parser/rendering support.
- CLI repository list parity now applies documented `repos` age, release, event, scope, pinned-only, owner, mine, filter/only-with, forks, archived, and sort flags through shared filter/sort helpers.
- CLI repo detail parity now includes documented `repo --traffic`, `repo --heatmap`, and `repo --release` output.
- Local branch/worktree/open/checkout/sync and destructive CLI actions now cover `local --sync`, `local branches`, `worktrees`, `open finder`, `open terminal`, `checkout`, and explicit `--confirm`/`--yes` guardrails for `local rebase` and `local reset`.
- The desktop default dashboard now uses a provider-backed cache-first data source with credential state, cache diagnostics, rate-limit summaries, and local checkout matching; sample data is no longer the default app source.
- Local checkout changelog preview now reads common changelog filenames and uses the shared parser with release-tag selection.
- Secret redaction now covers structured provider secret fields plus raw GitHub/GitLab token shapes in diagnostics.
- Local Git state tests now explicitly cover clean, dirty, ahead, behind, diverged, missing-upstream, detached, and auto-sync eligibility behavior.
- WiX file-list generation now preserves nested publish directories such as the CLI subfolder instead of flattening all files into `INSTALLFOLDER`.
- Phase 9 parity audit maps resolved v1 surfaces, explicit product deferrals, and Windows-runtime-only checks.
- Windows docs now record exact supported and unsupported v1 behavior, package/update decisions, telemetry stance, and remaining Windows 11 runtime checks.

Known gaps:

- Live GitHub.com, GitHub Enterprise, and GitLab.com happy paths with real credentials have not been exercised in this macOS environment.
- Offline, expired-token, missing-permission, and provider-rate-limited paths are covered by deterministic tests but still need live-provider verification.
- Tray display, native notifications, high-DPI icon rendering, launch-at-login after reboot, Credential Manager release storage, installed shell launch, clean install, upgrade, uninstall, and restart/upgrade persistence require a Windows 11 VM/device.

Gate result:

- Phase 9 is not fully closed because the completion gate requires Windows runtime evidence that this environment cannot provide. Repository-verifiable parity hardening is complete and documented in `windows-phase9-parity-audit.md`; the target-system execution plan is `windows-runtime-verification-plan.md`.
