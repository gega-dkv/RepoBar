import Foundation

public enum ProviderTokenAuthError: Error, LocalizedError, Equatable {
    case unsupportedProvider(SourceControlProvider)
    case invalidToken
    case forbidden(String)
    case invalidResponse
    case network(String)

    public var errorDescription: String? {
        switch self {
        case let .unsupportedProvider(provider):
            "\(provider.label) token login is not implemented yet."
        case .invalidToken:
            "Invalid token."
        case let .forbidden(message):
            message
        case .invalidResponse:
            "Invalid response from server."
        case let .network(message):
            message
        }
    }
}

public struct ProviderTokenAuthenticator: Sendable {
    private let tokenStore: TokenStore
    private let session: URLSession

    public init(
        tokenStore: TokenStore = .shared,
        session: URLSession = .shared
    ) {
        self.tokenStore = tokenStore
        self.session = session
    }

    public func authenticatePAT(
        provider: SourceControlProvider,
        token: String,
        host: URL
    ) async throws -> UserIdentity {
        switch provider {
        case .gitlab:
            return try await self.authenticateGitLabPAT(token: token, host: host)
        case .github:
            throw ProviderTokenAuthError.unsupportedProvider(provider)
        case .bitbucketCloud, .forgejo, .gitea, .customGit:
            throw ProviderTokenAuthError.unsupportedProvider(provider)
        }
    }

    public func authenticateBitbucketAPIToken(
        email: String,
        token: String,
        host: URL = RepositoryHost.bitbucketCloud.webBaseURL
    ) async throws -> UserIdentity {
        let credential = ProviderCredential(
            provider: .bitbucketCloud,
            host: host,
            kind: .apiToken,
            headerStyle: .basic,
            token: token,
            username: email
        )
        let userURL = URL(string: "https://api.bitbucket.org/2.0/user")!
        var request = URLRequest(url: userURL)
        credential.headerStyle.apply(to: &request, credential: credential)

        let data = try await self.validatedData(for: request, provider: .bitbucketCloud)
        let decoded = try Self.decodeBitbucketUser(from: data)
        try self.tokenStore.save(credential: credential)
        return UserIdentity(username: decoded, host: host)
    }

    private func authenticateGitLabPAT(token: String, host: URL) async throws -> UserIdentity {
        let credential = ProviderCredential(
            provider: .gitlab,
            host: host,
            kind: .pat,
            headerStyle: .privateToken,
            token: token
        )
        let apiHost = Self.gitLabAPIHost(for: host)
        let userURL = apiHost.appending(path: "user")
        var request = URLRequest(url: userURL)
        credential.headerStyle.apply(to: &request, credential: credential)

        let data = try await self.validatedData(for: request, provider: .gitlab)
        let decoded = try Self.decodeGitLabUser(from: data)
        try self.tokenStore.save(credential: credential)
        return UserIdentity(username: decoded.username, host: host)
    }

    private func validatedData(for request: URLRequest, provider: SourceControlProvider) async throws -> Data {
        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await self.session.data(for: request)
        } catch {
            throw ProviderTokenAuthError.network(error.localizedDescription)
        }

        guard let http = response as? HTTPURLResponse else {
            throw ProviderTokenAuthError.invalidResponse
        }

        switch http.statusCode {
        case 200:
            return data
        case 401:
            throw ProviderTokenAuthError.invalidToken
        case 403:
            throw ProviderTokenAuthError.forbidden(Self.forbiddenMessage(provider: provider))
        default:
            throw ProviderTokenAuthError.invalidResponse
        }
    }

    private static func gitLabAPIHost(for host: URL) -> URL {
        if host.path.hasPrefix("/api/v4") {
            return host
        }
        return host.appending(path: "api/v4")
    }

    private static func forbiddenMessage(provider: SourceControlProvider) -> String {
        switch provider {
        case .gitlab:
            "Access forbidden. Token may lack required scopes (read_user, read_api, read_repository)."
        case .bitbucketCloud:
            "Access forbidden. Check the Atlassian email and API token permissions."
        case .github, .forgejo, .gitea, .customGit:
            "Access forbidden."
        }
    }

    private static func decodeGitLabUser(from data: Data) throws -> GitLabUserResponse {
        do {
            return try JSONDecoder().decode(GitLabUserResponse.self, from: data)
        } catch {
            throw ProviderTokenAuthError.invalidResponse
        }
    }

    private static func decodeBitbucketUser(from data: Data) throws -> String {
        do {
            let decoded = try JSONDecoder().decode(BitbucketUserResponse.self, from: data)
            if let username = decoded.username, !username.isEmpty { return username }
            if let nickname = decoded.nickname, !nickname.isEmpty { return nickname }
            if let displayName = decoded.displayName, !displayName.isEmpty { return displayName }
            if let accountID = decoded.accountID, !accountID.isEmpty { return accountID }
            if let uuid = decoded.uuid, !uuid.isEmpty { return uuid }
            throw ProviderTokenAuthError.invalidResponse
        } catch let error as ProviderTokenAuthError {
            throw error
        } catch {
            throw ProviderTokenAuthError.invalidResponse
        }
    }
}

private struct GitLabUserResponse: Decodable {
    let username: String
}

private struct BitbucketUserResponse: Decodable {
    let username: String?
    let nickname: String?
    let displayName: String?
    let accountID: String?
    let uuid: String?

    enum CodingKeys: String, CodingKey {
        case username
        case nickname
        case displayName = "display_name"
        case accountID = "account_id"
        case uuid
    }
}
