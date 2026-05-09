import Foundation

struct BitbucketPage<T: Decodable>: Decodable {
    let size: Int?
    let page: Int?
    let pagelen: Int?
    let next: URL?
    let values: [T]
}

struct BitbucketUser: Decodable {
    let uuid: String?
    let username: String?
    let nickname: String?
    let displayName: String?

    enum CodingKeys: String, CodingKey {
        case uuid
        case username
        case nickname
        case displayName = "display_name"
    }
}

struct BitbucketRepositoryPermission: Decodable {
    let repository: BitbucketRepository
    let permission: String?
}

struct BitbucketRepository: Decodable {
    let uuid: String
    let fullName: String
    let name: String
    let owner: Owner?
    let project: Project?
    let scm: String?
    let updatedOn: Date?
    let createdOn: Date?
    let hasIssues: Bool?
    let isPrivate: Bool?
    let links: Links?
    let mainbranch: BranchRef?

    enum CodingKeys: String, CodingKey {
        case uuid
        case fullName = "full_name"
        case name
        case owner
        case project
        case scm
        case updatedOn = "updated_on"
        case createdOn = "created_on"
        case hasIssues = "has_issues"
        case isPrivate = "is_private"
        case links
        case mainbranch
    }

    struct Owner: Decodable {
        let username: String?
        let nickname: String?
        let displayName: String?
        let uuid: String?

        enum CodingKeys: String, CodingKey {
            case username
            case nickname
            case displayName = "display_name"
            case uuid
        }
    }

    struct Project: Decodable {
        let key: String?
        let name: String?
        let uuid: String?
    }

    struct BranchRef: Decodable {
        let name: String?
    }

    struct Links: Decodable {
        let html: Link?
        let selfLink: Link?
        let avatar: Link?
        let pullrequests: Link?
        let commits: Link?
        let branches: Link?
        let tags: Link?
        let clone: [Link]?

        enum CodingKeys: String, CodingKey {
            case html
            case selfLink = "self"
            case avatar
            case pullrequests
            case commits
            case branches
            case tags
            case clone
        }
    }

    struct Link: Decodable {
        let href: URL?
        let name: String?
    }
}

struct BitbucketPullRequest: Decodable {
    let id: Int
    let title: String
    let state: String?
    let updatedOn: Date?
    let createdOn: Date?
    let author: BitbucketUser?
    let commentCount: Int?
    let taskCount: Int?
    let links: Links?
    let source: Ref?
    let destination: Ref?

    enum CodingKeys: String, CodingKey {
        case id
        case title
        case state
        case updatedOn = "updated_on"
        case createdOn = "created_on"
        case author
        case commentCount = "comment_count"
        case taskCount = "task_count"
        case links
        case source
        case destination
    }

    struct Links: Decodable {
        let html: BitbucketRepository.Link?
    }

    struct Ref: Decodable {
        let branch: Branch?
    }

    struct Branch: Decodable {
        let name: String?
    }
}

struct BitbucketIssue: Decodable {
    let id: Int
    let title: String
    let state: String?
    let updatedOn: Date?
    let createdOn: Date?
    let reporter: BitbucketUser?
    let assignee: BitbucketUser?
    let commentCount: Int?
    let links: Links?

    enum CodingKeys: String, CodingKey {
        case id
        case title
        case state
        case updatedOn = "updated_on"
        case createdOn = "created_on"
        case reporter
        case assignee
        case commentCount = "comment_count"
        case links
    }

    struct Links: Decodable {
        let html: BitbucketRepository.Link?
    }
}

struct BitbucketCommit: Decodable {
    let hash: String
    let message: String?
    let date: Date?
    let author: Author?
    let links: Links?

    struct Author: Decodable {
        let raw: String?
        let user: BitbucketUser?
    }

    struct Links: Decodable {
        let html: BitbucketRepository.Link?
    }
}

struct BitbucketRef: Decodable {
    let name: String
    let target: Target?
    let links: Links?

    struct Target: Decodable {
        let hash: String?
    }

    struct Links: Decodable {
        let html: BitbucketRepository.Link?
    }
}

struct BitbucketSourceItem: Decodable {
    let path: String
    let type: String
    let size: Int?
    let links: Links?

    struct Links: Decodable {
        let selfLink: BitbucketRepository.Link?
        let meta: BitbucketRepository.Link?
        let html: BitbucketRepository.Link?

        enum CodingKeys: String, CodingKey {
            case selfLink = "self"
            case meta
            case html
        }
    }
}
