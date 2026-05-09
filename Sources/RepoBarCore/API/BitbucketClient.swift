import Foundation

public actor BitbucketClient: RepositoryService {
    public private(set) var apiHost: URL
    private var credentialProvider: (@Sendable () async throws -> ProviderCredential?)?
    private let tokenStore: TokenStore
    private let runner: BitbucketRequestRunner
    private var repositoryCache: [Repository] = []

    public init(
        apiHost: URL = RepositoryHost.bitbucketCloud.apiBaseURL!,
        tokenStore: TokenStore = .shared,
        session: URLSession = .shared
    ) {
        self.apiHost = apiHost
        self.tokenStore = tokenStore
        self.runner = BitbucketRequestRunner(session: session)
    }

    public var provider: SourceControlProvider {
        .bitbucketCloud
    }

    public var capabilities: ProviderCapabilities {
        .bitbucketCloud
    }

    public func setAPIHost(_ host: URL) {
        guard host.scheme?.lowercased() == "https", host.host != nil else { return }

        self.apiHost = host
    }

    public func setCredentialProvider(_ provider: @Sendable @escaping () async throws -> ProviderCredential?) {
        self.credentialProvider = provider
    }

    public func rateLimitReset(now _: Date = Date()) async -> Date? {
        nil
    }

    public func rateLimitMessage(now _: Date = Date()) async -> String? {
        nil
    }

    public func refreshRateLimitResources() async throws -> RateLimitResourcesSnapshot {
        throw UnsupportedProviderFeature.unsupported(provider: .bitbucketCloud, feature: "rate-limit resources")
    }

    public func repositoryList(limit: Int?) async throws -> [Repository] {
        let permissions = try await self.runner.paginated(
            BitbucketRepositoryPermission.self,
            apiHost: self.apiHost,
            path: "user/permissions/repositories",
            limit: limit,
            credential: self.credential()
        )
        let repositories = permissions.map { self.repository(from: $0.repository) }
        self.repositoryCache = repositories
        return repositories
    }

    public func cachedRepositoryList(limit: Int?) async throws -> [Repository] {
        if self.repositoryCache.isEmpty {
            return try await self.repositoryList(limit: limit)
        }
        guard let limit else { return self.repositoryCache }

        return Array(self.repositoryCache.prefix(limit))
    }

    public func currentUser() async throws -> UserIdentity {
        let user = try await self.runner.get(
            BitbucketUser.self,
            apiHost: self.apiHost,
            path: "user",
            credential: self.credential()
        )
        return UserIdentity(username: Self.displayName(for: user), host: RepositoryHost.bitbucketCloud.webBaseURL)
    }

    public func searchRepositories(matching query: String) async throws -> [Repository] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.isEmpty == false else { return try await self.repositoryList(limit: 20) }

        let permissions = try await self.runner.paginated(
            BitbucketRepositoryPermission.self,
            apiHost: self.apiHost,
            path: "user/permissions/repositories",
            queryItems: [URLQueryItem(name: "q", value: #"repository.name~"\#(trimmed)""#)],
            limit: 20,
            credential: self.credential()
        )
        return permissions.map { self.repository(from: $0.repository) }
    }

    public func recentRepositories(limit: Int = 8) async throws -> [Repository] {
        try await self.repositoryList(limit: limit)
    }

    public func prefetchedRepositories(max: Int = RepoCacheConstants.maxRepositoriesToPrefetch) async throws -> [Repository] {
        try await self.repositoryList(limit: max)
    }

    public func defaultRepositories(limit: Int, for _: String) async throws -> [Repository] {
        try await self.repositoryList(limit: limit)
    }

    public func activityRepositories(limit: Int?) async throws -> [Repository] {
        try await self.repositoryList(limit: limit)
    }

    public func fullRepository(owner: String, name: String) async throws -> Repository {
        let item = try await self.runner.get(
            BitbucketRepository.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)",
            credential: self.credential()
        )
        return self.repository(from: item)
    }

    public func latestRelease(owner _: String, name _: String) async throws -> Release? {
        nil
    }

    public func userActivityEvents(username _: String, scope _: GlobalActivityScope, limit _: Int) async throws -> [ActivityEvent] {
        []
    }

    public func userCommitEvents(username _: String, scope _: GlobalActivityScope, limit: Int) async throws -> [RepoCommitSummary] {
        let repos = try await self.repositoryList(limit: min(max(limit, 1), 20))
        var commits: [RepoCommitSummary] = []
        for repo in repos {
            let repoCommits = try await self.recentCommits(owner: repo.owner, name: repo.name, limit: max(1, limit - commits.count)).items
            commits.append(contentsOf: repoCommits)
            if commits.count >= limit { break }
        }
        return Array(commits.sorted { $0.authoredAt > $1.authoredAt }.prefix(limit))
    }

    public func userContributionHeatmap(login _: String) async throws -> [HeatmapCell] {
        throw UnsupportedProviderFeature.unsupported(provider: .bitbucketCloud, feature: "contribution heatmap")
    }

    public func cachedReferenceMatches(query _: GitHubReferenceQuery, repositories _: [Repository], limit _: Int = 20) async -> [GitHubReferenceMatch] {
        []
    }

    public func liveReferenceMatch(query _: GitHubReferenceQuery, repositories _: [Repository]) async -> GitHubReferenceMatch? {
        nil
    }

    public func liveReferenceMatch(query _: GitHubReferenceQuery) async -> GitHubReferenceMatch? {
        nil
    }

    public func recentPullRequests(owner: String, name: String, limit: Int = 20) async throws -> [RepoPullRequestSummary] {
        let items = try await self.runner.paginated(
            BitbucketPullRequest.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/pullrequests",
            queryItems: [URLQueryItem(name: "state", value: "OPEN")],
            limit: limit,
            credential: self.credential()
        )
        return items.map(Self.pullRequestSummary)
    }

    public func recentIssues(owner: String, name: String, limit: Int = 20) async throws -> [RepoIssueSummary] {
        guard self.cachedRepository(owner: owner, name: name)?.identity.providerSpecificID != nil
            || self.cachedRepository(owner: owner, name: name)?.openIssues != 0
        else { return [] }

        let items = try await self.runner.paginated(
            BitbucketIssue.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/issues",
            queryItems: [URLQueryItem(name: "q", value: #"state="open""#)],
            limit: limit,
            credential: self.credential()
        )
        return items.map(Self.issueSummary)
    }

    public func recentReleases(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoReleaseSummary] {
        []
    }

    public func recentWorkflowRuns(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoWorkflowRunSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .bitbucketCloud, feature: "pipelines")
    }

    public func recentCommits(owner: String, name: String, limit: Int = 20) async throws -> RepoCommitList {
        let items = try await self.runner.paginated(
            BitbucketCommit.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/commits",
            limit: limit,
            credential: self.credential()
        )
        return RepoCommitList(items: items.map { Self.commitSummary($0, repoFullName: "\(owner)/\(name)") }, totalCount: nil)
    }

    public func recentDiscussions(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoDiscussionSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .bitbucketCloud, feature: "discussions")
    }

    public func recentTags(owner: String, name: String, limit: Int = 20) async throws -> [RepoTagSummary] {
        let items = try await self.runner.paginated(
            BitbucketRef.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/refs/tags",
            limit: limit,
            credential: self.credential()
        )
        return items.map { RepoTagSummary(name: $0.name, commitSHA: $0.target?.hash ?? "") }
    }

    public func recentBranches(owner: String, name: String, limit: Int = 20) async throws -> [RepoBranchSummary] {
        let items = try await self.runner.paginated(
            BitbucketRef.self,
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/refs/branches",
            limit: limit,
            credential: self.credential()
        )
        return items.map { RepoBranchSummary(name: $0.name, commitSHA: $0.target?.hash ?? "", isProtected: false) }
    }

    public func repoContents(owner: String, name: String, path: String? = nil) async throws -> [RepoContentItem] {
        let suffix = [owner, name, "src", "HEAD", path ?? ""]
            .filter { $0.isEmpty == false }
            .joined(separator: "/")
        let items = try await self.runner.paginated(
            BitbucketSourceItem.self,
            apiHost: self.apiHost,
            path: "repositories/\(suffix)",
            limit: nil,
            credential: self.credential()
        )
        return items.map(Self.contentItem)
    }

    public func repoFileContents(owner: String, name: String, path: String) async throws -> Data {
        try await self.runner.data(
            apiHost: self.apiHost,
            path: "repositories/\(owner)/\(name)/src/HEAD/\(path)",
            credential: self.credential()
        )
    }

    public func topContributors(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoContributorSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .bitbucketCloud, feature: "contributors")
    }

    public func diagnostics() async -> DiagnosticsSummary {
        .empty
    }

    public func clearCache() async {
        self.repositoryCache = []
    }

    public func clearRepoDetailCache() async {}

    private func credential() async throws -> ProviderCredential {
        if let credential = try await self.credentialProvider?() {
            return credential
        }
        if let credential = try self.tokenStore.loadCredential(
            provider: .bitbucketCloud,
            host: RepositoryHost.bitbucketCloud.webBaseURL,
            kind: .apiToken
        ) {
            return credential
        }
        throw BitbucketAPIError.missingCredential
    }

    private func repository(from item: BitbucketRepository) -> Repository {
        let parts = item.fullName.split(separator: "/", maxSplits: 1).map(String.init)
        let owner = parts.first ?? item.owner?.username ?? item.owner?.nickname ?? ""
        let slug = parts.count > 1 ? parts[1] : item.name
        let identity = RepositoryIdentity(
            provider: .bitbucketCloud,
            id: item.uuid,
            name: slug,
            namespacePath: owner,
            pathWithNamespace: item.fullName,
            slug: slug,
            webURL: item.links?.html?.href,
            apiURL: item.links?.selfLink?.href,
            providerSpecificID: item.uuid
        )
        return Repository(
            id: item.uuid,
            name: slug,
            owner: owner,
            identity: identity,
            isFork: false,
            isArchived: false,
            viewerCanRead: true,
            sortOrder: nil,
            error: nil,
            rateLimitedUntil: nil,
            ciStatus: .unknown,
            openIssues: item.hasIssues == true ? 1 : 0,
            openPulls: 0,
            stars: 0,
            forks: 0,
            pushedAt: item.updatedOn,
            latestRelease: nil,
            latestActivity: nil,
            traffic: nil,
            heatmap: []
        )
    }

    private func cachedRepository(owner: String, name: String) -> Repository? {
        self.repositoryCache.first { $0.owner == owner && $0.name == name }
    }

    private static func displayName(for user: BitbucketUser) -> String {
        if let nickname = user.nickname, nickname.isEmpty == false { return nickname }
        if let username = user.username, username.isEmpty == false { return username }
        if let displayName = user.displayName, displayName.isEmpty == false { return displayName }
        return user.uuid ?? ""
    }

    private static func pullRequestSummary(_ pullRequest: BitbucketPullRequest) -> RepoPullRequestSummary {
        RepoPullRequestSummary(
            number: pullRequest.id,
            title: pullRequest.title,
            url: pullRequest.links?.html?.href ?? URL(string: "https://bitbucket.org")!,
            updatedAt: pullRequest.updatedOn ?? pullRequest.createdOn ?? Date.distantPast,
            createdAt: pullRequest.createdOn,
            authorLogin: pullRequest.author.map(self.displayName),
            authorAvatarURL: nil,
            isDraft: false,
            commentCount: pullRequest.commentCount ?? 0,
            reviewCommentCount: pullRequest.taskCount ?? 0,
            labels: [],
            headRefName: pullRequest.source?.branch?.name,
            baseRefName: pullRequest.destination?.branch?.name
        )
    }

    private static func issueSummary(_ issue: BitbucketIssue) -> RepoIssueSummary {
        RepoIssueSummary(
            number: issue.id,
            title: issue.title,
            url: issue.links?.html?.href ?? URL(string: "https://bitbucket.org")!,
            updatedAt: issue.updatedOn ?? issue.createdOn ?? Date.distantPast,
            createdAt: issue.createdOn,
            authorLogin: issue.reporter.map(self.displayName),
            authorAvatarURL: nil,
            assigneeLogins: issue.assignee.map { [Self.displayName(for: $0)] } ?? [],
            commentCount: issue.commentCount ?? 0,
            labels: []
        )
    }

    private static func commitSummary(_ commit: BitbucketCommit, repoFullName: String) -> RepoCommitSummary {
        RepoCommitSummary(
            sha: commit.hash,
            message: commit.message ?? commit.hash,
            url: commit.links?.html?.href ?? URL(string: "https://bitbucket.org")!,
            authoredAt: commit.date ?? Date.distantPast,
            authorName: commit.author?.raw,
            authorLogin: commit.author?.user.map(self.displayName),
            authorAvatarURL: nil,
            repoFullName: repoFullName
        )
    }

    private static func contentItem(_ item: BitbucketSourceItem) -> RepoContentItem {
        let type: RepoContentType = switch item.type {
        case "commit_file":
            .file
        case "commit_directory":
            .dir
        default:
            .unknown
        }
        return RepoContentItem(
            name: URL(fileURLWithPath: item.path).lastPathComponent,
            path: item.path,
            type: type,
            size: item.size,
            url: item.links?.selfLink?.href ?? item.links?.meta?.href ?? URL(string: "https://bitbucket.org")!,
            htmlURL: item.links?.html?.href,
            downloadURL: item.links?.selfLink?.href
        )
    }
}
