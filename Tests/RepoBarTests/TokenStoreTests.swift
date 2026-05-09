import Foundation
@testable import RepoBarCore
import Testing

struct TokenStoreTests {
    @Test
    func `debug default storage does not use keychain`() throws {
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, accessGroup: "com.steipete.repobar.shared")
        defer { store.clear() }

        let tokens = OAuthTokens(
            accessToken: "token-\(UUID().uuidString)",
            refreshToken: "refresh-\(UUID().uuidString)",
            expiresAt: Date().addingTimeInterval(3600)
        )

        try store.save(tokens: tokens)
        let loaded = try store.load()
        #expect(loaded == tokens)
    }

    @Test
    func `file storage does not use keychain`() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, storage: .file(directory))
        let tokens = OAuthTokens(
            accessToken: "debug-token",
            refreshToken: "debug-refresh",
            expiresAt: Date().addingTimeInterval(60)
        )

        try store.save(tokens: tokens)
        #expect(try store.load() == tokens)

        store.clear()
        #expect(try store.load() == nil)
    }

    @Test
    func `provider credentials are isolated by provider host and kind`() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, storage: .file(directory))
        let github = try #require(URL(string: "https://github.com"))
        let gitlab = try #require(URL(string: "https://gitlab.com"))

        try store.savePAT("ghp_token", provider: .github, host: github)
        try store.savePAT("glpat-token", provider: .gitlab, host: gitlab)
        try store.saveAPIToken("bb-token", username: "me@example.com", provider: .bitbucketCloud, host: #require(URL(string: "https://bitbucket.org")))

        let githubCredential = try store.loadCredential(provider: .github, host: github, kind: .pat)
        let gitlabCredential = try store.loadCredential(provider: .gitlab, host: gitlab, kind: .pat)
        let bitbucketCredential = try store.loadCredential(
            provider: .bitbucketCloud,
            host: #require(URL(string: "https://bitbucket.org")),
            kind: .apiToken
        )

        #expect(githubCredential?.token == "ghp_token")
        #expect(githubCredential?.headerStyle == .authorizationBearer)
        #expect(gitlabCredential?.token == "glpat-token")
        #expect(gitlabCredential?.headerStyle == .privateToken)
        #expect(bitbucketCredential?.token == "bb-token")
        #expect(bitbucketCredential?.username == "me@example.com")
        #expect(bitbucketCredential?.headerStyle == .basic)
    }

    @Test
    func `github enterprise credentials do not overwrite github dot com legacy credentials`() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, storage: .file(directory))
        let github = try #require(URL(string: "https://github.com"))
        let enterprise = try #require(URL(string: "https://ghe.example.com"))

        try store.savePAT("github-token", provider: .github, host: github)
        try store.savePAT("enterprise-token", provider: .github, host: enterprise)

        #expect(try store.loadPAT() == "github-token")
        #expect(try store.loadPAT(provider: .github, host: github) == "github-token")
        #expect(try store.loadPAT(provider: .github, host: enterprise) == "enterprise-token")
    }

    @Test
    func `oauth tokens can be loaded by github enterprise host`() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, storage: .file(directory))
        let enterprise = try #require(URL(string: "https://ghe.example.com"))
        let tokens = OAuthTokens(accessToken: "enterprise-oauth", refreshToken: "refresh", expiresAt: Date().addingTimeInterval(60))

        try store.save(tokens: tokens, provider: .github, host: enterprise)

        #expect(try store.load(provider: .github, host: enterprise) == tokens)
        #expect(try store.load() == nil)
    }

    @Test
    func `credential account keys are stable`() throws {
        let github = try #require(URL(string: "https://github.com"))
        let gitlab = try #require(URL(string: "https://gitlab.com"))

        #expect(TokenStore.credentialAccount(provider: .github, host: github, kind: .oauth) == "github:github.com:oauth")
        #expect(TokenStore.credentialAccount(provider: .github, host: github, kind: .pat) == "github:github.com:pat")
        #expect(TokenStore.credentialAccount(provider: .gitlab, host: gitlab, kind: .pat) == "gitlab:gitlab.com:pat")
        #expect(
            try TokenStore.credentialAccount(
                provider: .bitbucketCloud,
                host: #require(URL(string: "https://bitbucket.org")),
                kind: .apiToken
            ) == "bitbucketCloud:bitbucket.org:apiToken"
        )
    }

    @Test
    func `header styles apply expected headers`() throws {
        var bearerRequest = try URLRequest(url: #require(URL(string: "https://example.com")))
        CredentialHeaderStyle.authorizationBearer.apply(
            to: &bearerRequest,
            credential: ProviderCredential(
                provider: .github,
                host: "github.com",
                kind: .pat,
                headerStyle: .authorizationBearer,
                token: "token"
            )
        )
        #expect(bearerRequest.value(forHTTPHeaderField: "Authorization") == "Bearer token")

        var privateTokenRequest = try URLRequest(url: #require(URL(string: "https://example.com")))
        CredentialHeaderStyle.privateToken.apply(
            to: &privateTokenRequest,
            credential: ProviderCredential(
                provider: .gitlab,
                host: "gitlab.com",
                kind: .pat,
                headerStyle: .privateToken,
                token: "glpat"
            )
        )
        #expect(privateTokenRequest.value(forHTTPHeaderField: "PRIVATE-TOKEN") == "glpat")

        var tokenRequest = try URLRequest(url: #require(URL(string: "https://example.com")))
        CredentialHeaderStyle.authorizationToken.apply(
            to: &tokenRequest,
            credential: ProviderCredential(
                provider: .forgejo,
                host: "codeberg.org",
                kind: .pat,
                headerStyle: .authorizationToken,
                token: "forgejo-token"
            )
        )
        #expect(tokenRequest.value(forHTTPHeaderField: "Authorization") == "token forgejo-token")

        var basicRequest = try URLRequest(url: #require(URL(string: "https://example.com")))
        CredentialHeaderStyle.basic.apply(
            to: &basicRequest,
            credential: ProviderCredential(
                provider: .bitbucketCloud,
                host: "bitbucket.org",
                kind: .apiToken,
                headerStyle: .basic,
                token: "bb-token",
                username: "me@example.com"
            )
        )
        #expect(basicRequest.value(forHTTPHeaderField: "Authorization") == "Basic bWVAZXhhbXBsZS5jb206YmItdG9rZW4=")
    }

    @Test
    func `legacy oauth tokens migrate to provider credential`() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = "com.steipete.repobar.auth.tests.\(UUID().uuidString)"
        let store = TokenStore(service: service, storage: .file(directory))
        let legacy = OAuthTokens(accessToken: "legacy", refreshToken: "refresh", expiresAt: Date().addingTimeInterval(60))

        let legacyData = try JSONEncoder().encode(legacy)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let legacyURL = directory.appendingPathComponent("\(service)-default.json")
        try legacyData.write(to: legacyURL)

        let loaded = try store.load()
        let credential = try store.loadCredential(
            provider: .github,
            host: #require(URL(string: "https://github.com")),
            kind: .oauth
        )

        #expect(loaded == legacy)
        #expect(credential?.token == "legacy")
        #expect(credential?.refreshToken == "refresh")
    }
}
