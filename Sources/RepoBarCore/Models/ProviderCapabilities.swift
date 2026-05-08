import Foundation

public struct ProviderCapabilities: Equatable, Codable, Sendable {
    public var repositories: Bool
    public var issues: Bool
    public var pullRequests: Bool
    public var ci: Bool
    public var releases: Bool
    public var tags: Bool
    public var branches: Bool
    public var commits: Bool
    public var contributors: Bool
    public var repositoryContents: Bool
    public var contributionCalendar: Bool
    public var trafficStats: Bool
    public var discussions: Bool
    public var rateLimitDiagnostics: Bool

    public init(
        repositories: Bool,
        issues: Bool,
        pullRequests: Bool,
        ci: Bool,
        releases: Bool,
        tags: Bool,
        branches: Bool,
        commits: Bool,
        contributors: Bool,
        repositoryContents: Bool,
        contributionCalendar: Bool,
        trafficStats: Bool,
        discussions: Bool,
        rateLimitDiagnostics: Bool
    ) {
        self.repositories = repositories
        self.issues = issues
        self.pullRequests = pullRequests
        self.ci = ci
        self.releases = releases
        self.tags = tags
        self.branches = branches
        self.commits = commits
        self.contributors = contributors
        self.repositoryContents = repositoryContents
        self.contributionCalendar = contributionCalendar
        self.trafficStats = trafficStats
        self.discussions = discussions
        self.rateLimitDiagnostics = rateLimitDiagnostics
    }

    public static let github = ProviderCapabilities(
        repositories: true,
        issues: true,
        pullRequests: true,
        ci: true,
        releases: true,
        tags: true,
        branches: true,
        commits: true,
        contributors: true,
        repositoryContents: true,
        contributionCalendar: true,
        trafficStats: true,
        discussions: true,
        rateLimitDiagnostics: true
    )

    public static let gitlab = ProviderCapabilities(
        repositories: true,
        issues: true,
        pullRequests: true,
        ci: true,
        releases: true,
        tags: true,
        branches: true,
        commits: true,
        contributors: true,
        repositoryContents: true,
        contributionCalendar: false,
        trafficStats: false,
        discussions: false,
        rateLimitDiagnostics: true
    )

    public static let bitbucketCloud = ProviderCapabilities(
        repositories: true,
        issues: true,
        pullRequests: true,
        ci: true,
        releases: false,
        tags: true,
        branches: true,
        commits: true,
        contributors: false,
        repositoryContents: true,
        contributionCalendar: false,
        trafficStats: false,
        discussions: false,
        rateLimitDiagnostics: true
    )

    public static let forgejoCompatible = ProviderCapabilities(
        repositories: true,
        issues: true,
        pullRequests: true,
        ci: false,
        releases: true,
        tags: true,
        branches: true,
        commits: true,
        contributors: true,
        repositoryContents: true,
        contributionCalendar: false,
        trafficStats: false,
        discussions: false,
        rateLimitDiagnostics: true
    )

    public static let customGit = ProviderCapabilities(
        repositories: true,
        issues: false,
        pullRequests: false,
        ci: false,
        releases: false,
        tags: false,
        branches: true,
        commits: true,
        contributors: false,
        repositoryContents: false,
        contributionCalendar: false,
        trafficStats: false,
        discussions: false,
        rateLimitDiagnostics: false
    )
}

public extension SourceControlProvider {
    var defaultCapabilities: ProviderCapabilities {
        switch self {
        case .github:
            .github
        case .gitlab:
            .gitlab
        case .bitbucketCloud:
            .bitbucketCloud
        case .forgejo, .gitea:
            .forgejoCompatible
        case .customGit:
            .customGit
        }
    }
}
