import Commander
import Foundation
import RepoBarCore

struct AuthContext {
    let client: RepositoryServiceRouter
    let settings: UserSettings
    let host: URL
}

func makeAuthenticatedClient() async throws -> AuthContext {
    let settings = SettingsStore().load()
    let host = settings.enterpriseHost ?? settings.githubHost
    guard CredentialLoader.hasCredential(settings: settings, host: host) else {
        throw CLIError.notAuthenticated
    }

    let apiHost: URL = if let enterprise = settings.enterpriseHost {
        enterprise.appending(path: "/api/v3")
    } else {
        RepoBarAuthDefaults.apiHost
    }

    let client = RepositoryServiceRouter(provider: settings.selectedProvider)
    await client.setAPIHost(apiHost)
    let authMethod = settings.authMethod
    await client.setTokenProvider { @Sendable () async throws -> OAuthTokens? in
        if authMethod == .pat, let pat = try TokenStore.shared.loadPAT(provider: .github, host: host) {
            return OAuthTokens(accessToken: pat, refreshToken: "", expiresAt: nil)
        }
        return try await OAuthTokenRefresher().refreshIfNeeded(host: host)
    }
    return AuthContext(client: client, settings: settings, host: host)
}

enum CredentialLoader {
    static func hasCredential(settings: UserSettings, host: URL) -> Bool {
        switch settings.authMethod {
        case .pat:
            (try? TokenStore.shared.loadPAT(provider: .github, host: host)) != nil
        case .oauth:
            (try? TokenStore.shared.load(provider: .github, host: host)) != nil
        case .apiToken:
            false
        }
    }
}

func makeRepoURL(baseHost: URL, owner: String, name: String) -> URL {
    baseHost.appending(path: "/\(owner)/\(name)")
}

func requireRepoName(_ name: String?) throws -> String {
    guard let name, name.isEmpty == false else {
        throw ValidationError("Missing repository name (owner/name)")
    }

    return name
}

func parseRepoName(_ value: String) throws -> (owner: String, name: String) {
    let parts = value.split(separator: "/", maxSplits: 1).map(String.init)
    guard parts.count == 2, parts[0].isEmpty == false, parts[1].isEmpty == false else {
        throw ValidationError("Repository must be in owner/name format")
    }

    return (parts[0], parts[1])
}
