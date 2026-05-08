import RepoBarCore
import Testing

struct RepositoryServiceTests {
    @Test
    func `router exposes active provider capabilities`() async {
        let router = RepositoryServiceRouter()
        #expect(await router.provider == .github)
        #expect(await router.capabilities == .github)

        await router.setProvider(.customGit)
        #expect(await router.provider == .customGit)
        #expect(await router.capabilities == .customGit)
    }

    @Test
    func `router reports unsupported provider features`() async throws {
        let router = RepositoryServiceRouter(provider: .customGit)

        do {
            _ = try await router.recentIssues(owner: "owner", name: "repo")
            Issue.record("Expected unsupported feature error")
        } catch let error as UnsupportedProviderFeature {
            #expect(error.provider == .customGit)
            #expect(error.feature == "issues")
        }
    }

    @Test
    func `local only service does not expose API backed features`() async throws {
        let service = LocalOnlyRepositoryService()
        #expect(await service.provider == .customGit)
        #expect(await service.capabilities == .customGit)
        #expect(try await service.repositoryList(limit: nil).isEmpty)

        do {
            _ = try await service.recentPullRequests(owner: "owner", name: "repo")
            Issue.record("Expected unsupported feature error")
        } catch let error as UnsupportedProviderFeature {
            #expect(error.provider == .customGit)
            #expect(error.feature == "pull requests")
        }
    }
}
