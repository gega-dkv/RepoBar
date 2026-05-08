import Foundation

public enum SourceControlProvider: String, CaseIterable, Codable, Hashable, Sendable, Identifiable {
    case github
    case gitlab
    case bitbucketCloud
    case forgejo
    case gitea
    case customGit

    public var id: String {
        self.rawValue
    }

    public var label: String {
        switch self {
        case .github: "GitHub"
        case .gitlab: "GitLab"
        case .bitbucketCloud: "Bitbucket Cloud"
        case .forgejo: "Forgejo"
        case .gitea: "Gitea"
        case .customGit: "Custom Git"
        }
    }
}
