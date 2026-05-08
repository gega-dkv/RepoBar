import Foundation

public struct RepositoryURLTemplates: Equatable, Codable, Sendable {
    public var repository: String?
    public var branch: String?
    public var commit: String?
    public var issue: String?
    public var pullRequest: String?

    public init(
        repository: String? = nil,
        branch: String? = nil,
        commit: String? = nil,
        issue: String? = nil,
        pullRequest: String? = nil
    ) {
        self.repository = repository
        self.branch = branch
        self.commit = commit
        self.issue = issue
        self.pullRequest = pullRequest
    }
}

public struct RepositoryHost: Identifiable, Equatable, Codable, Sendable {
    public var id: String
    public var provider: SourceControlProvider
    public var displayName: String
    public var webBaseURL: URL
    public var apiBaseURL: URL?
    public var authMethod: AuthMethod
    public var urlTemplates: RepositoryURLTemplates

    public init(
        id: String? = nil,
        provider: SourceControlProvider,
        displayName: String,
        webBaseURL: URL,
        apiBaseURL: URL?,
        authMethod: AuthMethod,
        urlTemplates: RepositoryURLTemplates = RepositoryURLTemplates()
    ) {
        self.provider = provider
        self.webBaseURL = webBaseURL
        self.id = id ?? Self.defaultID(provider: provider, webBaseURL: webBaseURL)
        self.displayName = displayName
        self.apiBaseURL = apiBaseURL
        self.authMethod = authMethod
        self.urlTemplates = urlTemplates
    }

    public static let githubCom = RepositoryHost(
        id: "github:github.com",
        provider: .github,
        displayName: "GitHub.com",
        webBaseURL: URL(string: "https://github.com")!,
        apiBaseURL: URL(string: "https://api.github.com")!,
        authMethod: .oauth
    )

    public static let gitlabCom = RepositoryHost(
        id: "gitlab:gitlab.com",
        provider: .gitlab,
        displayName: "GitLab.com",
        webBaseURL: URL(string: "https://gitlab.com")!,
        apiBaseURL: URL(string: "https://gitlab.com/api/v4")!,
        authMethod: .pat
    )

    public static let bitbucketCloud = RepositoryHost(
        id: "bitbucketCloud:bitbucket.org",
        provider: .bitbucketCloud,
        displayName: "Bitbucket Cloud",
        webBaseURL: URL(string: "https://bitbucket.org")!,
        apiBaseURL: URL(string: "https://api.bitbucket.org/2.0")!,
        authMethod: .apiToken
    )

    public static let codeberg = RepositoryHost(
        id: "forgejo:codeberg.org",
        provider: .forgejo,
        displayName: "Codeberg",
        webBaseURL: URL(string: "https://codeberg.org")!,
        apiBaseURL: URL(string: "https://codeberg.org/api/v1")!,
        authMethod: .pat
    )

    public static func defaultID(provider: SourceControlProvider, webBaseURL: URL) -> String {
        let host = webBaseURL.host?.lowercased() ?? webBaseURL.absoluteString.lowercased()
        return "\(provider.rawValue):\(host)"
    }
}

public struct RepositoryAccount: Equatable, Codable, Sendable {
    public var provider: SourceControlProvider
    public var webHost: URL
    public var apiHost: URL?
    public var username: String?

    public init(provider: SourceControlProvider, webHost: URL, apiHost: URL?, username: String? = nil) {
        self.provider = provider
        self.webHost = webHost
        self.apiHost = apiHost
        self.username = username
    }
}
