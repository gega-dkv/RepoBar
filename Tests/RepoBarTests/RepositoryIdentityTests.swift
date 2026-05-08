import Foundation
import RepoBarCore
import Testing

struct RepositoryIdentityTests {
    @Test
    func `repository defaults to github identity`() {
        let repo = Repository(
            id: "1",
            name: "RepoBar",
            owner: "steipete",
            sortOrder: nil,
            error: nil,
            rateLimitedUntil: nil,
            ciStatus: .unknown,
            openIssues: 0,
            openPulls: 0,
            latestRelease: nil,
            latestActivity: nil,
            traffic: nil,
            heatmap: []
        )

        #expect(repo.provider == .github)
        #expect(repo.namespacePath == "steipete")
        #expect(repo.pathWithNamespace == "steipete/RepoBar")
        #expect(repo.fullName == "steipete/RepoBar")
        #expect(repo.slug == "RepoBar")
    }

    @Test
    func `repository can carry provider neutral identity`() {
        let identity = RepositoryIdentity(
            provider: .gitlab,
            id: "42",
            name: "project",
            namespacePath: "group/subgroup",
            webURL: URL(string: "https://gitlab.com/group/subgroup/project"),
            apiURL: URL(string: "https://gitlab.com/api/v4/projects/42"),
            providerSpecificID: "42"
        )

        let repo = Repository(
            id: identity.id,
            name: identity.name,
            owner: "group",
            identity: identity,
            sortOrder: nil,
            error: nil,
            rateLimitedUntil: nil,
            ciStatus: .unknown,
            openIssues: 0,
            openPulls: 0,
            latestRelease: nil,
            latestActivity: nil,
            traffic: nil,
            heatmap: []
        )

        #expect(repo.provider == .gitlab)
        #expect(repo.namespacePath == "group/subgroup")
        #expect(repo.pathWithNamespace == "group/subgroup/project")
        #expect(repo.fullName == "group/subgroup/project")
        #expect(repo.webURL == URL(string: "https://gitlab.com/group/subgroup/project")!)
        #expect(repo.apiURL == URL(string: "https://gitlab.com/api/v4/projects/42")!)
    }
}
