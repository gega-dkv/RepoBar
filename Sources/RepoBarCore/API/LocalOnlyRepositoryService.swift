import Foundation

public actor LocalOnlyRepositoryService: RepositoryService {
    public let provider: SourceControlProvider = .customGit
    public let capabilities: ProviderCapabilities = .customGit

    public init() {}

    public func rateLimitReset(now _: Date = Date()) async -> Date? {
        nil
    }

    public func rateLimitMessage(now _: Date = Date()) async -> String? {
        nil
    }

    public func refreshRateLimitResources() async throws -> RateLimitResourcesSnapshot {
        throw self.unsupported("rate-limit resources")
    }

    public func repositoryList(limit _: Int?) async throws -> [Repository] {
        []
    }

    public func cachedRepositoryList(limit _: Int?) async throws -> [Repository] {
        []
    }

    public func currentUser() async throws -> UserIdentity {
        throw self.unsupported("current user")
    }

    public func searchRepositories(matching _: String) async throws -> [Repository] {
        []
    }

    public func recentRepositories(limit _: Int = 8) async throws -> [Repository] {
        []
    }

    public func prefetchedRepositories(max _: Int = RepoCacheConstants.maxRepositoriesToPrefetch) async throws -> [Repository] {
        []
    }

    public func defaultRepositories(limit _: Int, for _: String) async throws -> [Repository] {
        []
    }

    public func activityRepositories(limit _: Int?) async throws -> [Repository] {
        []
    }

    public func fullRepository(owner: String, name: String) async throws -> Repository {
        Repository.placeholder(
            owner: owner,
            name: name,
            error: "Custom Git hosts do not expose repository details.",
            rateLimitedUntil: nil
        )
    }

    public func latestRelease(owner _: String, name _: String) async throws -> Release? {
        nil
    }

    public func userActivityEvents(
        username _: String,
        scope _: GlobalActivityScope,
        limit _: Int
    ) async throws -> [ActivityEvent] {
        []
    }

    public func userCommitEvents(
        username _: String,
        scope _: GlobalActivityScope,
        limit _: Int
    ) async throws -> [RepoCommitSummary] {
        []
    }

    public func userContributionHeatmap(login _: String) async throws -> [HeatmapCell] {
        throw self.unsupported("contribution heatmap")
    }

    public func cachedReferenceMatches(
        query _: GitHubReferenceQuery,
        repositories _: [Repository],
        limit _: Int = 20
    ) async -> [GitHubReferenceMatch] {
        []
    }

    public func liveReferenceMatch(
        query _: GitHubReferenceQuery,
        repositories _: [Repository]
    ) async -> GitHubReferenceMatch? {
        nil
    }

    public func liveReferenceMatch(query _: GitHubReferenceQuery) async -> GitHubReferenceMatch? {
        nil
    }

    public func recentPullRequests(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoPullRequestSummary] {
        throw self.unsupported("pull requests")
    }

    public func recentIssues(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoIssueSummary] {
        throw self.unsupported("issues")
    }

    public func recentReleases(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoReleaseSummary] {
        throw self.unsupported("releases")
    }

    public func recentWorkflowRuns(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoWorkflowRunSummary] {
        throw self.unsupported("workflow runs")
    }

    public func recentCommits(owner _: String, name _: String, limit _: Int = 20) async throws -> RepoCommitList {
        throw self.unsupported("commits")
    }

    public func recentDiscussions(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoDiscussionSummary] {
        throw self.unsupported("discussions")
    }

    public func recentTags(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoTagSummary] {
        throw self.unsupported("tags")
    }

    public func recentBranches(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoBranchSummary] {
        throw self.unsupported("branches")
    }

    public func repoContents(owner _: String, name _: String, path _: String? = nil) async throws -> [RepoContentItem] {
        throw self.unsupported("repository contents")
    }

    public func repoFileContents(owner _: String, name _: String, path _: String) async throws -> Data {
        throw self.unsupported("repository file contents")
    }

    public func topContributors(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoContributorSummary] {
        throw self.unsupported("contributors")
    }

    public func diagnostics() async -> DiagnosticsSummary {
        .empty
    }

    public func clearCache() async {}

    public func clearRepoDetailCache() async {}

    private func unsupported(_ feature: String) -> UnsupportedProviderFeature {
        .unsupported(provider: .customGit, feature: feature)
    }
}
