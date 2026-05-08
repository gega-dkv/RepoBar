import RepoBarCore
import Testing

struct SourceControlProviderTests {
    @Test
    func `providers have stable raw values`() {
        #expect(SourceControlProvider.allCases.map(\.rawValue) == [
            "github",
            "gitlab",
            "bitbucketCloud",
            "forgejo",
            "gitea",
            "customGit"
        ])
    }

    @Test
    func `providers expose display labels`() {
        #expect(SourceControlProvider.github.label == "GitHub")
        #expect(SourceControlProvider.gitlab.label == "GitLab")
        #expect(SourceControlProvider.bitbucketCloud.label == "Bitbucket Cloud")
        #expect(SourceControlProvider.forgejo.label == "Forgejo")
        #expect(SourceControlProvider.gitea.label == "Gitea")
        #expect(SourceControlProvider.customGit.label == "Custom Git")
    }

    @Test
    func `providers expose default capabilities`() {
        #expect(SourceControlProvider.github.defaultCapabilities.trafficStats)
        #expect(SourceControlProvider.gitlab.defaultCapabilities.pullRequests)
        #expect(!SourceControlProvider.gitlab.defaultCapabilities.trafficStats)
        #expect(SourceControlProvider.bitbucketCloud.defaultCapabilities.repositoryContents)
        #expect(!SourceControlProvider.customGit.defaultCapabilities.issues)
    }

    @Test
    func `repository host defaults are stable`() {
        #expect(RepositoryHost.githubCom.provider == .github)
        #expect(RepositoryHost.githubCom.webBaseURL.absoluteString == "https://github.com")
        #expect(RepositoryHost.githubCom.apiBaseURL?.absoluteString == "https://api.github.com")
        #expect(RepositoryHost.gitlabCom.apiBaseURL?.absoluteString == "https://gitlab.com/api/v4")
        #expect(RepositoryHost.bitbucketCloud.authMethod == .apiToken)
        #expect(RepositoryHost.codeberg.provider == .forgejo)
    }
}
