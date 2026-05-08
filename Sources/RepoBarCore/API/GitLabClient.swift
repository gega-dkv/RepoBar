import Foundation

public actor GitLabClient: RepositoryService {
    public private(set) var apiHost: URL
    private var credentialProvider: (@Sendable () async throws -> ProviderCredential?)?
    private let tokenStore: TokenStore
    private let runner: GitLabRequestRunner
    private var repositoryCache: [Repository] = []

    public init(
        apiHost: URL = RepositoryHost.gitlabCom.apiBaseURL!,
        tokenStore: TokenStore = .shared,
        session: URLSession = .shared
    ) {
        self.apiHost = apiHost
        self.tokenStore = tokenStore
        self.runner = GitLabRequestRunner(session: session)
    }

    public var provider: SourceControlProvider {
        .gitlab
    }

    public var capabilities: ProviderCapabilities {
        .gitlab
    }

    public func setAPIHost(_ host: URL) {
        guard let trusted = try? Self.trustedAPIHost(host) else { return }

        self.apiHost = trusted
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
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "rate-limit resources")
    }

    public func repositoryList(limit: Int?) async throws -> [Repository] {
        let projects = try await self.projects(limit: limit)
        let repositories = projects.map { self.repository(from: $0) }
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
            GitLabUser.self,
            apiHost: self.apiHost,
            path: "user",
            credential: self.credential()
        )
        return UserIdentity(username: user.username, host: self.webHostURL())
    }

    public func searchRepositories(matching query: String) async throws -> [Repository] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.isEmpty == false else { return try await self.repositoryList(limit: 20) }

        let projects = try await self.projects(
            limit: 20,
            queryItems: [URLQueryItem(name: "search", value: trimmed)]
        )
        return projects.map { self.repository(from: $0) }
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
        let project = try await self.project(pathWithNamespace: "\(owner)/\(name)")
        return self.repository(from: project)
    }

    public func latestRelease(owner: String, name: String) async throws -> Release? {
        try await self.recentReleases(owner: owner, name: name, limit: 1).first.map {
            Release(name: $0.name, tag: $0.tag, publishedAt: $0.publishedAt, url: $0.url)
        }
    }

    public func userActivityEvents(username _: String, scope _: GlobalActivityScope, limit _: Int) async throws -> [ActivityEvent] {
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "user activity")
    }

    public func userCommitEvents(username _: String, scope _: GlobalActivityScope, limit _: Int) async throws -> [RepoCommitSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "user commits")
    }

    public func userContributionHeatmap(login _: String) async throws -> [HeatmapCell] {
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "contribution heatmap")
    }

    public func cachedReferenceMatches(
        query _: GitHubReferenceQuery,
        repositories _: [Repository],
        limit _: Int = 20
    ) async -> [GitHubReferenceMatch] {
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
            GitLabMergeRequest.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/merge_requests",
            queryItems: [
                URLQueryItem(name: "state", value: "opened"),
                URLQueryItem(name: "order_by", value: "updated_at"),
                URLQueryItem(name: "sort", value: "desc")
            ],
            limit: limit,
            credential: self.credential()
        )
        return items.map(Self.pullRequestSummary)
    }

    public func recentIssues(owner: String, name: String, limit: Int = 20) async throws -> [RepoIssueSummary] {
        let items = try await self.runner.paginated(
            GitLabIssue.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/issues",
            queryItems: [
                URLQueryItem(name: "state", value: "opened"),
                URLQueryItem(name: "order_by", value: "updated_at"),
                URLQueryItem(name: "sort", value: "desc")
            ],
            limit: limit,
            credential: self.credential()
        )
        return items.map(Self.issueSummary)
    }

    public func recentReleases(owner: String, name: String, limit: Int = 20) async throws -> [RepoReleaseSummary] {
        let items = try await self.runner.paginated(
            GitLabRelease.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/releases",
            queryItems: [URLQueryItem(name: "order_by", value: "released_at"), URLQueryItem(name: "sort", value: "desc")],
            limit: limit,
            credential: self.credential()
        )
        return items.compactMap { self.releaseSummary($0, owner: owner, name: name) }
    }

    public func recentWorkflowRuns(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoWorkflowRunSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "pipelines")
    }

    public func recentCommits(owner: String, name: String, limit: Int = 20) async throws -> RepoCommitList {
        let items = try await self.runner.paginated(
            GitLabCommit.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/commits",
            limit: limit,
            credential: self.credential()
        )
        return RepoCommitList(items: items.map { Self.commitSummary($0, repoFullName: "\(owner)/\(name)") }, totalCount: nil)
    }

    public func recentDiscussions(owner _: String, name _: String, limit _: Int = 20) async throws -> [RepoDiscussionSummary] {
        throw UnsupportedProviderFeature.unsupported(provider: .gitlab, feature: "discussions")
    }

    public func recentTags(owner: String, name: String, limit: Int = 20) async throws -> [RepoTagSummary] {
        let items = try await self.runner.paginated(
            GitLabTag.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/tags",
            limit: limit,
            credential: self.credential()
        )
        return items.map { RepoTagSummary(name: $0.name, commitSHA: $0.commit?.id ?? $0.commit?.shortID ?? "") }
    }

    public func recentBranches(owner: String, name: String, limit: Int = 20) async throws -> [RepoBranchSummary] {
        let items = try await self.runner.paginated(
            GitLabBranch.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/branches",
            limit: limit,
            credential: self.credential()
        )
        return items.map {
            RepoBranchSummary(name: $0.name, commitSHA: $0.commit?.id ?? $0.commit?.shortID ?? "", isProtected: $0.protected ?? false)
        }
    }

    public func repoContents(owner: String, name: String, path: String? = nil) async throws -> [RepoContentItem] {
        var queryItems: [URLQueryItem] = [URLQueryItem(name: "recursive", value: "false")]
        if let path, path.isEmpty == false {
            queryItems.append(URLQueryItem(name: "path", value: path))
        }
        let items = try await self.runner.paginated(
            GitLabTreeItem.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/tree",
            queryItems: queryItems,
            limit: nil,
            credential: self.credential()
        )
        return items.map { self.contentItem($0, owner: owner, name: name) }
    }

    public func repoFileContents(owner: String, name: String, path: String) async throws -> Data {
        try await self.runner.data(
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/files/\(Self.urlPathComponent(path))/raw",
            credential: self.credential()
        )
    }

    public func topContributors(owner: String, name: String, limit: Int = 20) async throws -> [RepoContributorSummary] {
        let items = try await self.runner.paginated(
            GitLabContributor.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.projectID(owner: owner, name: name))/repository/contributors",
            queryItems: [URLQueryItem(name: "order_by", value: "commits"), URLQueryItem(name: "sort", value: "desc")],
            limit: limit,
            credential: self.credential()
        )
        return items.map { RepoContributorSummary(login: $0.name, avatarURL: nil, url: nil, contributions: $0.commits) }
    }

    public func diagnostics() async -> DiagnosticsSummary {
        .empty
    }

    public func clearCache() async {
        self.repositoryCache = []
    }

    public func clearRepoDetailCache() async {}

    private func projects(limit: Int?, queryItems: [URLQueryItem] = []) async throws -> [GitLabProject] {
        try await self.runner.paginated(
            GitLabProject.self,
            apiHost: self.apiHost,
            path: "projects",
            queryItems: [
                URLQueryItem(name: "membership", value: "true"),
                URLQueryItem(name: "simple", value: "true"),
                URLQueryItem(name: "order_by", value: "last_activity_at"),
                URLQueryItem(name: "sort", value: "desc")
            ] + queryItems,
            limit: limit,
            credential: self.credential()
        )
    }

    private func project(pathWithNamespace: String) async throws -> GitLabProject {
        try await self.runner.get(
            GitLabProject.self,
            apiHost: self.apiHost,
            path: "projects/\(Self.urlPathComponent(pathWithNamespace))",
            credential: self.credential()
        )
    }

    private func credential() async throws -> ProviderCredential {
        if let credential = try await self.credentialProvider?() {
            return credential
        }
        if let credential = try self.tokenStore.loadCredential(provider: .gitlab, host: self.webHostURL(), kind: .pat) {
            return credential
        }
        if let credential = try self.tokenStore.loadCredential(provider: .gitlab, host: self.webHostURL(), kind: .oauth) {
            return credential
        }
        throw GitLabAPIError.missingCredential
    }

    private func repository(from project: GitLabProject) -> Repository {
        let namespace = project.namespace?.fullPath
            ?? project.pathWithNamespace.split(separator: "/").dropLast().joined(separator: "/")
        let identity = RepositoryIdentity(
            provider: .gitlab,
            id: String(project.id),
            name: project.path,
            namespacePath: namespace,
            pathWithNamespace: project.pathWithNamespace,
            slug: project.path,
            webURL: project.webURL,
            apiURL: self.apiHost.appending(path: "projects/\(project.id)"),
            providerSpecificID: String(project.id)
        )
        return Repository(
            id: String(project.id),
            name: project.path,
            owner: namespace,
            identity: identity,
            isFork: false,
            isArchived: project.archived,
            viewerCanRead: true,
            sortOrder: nil,
            error: nil,
            rateLimitedUntil: nil,
            ciStatus: .unknown,
            openIssues: project.openIssuesCount ?? 0,
            openPulls: 0,
            stars: project.starCount,
            forks: project.forksCount,
            pushedAt: project.lastActivityAt,
            latestRelease: nil,
            latestActivity: nil,
            traffic: nil,
            heatmap: []
        )
    }

    private func webHostURL() -> URL {
        Self.webHostURL(from: self.apiHost)
    }

    private static func trustedAPIHost(_ host: URL) throws -> URL {
        guard host.scheme?.lowercased() == "https", host.host != nil else { throw GitLabAPIError.invalidHost }

        if host.path.trimmingCharacters(in: CharacterSet(charactersIn: "/")).hasSuffix("api/v4") {
            return host
        }
        return host.appending(path: "api/v4")
    }

    private static func webHostURL(from apiHost: URL) -> URL {
        guard var components = URLComponents(url: apiHost, resolvingAgainstBaseURL: false) else { return apiHost }

        let path = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        if path == "api/v4" || path.hasSuffix("/api/v4") {
            components.path = String(path.dropLast("api/v4".count)).trimmingCharacters(in: CharacterSet(charactersIn: "/"))
            if components.path.isEmpty == false { components.path = "/" + components.path }
        }
        components.query = nil
        components.fragment = nil
        return components.url ?? apiHost
    }

    private static func projectID(owner: String, name: String) -> String {
        self.urlPathComponent("\(owner)/\(name)")
    }

    private static func urlPathComponent(_ value: String) -> String {
        value.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed.subtracting(CharacterSet(charactersIn: "/"))) ?? value
    }

    private static func issueSummary(_ issue: GitLabIssue) -> RepoIssueSummary {
        RepoIssueSummary(
            number: issue.iid,
            title: issue.title,
            url: issue.webURL,
            updatedAt: issue.updatedAt,
            createdAt: issue.createdAt,
            authorLogin: issue.author?.username,
            authorAvatarURL: issue.author?.avatarURL,
            assigneeLogins: issue.assignees?.compactMap(\.username) ?? [],
            commentCount: issue.userNotesCount ?? 0,
            labels: issue.labels?.map { RepoIssueLabel(name: $0, colorHex: "#6b7280") } ?? []
        )
    }

    private static func pullRequestSummary(_ mergeRequest: GitLabMergeRequest) -> RepoPullRequestSummary {
        RepoPullRequestSummary(
            number: mergeRequest.iid,
            title: mergeRequest.title,
            url: mergeRequest.webURL,
            updatedAt: mergeRequest.updatedAt,
            createdAt: mergeRequest.createdAt,
            authorLogin: mergeRequest.author?.username,
            authorAvatarURL: mergeRequest.author?.avatarURL,
            isDraft: mergeRequest.draft ?? mergeRequest.workInProgress ?? false,
            commentCount: mergeRequest.userNotesCount ?? 0,
            reviewCommentCount: 0,
            labels: mergeRequest.labels?.map { RepoIssueLabel(name: $0, colorHex: "#6b7280") } ?? [],
            headRefName: mergeRequest.sourceBranch,
            baseRefName: mergeRequest.targetBranch
        )
    }

    private func releaseSummary(_ release: GitLabRelease, owner: String, name: String) -> RepoReleaseSummary? {
        let publishedAt = release.releasedAt ?? release.createdAt ?? Date.distantPast
        let assets = (release.assets?.sources ?? []) + (release.assets?.links ?? [])
        let mappedAssets = assets.compactMap { asset -> RepoReleaseAssetSummary? in
            guard let url = asset.directAssetURL ?? asset.url else { return nil }

            return RepoReleaseAssetSummary(name: asset.name ?? url.lastPathComponent, sizeBytes: nil, downloadCount: 0, url: url)
        }
        let url = assets.compactMap(\.url).first
            ?? self.webHostURL()
            .appending(path: "\(owner)/\(name)")
            .appending(path: "-/releases")
            .appending(path: release.tagName)

        return RepoReleaseSummary(
            name: release.name ?? release.tagName,
            tag: release.tagName,
            url: url,
            publishedAt: publishedAt,
            isPrerelease: release.upcomingRelease ?? false,
            authorLogin: release.author?.username,
            authorAvatarURL: release.author?.avatarURL,
            assetCount: release.assets?.count ?? mappedAssets.count,
            downloadCount: 0,
            assets: mappedAssets
        )
    }

    private static func commitSummary(_ commit: GitLabCommit, repoFullName: String) -> RepoCommitSummary {
        RepoCommitSummary(
            sha: commit.id,
            message: commit.title ?? commit.message ?? commit.id,
            url: commit.webURL ?? URL(string: "https://gitlab.com")!,
            authoredAt: commit.authoredDate ?? commit.committedDate ?? Date.distantPast,
            authorName: commit.authorName,
            authorLogin: commit.authorEmail,
            authorAvatarURL: nil,
            repoFullName: repoFullName
        )
    }

    private func contentItem(_ item: GitLabTreeItem, owner: String, name: String) -> RepoContentItem {
        let type: RepoContentType = switch item.type {
        case "tree":
            .dir
        case "blob":
            .file
        default:
            .unknown
        }
        let encodedPath = Self.urlPathComponent(item.path)
        let apiURL = self.apiHost.appending(path: "projects/\(Self.projectID(owner: owner, name: name))/repository/files/\(encodedPath)")
        return RepoContentItem(
            name: item.name,
            path: item.path,
            type: type,
            size: nil,
            url: apiURL,
            htmlURL: item.webURL,
            downloadURL: type == .file ? apiURL.appending(path: "raw") : nil
        )
    }
}

private extension CharacterSet {
    func subtracting(_ other: CharacterSet) -> CharacterSet {
        var copy = self
        copy.subtract(other)
        return copy
    }
}
