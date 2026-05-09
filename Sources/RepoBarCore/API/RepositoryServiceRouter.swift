import Foundation

public actor RepositoryServiceRouter: RepositoryService {
    private let githubClient: GitHubClient
    private let gitLabClient: GitLabClient
    private let bitbucketClient: BitbucketClient
    private var activeProvider: SourceControlProvider

    public init(
        provider: SourceControlProvider = .github,
        githubClient: GitHubClient = GitHubClient(),
        gitLabClient: GitLabClient = GitLabClient(),
        bitbucketClient: BitbucketClient = BitbucketClient()
    ) {
        self.activeProvider = provider
        self.githubClient = githubClient
        self.gitLabClient = gitLabClient
        self.bitbucketClient = bitbucketClient
    }

    public var provider: SourceControlProvider {
        self.activeProvider
    }

    public var capabilities: ProviderCapabilities {
        self.activeProvider.defaultCapabilities
    }

    public func setProvider(_ provider: SourceControlProvider) {
        self.activeProvider = provider
    }

    public func setAPIHost(_ host: URL) async {
        switch self.activeProvider {
        case .github:
            await self.githubClient.setAPIHost(host)
        case .gitlab:
            await self.gitLabClient.setAPIHost(host)
        case .bitbucketCloud:
            await self.bitbucketClient.setAPIHost(host)
        case .forgejo, .gitea, .customGit:
            break
        }
    }

    public func setTokenProvider(_ provider: @Sendable @escaping () async throws -> OAuthTokens?) async {
        switch self.activeProvider {
        case .github:
            await self.githubClient.setTokenProvider(provider)
        case .gitlab:
            await self.gitLabClient.setCredentialProvider {
                guard let tokens = try await provider() else { return nil }

                return await ProviderCredential(
                    provider: .gitlab,
                    host: self.gitLabClient.apiHost,
                    kind: tokens.refreshToken.isEmpty ? .pat : .oauth,
                    headerStyle: tokens.refreshToken.isEmpty ? .privateToken : .authorizationBearer,
                    token: tokens.accessToken,
                    refreshToken: tokens.refreshToken,
                    expiresAt: tokens.expiresAt
                )
            }
        case .bitbucketCloud:
            break
        case .forgejo, .gitea, .customGit:
            break
        }
    }

    public func rateLimitReset(now: Date = Date()) async -> Date? {
        switch self.activeProvider {
        case .github:
            await self.githubClient.rateLimitReset(now: now)
        case .gitlab:
            await self.gitLabClient.rateLimitReset(now: now)
        case .bitbucketCloud:
            await self.bitbucketClient.rateLimitReset(now: now)
        case .forgejo, .gitea, .customGit:
            nil
        }
    }

    public func rateLimitMessage(now: Date = Date()) async -> String? {
        switch self.activeProvider {
        case .github:
            await self.githubClient.rateLimitMessage(now: now)
        case .gitlab:
            await self.gitLabClient.rateLimitMessage(now: now)
        case .bitbucketCloud:
            await self.bitbucketClient.rateLimitMessage(now: now)
        case .forgejo, .gitea, .customGit:
            nil
        }
    }

    public func refreshRateLimitResources() async throws -> RateLimitResourcesSnapshot {
        try await self.providerOperation("rate-limit resources", gitLabUnsupported: true) {
            try await self.githubClient.refreshRateLimitResources()
        } gitLab: {
            try await self.gitLabClient.refreshRateLimitResources()
        }
    }

    public func repositoryList(limit: Int?) async throws -> [Repository] {
        try await self.providerOperation("repository list") {
            try await self.githubClient.repositoryList(limit: limit)
        } gitLab: {
            try await self.gitLabClient.repositoryList(limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.repositoryList(limit: limit)
        }
    }

    public func cachedRepositoryList(limit: Int?) async throws -> [Repository] {
        try await self.providerOperation("cached repository list") {
            try await self.githubClient.cachedRepositoryList(limit: limit)
        } gitLab: {
            try await self.gitLabClient.cachedRepositoryList(limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.cachedRepositoryList(limit: limit)
        }
    }

    public func currentUser() async throws -> UserIdentity {
        try await self.providerOperation("current user") {
            try await self.githubClient.currentUser()
        } gitLab: {
            try await self.gitLabClient.currentUser()
        } bitbucket: {
            try await self.bitbucketClient.currentUser()
        }
    }

    public func searchRepositories(matching query: String) async throws -> [Repository] {
        try await self.providerOperation("repository search") {
            try await self.githubClient.searchRepositories(matching: query)
        } gitLab: {
            try await self.gitLabClient.searchRepositories(matching: query)
        } bitbucket: {
            try await self.bitbucketClient.searchRepositories(matching: query)
        }
    }

    public func recentRepositories(limit: Int = 8) async throws -> [Repository] {
        try await self.providerOperation("recent repositories") {
            try await self.githubClient.recentRepositories(limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentRepositories(limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentRepositories(limit: limit)
        }
    }

    public func prefetchedRepositories(max: Int = RepoCacheConstants.maxRepositoriesToPrefetch) async throws -> [Repository] {
        try await self.providerOperation("prefetched repositories") {
            try await self.githubClient.prefetchedRepositories(max: max)
        } gitLab: {
            try await self.gitLabClient.prefetchedRepositories(max: max)
        } bitbucket: {
            try await self.bitbucketClient.prefetchedRepositories(max: max)
        }
    }

    public func defaultRepositories(limit: Int, for username: String) async throws -> [Repository] {
        try await self.providerOperation("default repositories") {
            try await self.githubClient.defaultRepositories(limit: limit, for: username)
        } gitLab: {
            try await self.gitLabClient.defaultRepositories(limit: limit, for: username)
        } bitbucket: {
            try await self.bitbucketClient.defaultRepositories(limit: limit, for: username)
        }
    }

    public func activityRepositories(limit: Int?) async throws -> [Repository] {
        try await self.providerOperation("activity repositories") {
            try await self.githubClient.activityRepositories(limit: limit)
        } gitLab: {
            try await self.gitLabClient.activityRepositories(limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.activityRepositories(limit: limit)
        }
    }

    public func fullRepository(owner: String, name: String) async throws -> Repository {
        try await self.providerOperation("repository details") {
            try await self.githubClient.fullRepository(owner: owner, name: name)
        } gitLab: {
            try await self.gitLabClient.fullRepository(owner: owner, name: name)
        } bitbucket: {
            try await self.bitbucketClient.fullRepository(owner: owner, name: name)
        }
    }

    public func latestRelease(owner: String, name: String) async throws -> Release? {
        try await self.providerOperation("latest release") {
            try await self.githubClient.latestRelease(owner: owner, name: name)
        } gitLab: {
            try await self.gitLabClient.latestRelease(owner: owner, name: name)
        } bitbucket: {
            try await self.bitbucketClient.latestRelease(owner: owner, name: name)
        }
    }

    public func userActivityEvents(
        username: String,
        scope: GlobalActivityScope,
        limit: Int
    ) async throws -> [ActivityEvent] {
        try await self.githubOnly("user activity") {
            try await self.githubClient.userActivityEvents(username: username, scope: scope, limit: limit)
        }
    }

    public func userCommitEvents(
        username: String,
        scope: GlobalActivityScope,
        limit: Int
    ) async throws -> [RepoCommitSummary] {
        try await self.githubOnly("user commits") {
            try await self.githubClient.userCommitEvents(username: username, scope: scope, limit: limit)
        }
    }

    public func userContributionHeatmap(login: String) async throws -> [HeatmapCell] {
        try await self.githubOnly("contribution heatmap") {
            try await self.githubClient.userContributionHeatmap(login: login)
        }
    }

    public func cachedReferenceMatches(
        query: GitHubReferenceQuery,
        repositories: [Repository],
        limit: Int = 20
    ) async -> [GitHubReferenceMatch] {
        guard self.activeProvider == .github else { return [] }

        return await self.githubClient.cachedReferenceMatches(query: query, repositories: repositories, limit: limit)
    }

    public func liveReferenceMatch(
        query: GitHubReferenceQuery,
        repositories: [Repository]
    ) async -> GitHubReferenceMatch? {
        guard self.activeProvider == .github else { return nil }

        return await self.githubClient.liveReferenceMatch(query: query, repositories: repositories)
    }

    public func liveReferenceMatch(query: GitHubReferenceQuery) async -> GitHubReferenceMatch? {
        guard self.activeProvider == .github else { return nil }

        return await self.githubClient.liveReferenceMatch(query: query)
    }

    public func recentPullRequests(owner: String, name: String, limit: Int = 20) async throws -> [RepoPullRequestSummary] {
        try await self.providerOperation("pull requests") {
            try await self.githubClient.recentPullRequests(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentPullRequests(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentPullRequests(owner: owner, name: name, limit: limit)
        }
    }

    public func recentIssues(owner: String, name: String, limit: Int = 20) async throws -> [RepoIssueSummary] {
        try await self.providerOperation("issues") {
            try await self.githubClient.recentIssues(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentIssues(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentIssues(owner: owner, name: name, limit: limit)
        }
    }

    public func recentReleases(owner: String, name: String, limit: Int = 20) async throws -> [RepoReleaseSummary] {
        try await self.providerOperation("releases") {
            try await self.githubClient.recentReleases(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentReleases(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentReleases(owner: owner, name: name, limit: limit)
        }
    }

    public func recentWorkflowRuns(owner: String, name: String, limit: Int = 20) async throws -> [RepoWorkflowRunSummary] {
        try await self.providerOperation("workflow runs", gitLabUnsupported: true) {
            try await self.githubClient.recentWorkflowRuns(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentWorkflowRuns(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentWorkflowRuns(owner: owner, name: name, limit: limit)
        }
    }

    public func recentCommits(owner: String, name: String, limit: Int = 20) async throws -> RepoCommitList {
        try await self.providerOperation("commits") {
            try await self.githubClient.recentCommits(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentCommits(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentCommits(owner: owner, name: name, limit: limit)
        }
    }

    public func recentDiscussions(owner: String, name: String, limit: Int = 20) async throws -> [RepoDiscussionSummary] {
        try await self.providerOperation("discussions", gitLabUnsupported: true) {
            try await self.githubClient.recentDiscussions(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentDiscussions(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentDiscussions(owner: owner, name: name, limit: limit)
        }
    }

    public func recentTags(owner: String, name: String, limit: Int = 20) async throws -> [RepoTagSummary] {
        try await self.providerOperation("tags") {
            try await self.githubClient.recentTags(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentTags(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentTags(owner: owner, name: name, limit: limit)
        }
    }

    public func recentBranches(owner: String, name: String, limit: Int = 20) async throws -> [RepoBranchSummary] {
        try await self.providerOperation("branches") {
            try await self.githubClient.recentBranches(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.recentBranches(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.recentBranches(owner: owner, name: name, limit: limit)
        }
    }

    public func repoContents(owner: String, name: String, path: String? = nil) async throws -> [RepoContentItem] {
        try await self.providerOperation("repository contents") {
            try await self.githubClient.repoContents(owner: owner, name: name, path: path)
        } gitLab: {
            try await self.gitLabClient.repoContents(owner: owner, name: name, path: path)
        } bitbucket: {
            try await self.bitbucketClient.repoContents(owner: owner, name: name, path: path)
        }
    }

    public func repoFileContents(owner: String, name: String, path: String) async throws -> Data {
        try await self.providerOperation("repository file contents") {
            try await self.githubClient.repoFileContents(owner: owner, name: name, path: path)
        } gitLab: {
            try await self.gitLabClient.repoFileContents(owner: owner, name: name, path: path)
        } bitbucket: {
            try await self.bitbucketClient.repoFileContents(owner: owner, name: name, path: path)
        }
    }

    public func topContributors(owner: String, name: String, limit: Int = 20) async throws -> [RepoContributorSummary] {
        try await self.providerOperation("contributors") {
            try await self.githubClient.topContributors(owner: owner, name: name, limit: limit)
        } gitLab: {
            try await self.gitLabClient.topContributors(owner: owner, name: name, limit: limit)
        } bitbucket: {
            try await self.bitbucketClient.topContributors(owner: owner, name: name, limit: limit)
        }
    }

    public func diagnostics() async -> DiagnosticsSummary {
        switch self.activeProvider {
        case .github:
            await self.githubClient.diagnostics()
        case .gitlab:
            await self.gitLabClient.diagnostics()
        case .bitbucketCloud:
            await self.bitbucketClient.diagnostics()
        case .forgejo, .gitea, .customGit:
            .empty
        }
    }

    public func clearCache() async {
        switch self.activeProvider {
        case .github:
            await self.githubClient.clearCache()
        case .gitlab:
            await self.gitLabClient.clearCache()
        case .bitbucketCloud:
            await self.bitbucketClient.clearCache()
        case .forgejo, .gitea, .customGit:
            break
        }
    }

    public func clearRepoDetailCache() async {
        switch self.activeProvider {
        case .github:
            await self.githubClient.clearRepoDetailCache()
        case .gitlab:
            await self.gitLabClient.clearRepoDetailCache()
        case .bitbucketCloud:
            await self.bitbucketClient.clearRepoDetailCache()
        case .forgejo, .gitea, .customGit:
            break
        }
    }

    private func providerOperation<T>(
        _ feature: String,
        gitLabUnsupported _: Bool = false,
        github: () async throws -> T,
        gitLab: () async throws -> T,
        bitbucket: (() async throws -> T)? = nil
    ) async throws -> T {
        switch self.activeProvider {
        case .github:
            return try await github()
        case .gitlab:
            return try await gitLab()
        case .bitbucketCloud:
            guard let bitbucket else {
                throw UnsupportedProviderFeature.unsupported(provider: self.activeProvider, feature: feature)
            }

            return try await bitbucket()
        case .forgejo, .gitea, .customGit:
            throw UnsupportedProviderFeature.unsupported(provider: self.activeProvider, feature: feature)
        }
    }

    private func githubOnly<T>(
        _ feature: String,
        operation: () async throws -> T
    ) async throws -> T {
        guard self.activeProvider == .github else {
            throw UnsupportedProviderFeature.unsupported(provider: self.activeProvider, feature: feature)
        }

        return try await operation()
    }
}
