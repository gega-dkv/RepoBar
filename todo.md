# TODO: Multi-Provider Git Hosting Support

Generated: 2026-05-08

## Scope

Implement first-class support for multiple Git hosting providers alongside the existing GitHub.com/GitHub Enterprise path.

Initial target:

- GitLab.com, using `https://gitlab.com` and `https://gitlab.com/api/v4`.
- Bitbucket Cloud, using `https://bitbucket.org` and `https://api.bitbucket.org/2.0`.
- Codeberg, using its Forgejo-compatible API.
- Forgejo/Gitea-compatible self-hosted instances when the user provides a base URL.
- Generic custom Git hosts with no forge API, limited to local repository discovery, open-in-browser URLs, and clone/pull/local-state workflows.
- Read-only repository dashboard parity where each provider has equivalent APIs.
- Personal access token or API token first; OAuth can follow once the provider abstraction and token isolation are in place.
- Keep GitHub behavior unchanged while adding new providers.

Out of scope for the first pass:

- Write operations.
- Provider-specific features without a direct RepoBar UI today, such as GitLab epics, Bitbucket projects beyond grouping, deployments, packages, security dashboards, or wikis.
- Full API parity for arbitrary custom Git servers that are not GitLab, Forgejo, Gitea, or Bitbucket-compatible.
- GitHub App-style installation flows for non-GitHub providers.

## Provider Targets

- GitHub: existing implementation. Keep as the compatibility baseline.
- GitLab.com: first new full provider because its REST API covers projects, issues, merge requests, pipelines, releases, branches, tags, commits, contributors, and repository files.
- Bitbucket Cloud: full provider where APIs exist. Map pull requests, issues, branches, tags, commits, source browsing, and Pipelines when available.
- Codeberg: treat as a hosted Forgejo provider. Prefer a `ForgejoClient` that can also serve self-hosted Forgejo.
- Forgejo/Gitea self-hosted: configurable provider with `/api/v1`-style endpoints and user-supplied base URL.
- GitLab self-managed: add after GitLab.com by allowing a configured web host and API host, similar to the current GitHub Enterprise model.
- Generic custom Git: no issue/PR/CI API assumptions. Support remote parsing, local status, clone/open actions, and manually configured web URL templates.

## Current Architecture Findings

- The app is currently GitHub-shaped end to end. The main API actor is `Sources/RepoBarCore/API/GitHubClient.swift`, backed by `GitHubRestAPI`, `GraphQLClient`, `GitHubRequestRunner`, GitHub-specific decoders, and GitHub-specific error types.
- `AppState` owns a concrete `GitHubClient` at `Sources/RepoBar/App/AppState.swift`, so refresh, settings, contribution loading, reference lookup, diagnostics, recent menus, and CLI commands all depend on GitHub directly.
- `Repository` currently models identity as `owner/name`. GitLab and Forgejo projects can be `group/subgroup/project`, and Bitbucket uses workspace/repo slug, so adding more providers requires provider-neutral identity before serious API work.
- `UserSettings` stores `githubHost`, `enterpriseHost`, `githubArchives`, and GitHub-specific auth options. Tokens are stored under generic Keychain/file accounts like `default`, `client`, and `pat`, so credentials for multiple providers would collide unless token storage is keyed by provider and host.
- The menu and settings UI contains many GitHub labels: "Open in GitHub", "GitHub Rate Limits", "GitHub greens", "GitHub Reference Watcher", GitHub App installation text, and GitHub PAT scope text.
- Local project matching already parses Git remotes in `LocalProjectsService`, but matching assumes a two-part `owner/name` remote. It must understand provider host plus full repository path.
- The CLI in `Sources/repobarcli` constructs `GitHubClient` directly and assumes GitHub auth, GitHub hosts, and `owner/name` repository arguments.
- GitHub-only features need explicit fallback behavior for every other provider: contribution calendar, traffic views/clones, discussions, GitHub archive imports, GitHub App installation, and GitHub GraphQL rate limits.

## Provider Documentation Notes

Checked with Context7 (`/websites/gitlab_18_4`) on 2026-05-08.

- GitLab REST API is JSON-based and uses `/api/v4`.
- OAuth API calls use `Authorization: Bearer <token>`.
- GitLab PAT examples use `PRIVATE-TOKEN: <token>`.
- List endpoints use offset pagination with `page`, `per_page` up to 100, `Link`, `x-next-page`, `x-page`, `x-per-page`, `x-prev-page`, `x-total`, and `x-total-pages`.
- Private project access can intentionally return 404 for unauthorized users.
- GitLab exposes issues, merge requests, projects, repository content, archives, and related merge-request endpoints through REST APIs.
- Verify exact scopes during implementation, but the likely read-only PAT/OAuth scopes are `read_user`, `read_api`, and `read_repository`.

Checked with Context7 (`/websites/developer_atlassian_cloud_bitbucket_rest_intro`) on 2026-05-08.

- Bitbucket Cloud REST API uses `https://api.bitbucket.org/2.0`.
- OAuth-style calls use `Authorization: Bearer <access_token>`.
- Bitbucket API tokens use Basic HTTP auth with Atlassian email as username and API token as password.
- Relevant endpoints include repositories, refs, branches, tags, source, issues, and pull requests under `/2.0/repositories/{workspace}/{repo_slug}`.
- Repository objects include `full_name`, `uuid`, `name`, `owner`, `project`, `scm`, `updated_on`, and `links.html.href`.
- Most paginated collection endpoints support Bitbucket's shared query language.

Checked with Context7 library search on 2026-05-08.

- Forgejo API docs are available as `/openapi/codeberg_swagger_v1_json`.
- Codeberg docs are available as `/websites/codeberg`.
- Gitea docs are available as `/websites/gitea`.
- Fetch exact Forgejo/Gitea endpoint docs before implementing the client; expect a Forgejo/Gitea adapter rather than a Codeberg-only adapter.

## Phase 1: Provider Foundation

- [x] Add a provider enum in RepoBarCore, for example `SourceControlProvider` with:
  - `.github`
  - `.gitlab`
  - `.bitbucketCloud`
  - `.forgejo`
  - `.gitea`
  - `.customGit`
- [x] Add a provider/host account model, for example `RepositoryAccount { provider, webHost, apiHost, username }`.
- [x] Add provider capability flags:
  - repositories/projects
  - issues
  - pull requests or merge requests
  - CI/pipelines/actions
  - releases
  - tags
  - branches
  - commits
  - contributors
  - repository contents
  - contribution calendar
  - traffic stats
  - discussions
  - rate-limit diagnostics
- [x] Update `UserSettings` to store the selected provider and provider-specific host/auth settings.
- [x] Add settings migration from the current GitHub-only fields into the new provider settings.
- [x] Do not delete `githubHost`, `enterpriseHost`, or `githubArchives` until migration and compatibility are tested.
- [x] Add a provider-neutral repository identity:
  - `provider`
  - `id`
  - `name`
  - `namespacePath`
  - `pathWithNamespace`
  - `slug`
  - `webURL`
  - `apiURL`
  - provider-specific stable IDs such as GitHub node/database ID, GitLab project ID, Bitbucket UUID, or Forgejo numeric repo ID.
  - GitHub compatibility accessors for existing `owner`, `fullName`, and `owner/name` flows.
- [x] Replace new logic that parses repository names with `pathWithNamespace`, not `owner/name`.
- [x] Keep the first migration conservative: GitHub repos should still render exactly as `owner/name`.
- [x] Add a `RepositoryHost` model for configured hosts:
  - provider
  - display name
  - web base URL
  - API base URL
  - auth method
  - URL template overrides for generic custom Git.

## Phase 2: API Abstraction

- [x] Introduce a provider-neutral protocol, for example `RepositoryService`, covering the behavior `AppState`, recent menus, and CLI need:
  - current user
  - accessible repositories
  - cached repository list
  - full repository detail
  - recent issues
  - recent pull requests or merge requests
  - releases
  - CI/pipeline runs
  - tags
  - branches
  - contributors
  - commits
  - repository contents and file contents
  - search/autocomplete
  - reference lookup
  - rate-limit diagnostics
  - provider capabilities
- [x] Wrap the existing `GitHubClient` behind that protocol before adding the first new provider.
- [x] Rename only where it removes architectural friction. A full repo-wide GitHub-to-provider rename can be separate; avoid churn in the first compatibility patch.
- [x] Update `AppState` to depend on the protocol or on a small router actor that delegates to `GitHubClient` or `GitLabClient`.
- [x] Update `RecentMenuService`, `RepoDetailCoordinator` call sites, and CLI auth setup to use the provider abstraction.
- [x] Add an `UnsupportedProviderFeature` result path so UI can hide unavailable sections instead of showing errors for providers that do not expose a feature.
- [x] Keep generic custom Git behind a smaller `LocalOnlyRepositoryService` that never pretends to support issues, PRs, CI, releases, or API search.

## Phase 3: Token And Auth Isolation

- [x] Extend `TokenStore` so saved values are keyed by provider and host:
  - `github:github.com:oauth`
  - `github:github.com:pat`
  - `gitlab:gitlab.com:pat`
  - `bitbucket:bitbucket.org:api-token`
  - `forgejo:codeberg.org:pat`
  - `gitea:git.example.com:pat`
  - `custom-git:git.example.com:none`
  - future `gitlab:gitlab.com:oauth`
- [x] Support multiple credential header styles:
  - GitHub OAuth/PAT: `Authorization: Bearer <token>` for current behavior.
  - GitLab OAuth: `Authorization: Bearer <token>`.
  - GitLab PAT: `PRIVATE-TOKEN: <token>` or documented equivalent chosen during implementation.
  - Bitbucket OAuth: `Authorization: Bearer <token>`.
  - Bitbucket API token: Basic auth with Atlassian email plus API token.
  - Forgejo/Gitea PAT: verify exact accepted header before implementation.
- [x] Add a migration path for existing GitHub `default`, `client`, and `pat` accounts.
- [x] Store token type with the credential so the request runner knows whether to send `Authorization: Bearer` or `PRIVATE-TOKEN`.
- [x] Implement GitLab PAT login first:
  - validate with `GET https://gitlab.com/api/v4/user`
  - decode `username`, `name`, `avatar_url`, and `web_url` when available
  - save only after validation succeeds
- [x] Add GitLab.com to Account Settings as a provider choice.
- [x] Add Bitbucket Cloud API token login:
  - require Atlassian email plus API token
  - validate with Bitbucket current-user endpoint
  - store email and token together as a credential.
- [x] Add Codeberg/Forgejo PAT login after fetching exact Forgejo API auth docs.
- [x] Add generic custom Git host setup without API credentials:
  - web base URL
  - SSH/HTTPS clone host
  - optional URL templates for repository, branch, commit, issue, and PR pages.
- [x] Update token creation link for GitLab.com to the GitLab personal access token page.
- [x] Add token creation/help links for Bitbucket Cloud, Codeberg, Forgejo, and Gitea.
- [x] Update help text for GitLab PAT scopes after verifying exact docs: start with `read_user`, `read_api`, `read_repository`.
- [x] Add provider-specific scope/help copy in Account Settings instead of one shared PAT description.
- [ ] Add OAuth later:
  - authorize endpoint: `https://gitlab.com/oauth/authorize`
  - token endpoint: `https://gitlab.com/oauth/token`
  - loopback callback can reuse the existing PKCE server
  - token refresh behavior must be tested separately from GitHub.

## Phase 4: GitLab REST Client

- [x] Add `Sources/RepoBarCore/API/GitLabClient.swift`.
- [x] Add GitLab REST support files: `GitLabRequestRunner`, `GitLabModels`, and summary/status mapping inside `GitLabClient`.
- [x] Reuse shared HTTP/cache helpers where possible, but do not force GitLab into `GitHubRequestRunner` if header semantics diverge.
- [x] Implement GitLab pagination using `x-next-page` first and `Link` as fallback.
- [x] URL-encode project paths when using them as `:id`, for example `group%2Fsubgroup%2Fproject`.
- [x] Prefer GitLab project `id` for stable API calls once a project is known.
- [x] Map GitLab project fields into `Repository`:
  - `id`
  - `name`
  - `path_with_namespace`
  - `web_url`
  - `namespace.full_path`
  - `archived`
  - `star_count`
  - `forks_count`
  - `last_activity_at`
  - visibility/access metadata when available.
- [x] Repository list endpoint candidate:
  - `GET /projects?membership=true&simple=true&order_by=last_activity_at&sort=desc&per_page=100`
- [x] Repository detail endpoint candidate:
  - `GET /projects/:id`
  - or `GET /projects/:urlEncodedPath`.
- [x] Open issue count:
  - `GET /projects/:id/issues?state=opened&per_page=1`
  - prefer `x-total` when present.
- [x] Open merge request count:
  - `GET /projects/:id/merge_requests?state=opened&per_page=1`
  - map to current `openPulls` until UI vocabulary is provider-aware.
- [x] Recent issues:
  - `GET /projects/:id/issues?state=opened&order_by=updated_at&sort=desc&per_page=<limit>`.
- [x] Recent merge requests:
  - `GET /projects/:id/merge_requests?state=opened&order_by=updated_at&sort=desc&per_page=<limit>`.
- [x] CI status:
  - `GET /projects/:id/pipelines?per_page=1`
  - optionally pass `ref=<default_branch>` when known.
  - map `success`, `failed`, `canceled`, `skipped`, `running`, `pending`, and `manual` to `CIStatus`.
- [x] Releases:
  - `GET /projects/:id/releases?per_page=20`.
- [x] Tags:
  - `GET /projects/:id/repository/tags?per_page=<limit>`.
- [x] Branches:
  - `GET /projects/:id/repository/branches?per_page=<limit>`.
- [x] Commits:
  - `GET /projects/:id/repository/commits?per_page=<limit>`.
- [x] Contributors:
  - `GET /projects/:id/repository/contributors?per_page=<limit>`.
- [x] Contents:
  - repository tree and file endpoints, with a small adapter so existing changelog and file preview flows still work.
- [x] Activity:
  - start with project events if they provide enough signal.
  - If global activity parity is weak, show per-repo latest activity only and document the limitation.
- [x] Unsupported GitHub-only data:
  - traffic stats: set `nil` and hide the row.
  - GitHub contribution heatmap: hide contribution header for GitLab until a real GitLab contribution source is implemented.
  - discussions: hide the menu item for GitLab.

## Phase 5: Bitbucket Cloud Client

- [x] Add `Sources/RepoBarCore/API/BitbucketClient.swift`.
- [x] Add Bitbucket REST support files: `BitbucketRequestRunner`, `BitbucketModels`, and summary/status mapping inside `BitbucketClient`.
- [x] Use API base `https://api.bitbucket.org/2.0`.
- [x] Implement Basic auth for API tokens and Bearer auth for future OAuth.
- [x] Implement Bitbucket pagination from response `next` URLs and any available page metadata.
- [x] Map repository fields:
  - `uuid`
  - `full_name`
  - `name`
  - `owner.username` or workspace slug
  - `project.key`
  - `updated_on`
  - `links.html.href`
  - `links.clone`
  - `scm`.
- [x] Repository list endpoint candidate:
  - `GET /2.0/user/permissions/repositories` for authenticated accessible repositories.
- [x] Repository detail endpoint:
  - `GET /2.0/repositories/{workspace}/{repo_slug}`.
- [x] Pull requests:
  - `GET /2.0/repositories/{workspace}/{repo_slug}/pullrequests`.
- [x] Issues:
  - `GET /2.0/repositories/{workspace}/{repo_slug}/issues`.
  - Hide issue rows when `has_issues` is false.
- [x] Branches:
  - `GET /2.0/repositories/{workspace}/{repo_slug}/refs/branches`.
- [x] Tags:
  - `GET /2.0/repositories/{workspace}/{repo_slug}/refs/tags`.
- [x] Commits:
  - use repository `links.commits.href` or documented commits endpoint.
- [x] Contents:
  - use `/src` endpoints for tree and file preview.
- [x] CI:
  - map Bitbucket Pipelines if accessible; otherwise hide CI until implemented.
- [x] Releases:
  - Bitbucket Cloud does not have a GitHub/GitLab-style releases feature. Consider downloads/tags as a fallback only if the UI can label it honestly.
- [x] Unsupported GitHub-only data:
  - traffic stats: hide.
  - contribution heatmap: hide.
  - discussions: hide.
  - GitHub archives: hide.

## Phase 6: Codeberg, Forgejo, And Gitea Clients

- [ ] Fetch exact Context7 docs before implementation:
  - `/openapi/codeberg_swagger_v1_json`
  - `/websites/codeberg`
  - `/websites/forgejo`
  - `/websites/gitea`
- [ ] Prefer one `ForgejoCompatibleClient` with host-specific configuration for Codeberg, Forgejo, and Gitea.
- [ ] Add configured defaults:
  - Codeberg web host `https://codeberg.org`
  - Codeberg API host, to be verified from docs before implementation.
  - self-hosted API host derived from web host unless user overrides it.
- [ ] Add compatibility flags because Forgejo and Gitea versions may differ by instance.
- [ ] Implement repository list/detail mapping.
- [ ] Implement issues and pull requests if exposed by the instance.
- [ ] Implement releases where supported.
- [ ] Implement branches, tags, commits, contributors, and repository contents.
- [ ] Implement actions/CI only when the instance exposes compatible endpoints; otherwise hide CI.
- [ ] Treat disabled repository features as capability misses, not errors.
- [ ] Add version/capability probing endpoint if available.

## Phase 7: Custom Self-Hosted Git

- [ ] Add a `CustomGitHostSettings` model:
  - display name
  - web base URL
  - clone host aliases
  - SSH clone pattern
  - HTTPS clone pattern
  - repository URL template
  - branch URL template
  - commit URL template
  - optional issue URL template
  - optional pull/merge request URL template.
- [ ] Support local-only repositories from arbitrary remotes:
  - bare Git server
  - cgit
  - GitWeb
  - Sourcehut or other forge-like hosts without a first-class API adapter.
- [ ] Show custom Git repositories in the Local filter even without account login.
- [ ] Allow manual pinning of custom Git repositories discovered locally.
- [ ] Do not show API-backed counts for custom Git unless a provider adapter is selected.
- [ ] Add a future extension point for a user-supplied OpenAPI or endpoint mapping, but do not build that in the first pass.

## Phase 8: UI And Vocabulary

- [ ] Add provider vocabulary helpers:
  - GitHub: repository, pull request, GitHub, Actions, discussions.
  - GitLab: project, merge request, GitLab, pipelines, no discussions.
  - Bitbucket: repository, pull request, Bitbucket, Pipelines.
  - Codeberg/Forgejo/Gitea: repository, pull request, Forgejo/Gitea Actions only if supported by the instance.
  - Custom Git: repository, branch, commit, local status.
- [ ] Replace hard-coded menu strings:
  - "Open in GitHub" -> provider-specific host text
  - "Open on GitHub" -> provider-specific text
  - "GitHub Rate Limits" -> provider-specific diagnostics or hidden when unsupported
  - "Sign in to GitHub" -> provider-specific sign-in prompt
  - "Connect your GitHub account" -> provider-specific prompt.
- [ ] Keep CLI command `pulls` for compatibility, but add `merge-requests`, `mrs`, and provider-specific aliases where useful.
- [ ] Add provider badges or subtle labels in account/settings screens so users know which provider is active.
- [ ] Add a provider picker in Account Settings:
  - GitHub.com
  - GitHub Enterprise
  - GitLab.com
  - GitLab self-managed
  - Bitbucket Cloud
  - Codeberg
  - Forgejo self-hosted
  - Gitea self-hosted
  - Custom Git host.
- [ ] Hide or disable GitHub-only settings for other providers:
  - GitHub App installation link
  - GitHub archives
  - GitHub reference watcher copy until equivalent lookup exists
  - GraphQL rate-limit rows
  - GitHub green wording.
- [ ] Hide unavailable provider sections based on capability flags:
  - traffic
  - contribution header
  - releases
  - issues
  - PRs/MRs
  - CI
  - discussions
  - contributors.
- [ ] Update Display Settings menu builder when changing menu customization items.

## Phase 9: Local Projects

- [ ] Update `LocalRepoStatus` and `LocalRepoIndex` to include remote host and full path.
- [ ] Update remote parsing to preserve multi-segment namespaces:
  - `git@gitlab.com:group/subgroup/project.git`
  - `https://gitlab.com/group/subgroup/project.git`
  - `git@bitbucket.org:workspace/repo.git`
  - `https://bitbucket.org/workspace/repo.git`
  - `git@codeberg.org:org/repo.git`
  - `https://codeberg.org/org/repo.git`
  - `ssh://git@git.example.com/group/subgroup/repo.git`
  - custom host patterns configured by the user.
- [ ] Match local repositories by provider host plus `pathWithNamespace`.
- [ ] Keep fallback matching by repository leaf name only when unambiguous.
- [ ] Store preferred local paths by provider plus full path, not only lowercased `owner/name`.
- [ ] Update local menu actions that build web URLs to use the repository `webURL`.
- [ ] Add tests for host aliases, for example `github.com` vs `ssh.github.com`, and custom SSH hostnames.

## Phase 10: Reference Watcher

- [ ] Rename GitHub-specific reference types to provider-neutral names before adding new provider logic.
- [ ] Support GitLab URL forms:
  - `https://gitlab.com/group/project/-/issues/123`
  - `https://gitlab.com/group/project/-/merge_requests/123`
  - `https://gitlab.com/group/project/-/commit/<sha>`
- [ ] Support GitLab short references:
  - `#123` for issues
  - `!123` for merge requests
  - commit hashes.
- [ ] Support Bitbucket URL forms after confirming current web routes:
  - pull requests
  - issues
  - commits.
- [ ] Support Forgejo/Gitea URL forms after confirming current web routes:
  - issues
  - pulls
  - commits.
- [ ] For custom Git hosts, parse commit URLs only by default and support issue/PR templates if configured.
- [ ] Use project IID for issues and merge requests, not global database IDs.
- [ ] Cache lookup should include provider and project path so references from different providers never cross-match.

## Phase 11: Cache And Diagnostics

- [ ] Add provider and host to persistent cache keys wherever they are not already part of the URL key.
- [ ] Rename user-facing "GitHub cache" text only where other providers will use the same cache.
- [ ] Leave GitHub archive support GitHub-only in the first multi-provider release.
- [ ] Add provider-specific rate-limit diagnostics if the provider returns rate-limit headers.
- [ ] Always surface pagination and auth failures as provider-specific errors.
- [ ] Treat GitLab/Forgejo/Gitea 404 on private projects as "not found or not visible to this token".
- [ ] Treat Bitbucket 401/403 as credential or workspace access failures with provider-specific copy.
- [ ] Keep custom Git cache limited to local repo status and configured URL templates.

## Phase 12: CLI

- [ ] Update `makeAuthenticatedClient()` to read selected provider and create the correct client.
- [ ] Add CLI login support for:
  - GitLab PAT
  - Bitbucket API token
  - Codeberg/Forgejo/Gitea PAT
  - custom Git host configuration without token.
- [ ] Add `--provider github|gitlab|bitbucket|codeberg|forgejo|gitea|custom-git` where commands need to operate outside the selected app provider.
- [ ] Update repo argument parsing so providers can accept `group/subgroup/project`, `workspace/repo`, and custom paths.
- [ ] Add `merge-requests`/`mrs` command aliases.
- [ ] Update JSON output to include `provider`, `pathWithNamespace`, and `webURL`.
- [ ] Update `docs/cli.md` after command behavior is stable.

## Phase 13: Tests

- [ ] Add settings migration tests from GitHub-only settings to provider-aware settings.
- [ ] Add token-store isolation tests for GitHub OAuth, GitHub PAT, GitLab PAT, Bitbucket API token, and Forgejo/Gitea PAT.
- [ ] Add GitLab pagination tests for `x-next-page`, `x-total`, and missing total headers.
- [ ] Add GitLab project decoding tests, including subgroup paths.
- [ ] Add GitLab issue and merge-request decoder tests.
- [ ] Add GitLab pipeline status mapper tests.
- [ ] Add GitLab release/tag/branch/commit decoder tests.
- [ ] Add Bitbucket pagination tests for `next` links and page metadata.
- [ ] Add Bitbucket repository, issue, pull-request, branch, tag, commit, and source decoder tests.
- [ ] Add Forgejo/Gitea repository, issue, pull-request, release, branch, tag, commit, and content decoder tests after API docs are fetched.
- [ ] Add generic custom Git URL-template tests.
- [ ] Add local remote parser tests for GitLab, Bitbucket, Codeberg, Forgejo, Gitea, and custom SSH/HTTPS URLs.
- [ ] Add reference watcher parser tests for GitLab, Bitbucket, Forgejo/Gitea, and custom commit URLs.
- [ ] Add CLI parsing tests for multi-segment provider project paths.
- [ ] Run `pnpm test` for focused coverage during development.
- [ ] Run `pnpm check` before shipping.

## Suggested Implementation Order

1. Add provider/account/repository identity models and settings migration.
2. Isolate token storage by provider and host.
3. Put `GitHubClient` behind a provider-neutral service without changing behavior.
4. Add GitLab PAT login and `GET /api/v4/user` validation.
5. Add GitLab project list/detail mapping.
6. Wire GitLab into refresh with minimal repository cards: project name, stars, forks, open issues, open merge requests, last activity.
7. Add GitLab pipelines, releases, tags, branches, contributors, commits, and contents.
8. Add Bitbucket Cloud API token login and repository list/detail support.
9. Add Bitbucket pull requests, issues, branches, tags, commits, contents, and Pipelines where available.
10. Fetch Forgejo/Gitea docs, then add Codeberg as the first Forgejo-compatible hosted provider.
11. Add Forgejo/Gitea self-hosted configuration and capability probing.
12. Add generic custom Git host support for local-only repository workflows.
13. Update menus/settings/CLI vocabulary and hide unsupported provider features.
14. Add local project matching for all provider remotes.
15. Add provider-aware reference watcher support.
16. Update docs and run full checks.

## Main Risks

- Repository identity is the biggest risk. GitLab/Forgejo subgroup paths and Bitbucket workspace/repo slugs will break any lingering `owner/name` assumption.
- Token collisions are likely unless `TokenStore` is changed before adding any new provider login.
- Feature parity is not exact. Traffic stats, contribution calendar, discussions, releases, CI, and archive support vary by provider and should be hidden or clearly unavailable rather than faked.
- Self-hosted instances have version drift and disabled features. Capability probing is required before rendering feature menus.
- Generic custom Git cannot supply issue/PR/CI counts without a provider API. It should be treated as local-only, not as a degraded API provider.
- UI copy churn can be large. Keep provider vocabulary centralized so the first provider patch does not become a repo-wide string rewrite.
- CLI compatibility needs care because existing commands and tests assume `owner/name`.
