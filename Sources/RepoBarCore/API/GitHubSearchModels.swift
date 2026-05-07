import Foundation

struct SearchResponse: Decodable {
    let items: [RepoItem]
}

struct RepoItem: Decodable {
    let id: Int
    let name: String
    let fullName: String
    let fork: Bool
    let archived: Bool
    let hasDiscussions: Bool?
    let openIssuesCount: Int
    let stargazersCount: Int
    let forksCount: Int
    let pushedAt: Date?
    let permissions: RepoItemPermissions?
    let owner: Owner

    struct Owner: Decodable { let login: String }

    enum CodingKeys: String, CodingKey {
        case id, name
        case fullName = "full_name"
        case fork
        case archived
        case hasDiscussions = "has_discussions"
        case openIssuesCount = "open_issues_count"
        case stargazersCount = "stargazers_count"
        case forksCount = "forks_count"
        case pushedAt = "pushed_at"
        case permissions
        case owner
    }
}

struct RepoItemPermissions: Decodable {
    let admin: Bool?
    let maintain: Bool?
    let push: Bool?
    let pull: Bool?

    var hasReadAccess: Bool {
        self.admin == true || self.maintain == true || self.push == true || self.pull == true
    }
}

struct SearchIssuesResponse: Decodable {
    let totalCount: Int

    enum CodingKeys: String, CodingKey {
        case totalCount = "total_count"
    }
}

struct PullRequestListItem: Decodable {
    let id: Int
}
