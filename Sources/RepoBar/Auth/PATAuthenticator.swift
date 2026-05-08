import Foundation
import OSLog
import RepoBarCore

public enum PATAuthError: Error, LocalizedError {
    case invalidToken
    case forbidden(String)
    case networkError(Error)
    case invalidResponse

    public var errorDescription: String? {
        switch self {
        case .invalidToken:
            "Invalid token"
        case let .forbidden(message):
            message
        case let .networkError(error):
            error.localizedDescription
        case .invalidResponse:
            "Invalid response from server"
        }
    }
}

/// Handles Personal Access Token authentication as an alternative to OAuth.
/// PATs can be authorized for SAML SSO organizations in GitHub settings.
@MainActor
public final class PATAuthenticator {
    private let tokenStore: TokenStore
    private let signposter = OSSignposter(subsystem: "com.steipete.repobar", category: "pat-auth")
    private var cachedPAT: String?
    private var cachedHost: URL?
    private var hasLoadedPAT = false
    private let session: URLSession

    public init(
        tokenStore: TokenStore = .shared,
        session: URLSession = .shared
    ) {
        self.tokenStore = tokenStore
        self.session = session
    }

    /// Validates PAT via GET /user, stores on success, returns UserIdentity.
    public func authenticate(pat: String, host: URL) async throws -> UserIdentity {
        let signpost = self.signposter.beginInterval("authenticate")
        defer { self.signposter.endInterval("authenticate", signpost) }

        let apiHost = Self.apiHost(for: host)
        let userURL = apiHost.appendingPathComponent("user")

        var request = URLRequest(url: userURL)
        request.setValue("Bearer \(pat)", forHTTPHeaderField: "Authorization")
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")

        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await self.session.data(for: request)
        } catch {
            throw PATAuthError.networkError(error)
        }

        guard let httpResponse = response as? HTTPURLResponse else {
            throw PATAuthError.invalidResponse
        }

        switch httpResponse.statusCode {
        case 200:
            break
        case 401:
            throw PATAuthError.invalidToken
        case 403:
            throw PATAuthError.forbidden("Access forbidden. Token may lack required scopes (repo, read:org)")
        default:
            throw PATAuthError.invalidResponse
        }

        struct UserResponse: Decodable {
            let login: String
        }

        let user: UserResponse
        do {
            user = try JSONDecoder().decode(UserResponse.self, from: data)
        } catch {
            throw PATAuthError.invalidResponse
        }

        try self.tokenStore.savePAT(pat, provider: .github, host: host)
        self.cachedPAT = pat
        self.cachedHost = host
        self.hasLoadedPAT = true
        await DiagnosticsLogger.shared.message("PAT login succeeded; token stored.")

        return UserIdentity(username: user.login, host: host)
    }

    /// Loads the stored PAT from Keychain.
    public func loadPAT(host: URL? = nil) -> String? {
        if self.hasLoadedPAT, host == nil || self.cachedHost == host { return self.cachedPAT }
        self.hasLoadedPAT = true
        let pat = if let host {
            try? self.tokenStore.loadPAT(provider: .github, host: host)
        } else {
            try? self.tokenStore.loadPAT()
        }
        self.cachedPAT = pat
        self.cachedHost = host
        return pat
    }

    /// Clears the stored PAT.
    public func logout(host: URL? = nil) async {
        if let host {
            if ProviderCredential.normalizedHost(host.host ?? host.absoluteString) == "github.com" {
                self.tokenStore.clearPAT()
            } else {
                self.tokenStore.clearCredential(provider: .github, host: host, kind: .pat)
            }
        } else {
            self.tokenStore.clearPAT()
        }
        self.cachedPAT = nil
        self.cachedHost = nil
        self.hasLoadedPAT = false
        await DiagnosticsLogger.shared.message("PAT cleared.")
    }

    /// Converts a GitHub host URL to its API endpoint.
    private static func apiHost(for host: URL) -> URL {
        let hostString = host.host ?? "github.com"
        if hostString == "github.com" {
            return URL(string: "https://api.github.com")!
        }
        // Enterprise: use /api/v3 path
        return host.appendingPathComponent("api/v3")
    }
}
