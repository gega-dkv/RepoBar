import Foundation
import Observation
import RepoBarCore

// MARK: - AppState container

@MainActor
@Observable
final class AppState {
    var session = Session()
    let auth = OAuthCoordinator()
    let patAuth = PATAuthenticator()
    let github = RepositoryServiceRouter()
    let refreshScheduler = RefreshScheduler()
    let settingsStore = SettingsStore()
    let localRepoManager = LocalRepoManager()
    let accessibilityPermission = AccessibilityPermissionManager()
    let menuRefreshInterval: TimeInterval = 30
    var refreshTask: Task<Void, Never>?
    var localProjectsTask: Task<Void, Never>?
    private var tokenRefreshTask: Task<Void, Never>?
    private var accessibilityPermissionTask: Task<Void, Never>?
    var menuRefreshTask: Task<Void, Never>?
    private var keyboardIssueMonitor: KeyboardIssueMonitor?
    var refreshTaskToken = UUID()
    let hydrateConcurrencyLimit = 4
    var prefetchTask: Task<Void, Never>?
    private let tokenRefreshInterval: TimeInterval = 300
    let menuRefreshDebounceInterval: TimeInterval = 1
    var lastMenuRefreshRequest: Date?

    // Default GitHub App values for convenience login from the main window.
    let defaultClientID = RepoBarAuthDefaults.clientID
    let defaultClientSecret = RepoBarAuthDefaults.clientSecret
    let defaultLoopbackPort = RepoBarAuthDefaults.loopbackPort
    let defaultGitHubHost = RepoBarAuthDefaults.githubHost
    let defaultAPIHost = RepoBarAuthDefaults.apiHost

    init() {
        self.session.settings = self.settingsStore.load()
        self.reloadRateLimitCacheSummary()
        RepoBarLogging.bootstrapIfNeeded()
        RepoBarLogging.configure(
            verbosity: self.session.settings.loggingVerbosity,
            fileLoggingEnabled: self.session.settings.fileLoggingEnabled
        )
        let initialProvider = self.session.settings.selectedProvider
        let initialHost = Self.credentialHost(from: self.session.settings)
        let storedOAuthTokens = initialProvider == .github ? self.auth.loadTokens(host: initialHost) : nil
        let storedPAT = initialProvider == .github
            ? self.patAuth.loadPAT(host: initialHost)
            : (try? TokenStore.shared.loadCredential(provider: initialProvider, host: initialHost, kind: .pat))?.token
        let storedAPIToken = (try? TokenStore.shared.loadCredential(provider: initialProvider, host: initialHost, kind: .apiToken)) != nil
        self.session.hasStoredTokens = (storedOAuthTokens != nil) || (storedPAT != nil) || storedAPIToken
        let inferredAuthMethod: AuthMethod = storedAPIToken ? .apiToken : (storedPAT != nil ? .pat : .oauth)
        if self.session.settings.authMethod != inferredAuthMethod {
            self.session.settings.authMethod = inferredAuthMethod
            self.settingsStore.save(self.session.settings)
        }
        // Capture tokenStore separately for Sendable compliance
        let tokenStore = TokenStore.shared
        Task {
            await self.github.setProvider(initialProvider)
            await self.github.setAPIHost(Self.apiHost(from: self.session.settings))
            await self.github.setTokenProvider { @Sendable [weak self] () async throws -> OAuthTokens? in
                guard let self else { return nil }

                let (provider, authMethod, host) = await MainActor.run {
                    (
                        self.session.settings.selectedProvider,
                        self.session.settings.authMethod,
                        Self.credentialHost(from: self.session.settings)
                    )
                }
                switch (provider, authMethod) {
                case (.github, .pat):
                    if let pat = try? tokenStore.loadPAT(provider: .github, host: host) {
                        return OAuthTokens(accessToken: pat, refreshToken: "", expiresAt: nil)
                    }
                case (.github, .oauth):
                    return try? await self.auth.refreshIfNeeded(host: host)
                case (.gitlab, .pat):
                    if let credential = try? tokenStore.loadCredential(provider: .gitlab, host: host, kind: .pat) {
                        return OAuthTokens(accessToken: credential.token, refreshToken: "", expiresAt: nil)
                    }
                case (.gitlab, .oauth):
                    if let credential = try? tokenStore.loadCredential(provider: .gitlab, host: host, kind: .oauth) {
                        return OAuthTokens(
                            accessToken: credential.token,
                            refreshToken: credential.refreshToken ?? "",
                            expiresAt: credential.expiresAt
                        )
                    }
                case (.bitbucketCloud, .apiToken):
                    if let credential = try? tokenStore.loadCredential(provider: .bitbucketCloud, host: host, kind: .apiToken) {
                        return OAuthTokens(accessToken: credential.token, refreshToken: "", expiresAt: nil)
                    }
                case (.forgejo, .pat), (.gitea, .pat):
                    if let credential = try? tokenStore.loadCredential(provider: provider, host: host, kind: .pat) {
                        return OAuthTokens(accessToken: credential.token, refreshToken: "", expiresAt: nil)
                    }
                case (.customGit, _), _:
                    return nil
                }
                return nil
            }
        }
        self.tokenRefreshTask = Task { [weak self] in
            guard let self else { return }

            while !Task.isCancelled {
                let host = self.session.settings.enterpriseHost ?? self.session.settings.githubHost
                if self.session.settings.authMethod == .oauth, self.auth.loadTokens(host: host) != nil {
                    _ = try? await self.auth.refreshIfNeeded(host: host)
                }
                try? await Task.sleep(for: .seconds(self.tokenRefreshInterval))
            }
        }
        self.refreshScheduler.configure(interval: self.session.settings.refreshInterval.seconds) { [weak self] in
            self?.requestRefresh()
        }
        Task { await DiagnosticsLogger.shared.setEnabled(self.session.settings.diagnosticsEnabled) }
        Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(250))
            await self?.refreshRateLimitDisplayState()
        }
        self.accessibilityPermissionTask = Task { [weak self] in
            guard let self else { return }

            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(2))
                if self.accessibilityPermission.refresh() {
                    self.updateKeyboardIssueMonitor()
                }
            }
        }
        self.updateKeyboardIssueMonitor()
    }

    struct GlobalActivityResult {
        let events: [ActivityEvent]
        let commits: [RepoCommitSummary]
        let error: String?
        let commitError: String?
    }

    static func credentialHost(from settings: UserSettings) -> URL {
        if let host = settings.repositoryHosts.first(where: { $0.provider == settings.selectedProvider }) {
            return host.webBaseURL
        }
        switch settings.selectedProvider {
        case .github:
            return settings.enterpriseHost ?? settings.githubHost
        case .gitlab:
            return RepositoryHost.gitlabCom.webBaseURL
        case .bitbucketCloud:
            return RepositoryHost.bitbucketCloud.webBaseURL
        case .forgejo:
            return RepositoryHost.codeberg.webBaseURL
        case .gitea, .customGit:
            return URL(string: "https://git.example.com")!
        }
    }

    static func apiHost(from settings: UserSettings) -> URL {
        if let host = settings.repositoryHosts.first(where: { $0.provider == settings.selectedProvider }) {
            if let apiBaseURL = host.apiBaseURL {
                return apiBaseURL
            }
        }
        switch settings.selectedProvider {
        case .github:
            if let enterprise = settings.enterpriseHost { return enterprise.appending(path: "/api/v3") }
            return RepoBarAuthDefaults.apiHost
        case .gitlab:
            return RepositoryHost.gitlabCom.apiBaseURL!
        case .bitbucketCloud:
            return RepositoryHost.bitbucketCloud.apiBaseURL!
        case .forgejo:
            return RepositoryHost.codeberg.apiBaseURL!
        case .gitea, .customGit:
            return Self.credentialHost(from: settings)
        }
    }

    func diagnostics() async -> DiagnosticsSummary {
        await self.refreshRateLimitDisplayState()
        return self.session.rateLimitDiagnostics
    }

    func refreshRateLimitDisplayState() async {
        _ = try? await self.github.refreshRateLimitResources()
        let diagnostics = await self.github.diagnostics()
        let cacheSummary = try? RepoBarPersistentCache.summary(limit: 100)
        self.session.rateLimitReset = await self.github.rateLimitReset()
        self.session.rateLimitDiagnostics = diagnostics
        self.session.rateLimitCacheSummary = cacheSummary
        NotificationCenter.default.post(name: .menuDiagnosticsDidChange, object: nil)
    }

    func reloadRateLimitCacheSummary(limit: Int = 100) {
        self.session.rateLimitCacheSummary = try? RepoBarPersistentCache.summary(limit: limit)
    }

    func clearCaches() async {
        await self.github.clearCache()
        ContributionCacheStore.clear()
    }

    func persistSettings() {
        self.settingsStore.save(self.session.settings)
    }

    func updateKeyboardIssueMonitor() {
        guard self.session.settings.issueNumberMonitor.enabled else {
            Task { await DiagnosticsLogger.shared.message("keyboard reference monitor disabled") }
            self.keyboardIssueMonitor?.stop()
            self.keyboardIssueMonitor = nil
            self.setKeyboardIssueMatch(nil)
            return
        }

        if self.keyboardIssueMonitor == nil {
            Task { await DiagnosticsLogger.shared.message("keyboard reference monitor created") }
            self.keyboardIssueMonitor = KeyboardIssueMonitor(
                onPasteboardWithoutReference: { [weak self] in
                    await self?.clearTypedGitHubReference()
                },
                onReference: { [weak self] query in
                    await self?.resolveTypedGitHubReference(query)
                }
            )
        }
        let includeKeyboardEvents = self.session.settings.issueNumberMonitor.typedReferencesEnabled && self.accessibilityPermission.isTrusted
        let mode = includeKeyboardEvents ? "keyboard+clipboard" : "clipboard-only"
        Task { await DiagnosticsLogger.shared.message("GitHub reference monitor started mode=\(mode)") }
        self.keyboardIssueMonitor?.start(includeKeyboardEvents: includeKeyboardEvents)
    }

    private func clearTypedGitHubReference() async {
        guard self.session.settings.issueNumberMonitor.enabled else { return }

        self.setKeyboardIssueMatch(nil)
    }

    private func resolveTypedGitHubReference(_ query: GitHubReferenceQuery) async {
        guard self.session.settings.issueNumberMonitor.enabled else { return }

        let repositories = self.githubReferenceCandidateRepositories()
        let candidateRepositories = if let repositoryFullName = query.repositoryFullName {
            repositories.filter { $0.fullName.caseInsensitiveCompare(repositoryFullName) == .orderedSame }
        } else {
            repositories
        }
        guard candidateRepositories.isEmpty == false else {
            await self.setKeyboardIssueMatch(self.github.liveReferenceMatch(query: query))
            return
        }

        let cachedMatches = await self.github.cachedReferenceMatches(
            query: query,
            repositories: candidateRepositories,
            limit: AppLimits.IssueNumberMonitor.cacheLookupLimit
        )
        if let match = GitHubReferenceMatch.newestCreated(in: cachedMatches) {
            self.setKeyboardIssueMatch(match)
            return
        }

        let liveMatch = await self.github.liveReferenceMatch(
            query: query,
            repositories: Array(candidateRepositories.prefix(AppLimits.IssueNumberMonitor.liveLookupLimit))
        )
        if let liveMatch {
            self.setKeyboardIssueMatch(liveMatch)
            return
        }

        await self.setKeyboardIssueMatch(self.github.liveReferenceMatch(query: query))
    }

    private func githubReferenceCandidateRepositories() -> [Repository] {
        let sources = [
            self.session.accessibleRepositories,
            self.session.repositories,
            self.session.menuSnapshot?.repositories ?? []
        ]
        let repositories = sources.first(where: { $0.isEmpty == false }) ?? []
        var seen: Set<String> = []
        return repositories.filter { repo in
            guard repo.viewerCanRead else { return false }

            return seen.insert(repo.fullName.lowercased()).inserted
        }
    }

    private func setKeyboardIssueMatch(_ match: GitHubReferenceMatch?) {
        guard self.session.keyboardIssueMatch != match else { return }

        self.session.keyboardIssueMatch = match
        NotificationCenter.default.post(name: .keyboardIssueMatchDidChange, object: nil)
    }
}
