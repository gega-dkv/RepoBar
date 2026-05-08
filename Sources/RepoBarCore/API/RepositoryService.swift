import Foundation

public enum UnsupportedProviderFeature: Error, Equatable, Sendable {
    case unsupported(provider: SourceControlProvider, feature: String)

    public var provider: SourceControlProvider {
        switch self {
        case let .unsupported(provider, _):
            provider
        }
    }

    public var feature: String {
        switch self {
        case let .unsupported(_, feature):
            feature
        }
    }
}

extension UnsupportedProviderFeature: LocalizedError {
    public var errorDescription: String? {
        switch self {
        case let .unsupported(provider, feature):
            "\(provider.label) does not support \(feature)."
        }
    }
}

public protocol RepositoryService: Sendable {
    var provider: SourceControlProvider { get async }
    var capabilities: ProviderCapabilities { get async }

    func rateLimitReset(now: Date) async -> Date?
    func rateLimitMessage(now: Date) async -> String?
    func refreshRateLimitResources() async throws -> RateLimitResourcesSnapshot
    func repositoryList(limit: Int?) async throws -> [Repository]
    func cachedRepositoryList(limit: Int?) async throws -> [Repository]
    func currentUser() async throws -> UserIdentity
    func searchRepositories(matching query: String) async throws -> [Repository]
    func recentRepositories(limit: Int) async throws -> [Repository]
    func prefetchedRepositories(max: Int) async throws -> [Repository]
    func defaultRepositories(limit: Int, for username: String) async throws -> [Repository]
    func activityRepositories(limit: Int?) async throws -> [Repository]
    func fullRepository(owner: String, name: String) async throws -> Repository
    func latestRelease(owner: String, name: String) async throws -> Release?
    func userActivityEvents(username: String, scope: GlobalActivityScope, limit: Int) async throws -> [ActivityEvent]
    func userCommitEvents(username: String, scope: GlobalActivityScope, limit: Int) async throws -> [RepoCommitSummary]
    func userContributionHeatmap(login: String) async throws -> [HeatmapCell]
    func cachedReferenceMatches(query: GitHubReferenceQuery, repositories: [Repository], limit: Int) async -> [GitHubReferenceMatch]
    func liveReferenceMatch(query: GitHubReferenceQuery, repositories: [Repository]) async -> GitHubReferenceMatch?
    func liveReferenceMatch(query: GitHubReferenceQuery) async -> GitHubReferenceMatch?
    func recentPullRequests(owner: String, name: String, limit: Int) async throws -> [RepoPullRequestSummary]
    func recentIssues(owner: String, name: String, limit: Int) async throws -> [RepoIssueSummary]
    func recentReleases(owner: String, name: String, limit: Int) async throws -> [RepoReleaseSummary]
    func recentWorkflowRuns(owner: String, name: String, limit: Int) async throws -> [RepoWorkflowRunSummary]
    func recentCommits(owner: String, name: String, limit: Int) async throws -> RepoCommitList
    func recentDiscussions(owner: String, name: String, limit: Int) async throws -> [RepoDiscussionSummary]
    func recentTags(owner: String, name: String, limit: Int) async throws -> [RepoTagSummary]
    func recentBranches(owner: String, name: String, limit: Int) async throws -> [RepoBranchSummary]
    func repoContents(owner: String, name: String, path: String?) async throws -> [RepoContentItem]
    func repoFileContents(owner: String, name: String, path: String) async throws -> Data
    func topContributors(owner: String, name: String, limit: Int) async throws -> [RepoContributorSummary]
    func diagnostics() async -> DiagnosticsSummary
    func clearCache() async
    func clearRepoDetailCache() async
}

public extension RepositoryService {
    func rateLimitReset(now: Date = Date()) async -> Date? {
        await self.rateLimitReset(now: now)
    }

    func rateLimitMessage(now: Date = Date()) async -> String? {
        await self.rateLimitMessage(now: now)
    }

    func cachedReferenceMatches(
        query: GitHubReferenceQuery,
        repositories: [Repository],
        limit: Int = 20
    ) async -> [GitHubReferenceMatch] {
        await self.cachedReferenceMatches(query: query, repositories: repositories, limit: limit)
    }

    func recentRepositories(limit: Int = 8) async throws -> [Repository] {
        try await self.recentRepositories(limit: limit)
    }

    func prefetchedRepositories(max: Int = RepoCacheConstants.maxRepositoriesToPrefetch) async throws -> [Repository] {
        try await self.prefetchedRepositories(max: max)
    }

    func recentPullRequests(owner: String, name: String, limit: Int = 20) async throws -> [RepoPullRequestSummary] {
        try await self.recentPullRequests(owner: owner, name: name, limit: limit)
    }

    func recentIssues(owner: String, name: String, limit: Int = 20) async throws -> [RepoIssueSummary] {
        try await self.recentIssues(owner: owner, name: name, limit: limit)
    }

    func recentReleases(owner: String, name: String, limit: Int = 20) async throws -> [RepoReleaseSummary] {
        try await self.recentReleases(owner: owner, name: name, limit: limit)
    }

    func recentWorkflowRuns(owner: String, name: String, limit: Int = 20) async throws -> [RepoWorkflowRunSummary] {
        try await self.recentWorkflowRuns(owner: owner, name: name, limit: limit)
    }

    func recentCommits(owner: String, name: String, limit: Int = 20) async throws -> RepoCommitList {
        try await self.recentCommits(owner: owner, name: name, limit: limit)
    }

    func recentDiscussions(owner: String, name: String, limit: Int = 20) async throws -> [RepoDiscussionSummary] {
        try await self.recentDiscussions(owner: owner, name: name, limit: limit)
    }

    func recentTags(owner: String, name: String, limit: Int = 20) async throws -> [RepoTagSummary] {
        try await self.recentTags(owner: owner, name: name, limit: limit)
    }

    func recentBranches(owner: String, name: String, limit: Int = 20) async throws -> [RepoBranchSummary] {
        try await self.recentBranches(owner: owner, name: name, limit: limit)
    }

    func repoContents(owner: String, name: String, path: String? = nil) async throws -> [RepoContentItem] {
        try await self.repoContents(owner: owner, name: name, path: path)
    }

    func topContributors(owner: String, name: String, limit: Int = 20) async throws -> [RepoContributorSummary] {
        try await self.topContributors(owner: owner, name: name, limit: limit)
    }
}

public extension GitHubClient {
    var provider: SourceControlProvider {
        .github
    }

    var capabilities: ProviderCapabilities {
        .github
    }
}

extension GitHubClient: RepositoryService {}
