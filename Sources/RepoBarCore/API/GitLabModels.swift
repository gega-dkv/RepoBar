import Foundation

struct GitLabUser: Decodable {
    let id: Int
    let username: String
    let name: String?
    let avatarURL: URL?
    let webURL: URL?

    enum CodingKeys: String, CodingKey {
        case id
        case username
        case name
        case avatarURL = "avatar_url"
        case webURL = "web_url"
    }
}

struct GitLabProject: Decodable {
    let id: Int
    let name: String
    let path: String
    let pathWithNamespace: String
    let webURL: URL?
    let archived: Bool
    let starCount: Int
    let forksCount: Int
    let openIssuesCount: Int?
    let lastActivityAt: Date?
    let defaultBranch: String?
    let namespace: Namespace?
    let issuesEnabled: Bool?
    let mergeRequestsEnabled: Bool?
    let jobsEnabled: Bool?
    let emptyRepo: Bool?

    enum CodingKeys: String, CodingKey {
        case id
        case name
        case path
        case pathWithNamespace = "path_with_namespace"
        case webURL = "web_url"
        case archived
        case starCount = "star_count"
        case forksCount = "forks_count"
        case openIssuesCount = "open_issues_count"
        case lastActivityAt = "last_activity_at"
        case defaultBranch = "default_branch"
        case namespace
        case issuesEnabled = "issues_enabled"
        case mergeRequestsEnabled = "merge_requests_enabled"
        case jobsEnabled = "jobs_enabled"
        case emptyRepo = "empty_repo"
    }

    struct Namespace: Decodable {
        let fullPath: String?

        enum CodingKeys: String, CodingKey {
            case fullPath = "full_path"
        }
    }
}

struct GitLabIssue: Decodable {
    let iid: Int
    let title: String
    let webURL: URL
    let updatedAt: Date
    let createdAt: Date?
    let author: Author?
    let assignees: [Author]?
    let userNotesCount: Int?
    let labels: [String]?

    enum CodingKeys: String, CodingKey {
        case iid
        case title
        case webURL = "web_url"
        case updatedAt = "updated_at"
        case createdAt = "created_at"
        case author
        case assignees
        case userNotesCount = "user_notes_count"
        case labels
    }
}

struct GitLabMergeRequest: Decodable {
    let iid: Int
    let title: String
    let webURL: URL
    let updatedAt: Date
    let createdAt: Date?
    let author: GitLabIssue.Author?
    let userNotesCount: Int?
    let labels: [String]?
    let sourceBranch: String?
    let targetBranch: String?
    let draft: Bool?
    let workInProgress: Bool?

    enum CodingKeys: String, CodingKey {
        case iid
        case title
        case webURL = "web_url"
        case updatedAt = "updated_at"
        case createdAt = "created_at"
        case author
        case userNotesCount = "user_notes_count"
        case labels
        case sourceBranch = "source_branch"
        case targetBranch = "target_branch"
        case draft
        case workInProgress = "work_in_progress"
    }
}

extension GitLabIssue {
    struct Author: Decodable {
        let username: String?
        let avatarURL: URL?
        let webURL: URL?

        enum CodingKeys: String, CodingKey {
            case username
            case avatarURL = "avatar_url"
            case webURL = "web_url"
        }
    }
}

struct GitLabRelease: Decodable {
    let name: String?
    let tagName: String
    let releasedAt: Date?
    let createdAt: Date?
    let upcomingRelease: Bool?
    let author: GitLabIssue.Author?
    let assets: Assets?

    enum CodingKeys: String, CodingKey {
        case name
        case tagName = "tag_name"
        case releasedAt = "released_at"
        case createdAt = "created_at"
        case upcomingRelease = "upcoming_release"
        case author
        case assets
    }

    struct Assets: Decodable {
        let count: Int?
        let sources: [Asset]?
        let links: [Asset]?
    }

    struct Asset: Decodable {
        let name: String?
        let url: URL?
        let directAssetURL: URL?

        enum CodingKeys: String, CodingKey {
            case name
            case url
            case directAssetURL = "direct_asset_url"
        }
    }
}

struct GitLabBranch: Decodable {
    let name: String
    let protected: Bool?
    let commit: CommitRef?
}

struct GitLabTag: Decodable {
    let name: String
    let commit: CommitRef?
}

struct CommitRef: Decodable {
    let id: String?
    let shortID: String?

    enum CodingKeys: String, CodingKey {
        case id
        case shortID = "short_id"
    }
}

struct GitLabCommit: Decodable {
    let id: String
    let shortID: String?
    let title: String?
    let message: String?
    let webURL: URL?
    let authoredDate: Date?
    let committedDate: Date?
    let authorName: String?
    let authorEmail: String?

    enum CodingKeys: String, CodingKey {
        case id
        case shortID = "short_id"
        case title
        case message
        case webURL = "web_url"
        case authoredDate = "authored_date"
        case committedDate = "committed_date"
        case authorName = "author_name"
        case authorEmail = "author_email"
    }
}

struct GitLabTreeItem: Decodable {
    let id: String?
    let name: String
    let path: String
    let type: String
    let webURL: URL?

    enum CodingKeys: String, CodingKey {
        case id
        case name
        case path
        case type
        case webURL = "web_url"
    }
}

struct GitLabContributor: Decodable {
    let name: String
    let email: String?
    let commits: Int
}

struct GitLabPipeline: Decodable {
    let id: Int?
    let iid: Int?
    let status: String
    let ref: String?
    let webURL: URL?
    let updatedAt: Date?
    let createdAt: Date?

    enum CodingKeys: String, CodingKey {
        case id
        case iid
        case status
        case ref
        case webURL = "web_url"
        case updatedAt = "updated_at"
        case createdAt = "created_at"
    }
}

struct GitLabEvent: Decodable {
    let actionName: String?
    let targetType: String?
    let targetTitle: String?
    let authorUsername: String?
    let author: GitLabIssue.Author?
    let createdAt: Date
    let targetURL: URL?
    let pushData: PushData?

    enum CodingKeys: String, CodingKey {
        case actionName = "action_name"
        case targetType = "target_type"
        case targetTitle = "target_title"
        case authorUsername = "author_username"
        case author
        case createdAt = "created_at"
        case targetURL = "target_url"
        case pushData = "push_data"
    }

    struct PushData: Decodable {
        let commitTitle: String?
        let ref: String?
        let commitTo: String?

        enum CodingKeys: String, CodingKey {
            case commitTitle = "commit_title"
            case ref
            case commitTo = "commit_to"
        }
    }
}
