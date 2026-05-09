import Foundation
import Logging
import Security

public struct OAuthTokens: Codable, Equatable, Sendable {
    public let accessToken: String
    public let refreshToken: String
    public let expiresAt: Date?

    public init(accessToken: String, refreshToken: String, expiresAt: Date?) {
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        self.expiresAt = expiresAt
    }
}

public struct OAuthClientCredentials: Codable, Equatable, Sendable {
    public let clientID: String
    public let clientSecret: String

    public init(clientID: String, clientSecret: String) {
        self.clientID = clientID
        self.clientSecret = clientSecret
    }
}

public enum TokenCredentialKind: String, Codable, Equatable, Sendable {
    case oauth
    case pat
    case apiToken
    case none
}

public enum CredentialHeaderStyle: String, Codable, Equatable, Sendable {
    case authorizationBearer
    case authorizationToken
    case privateToken
    case basic
    case none

    public func apply(to request: inout URLRequest, credential: ProviderCredential) {
        switch self {
        case .authorizationBearer:
            request.setValue("Bearer \(credential.token)", forHTTPHeaderField: "Authorization")
        case .authorizationToken:
            request.setValue("token \(credential.token)", forHTTPHeaderField: "Authorization")
        case .privateToken:
            request.setValue(credential.token, forHTTPHeaderField: "PRIVATE-TOKEN")
        case .basic:
            guard let username = credential.username else { return }

            let raw = "\(username):\(credential.token)"
            let encoded = Data(raw.utf8).base64EncodedString()
            request.setValue("Basic \(encoded)", forHTTPHeaderField: "Authorization")
        case .none:
            break
        }
    }
}

public struct ProviderCredential: Codable, Equatable, Sendable {
    public var provider: SourceControlProvider
    public var host: String
    public var kind: TokenCredentialKind
    public var headerStyle: CredentialHeaderStyle
    public var token: String
    public var refreshToken: String?
    public var expiresAt: Date?
    public var username: String?

    public init(
        provider: SourceControlProvider,
        host: String,
        kind: TokenCredentialKind,
        headerStyle: CredentialHeaderStyle,
        token: String,
        refreshToken: String? = nil,
        expiresAt: Date? = nil,
        username: String? = nil
    ) {
        self.provider = provider
        self.host = Self.normalizedHost(host)
        self.kind = kind
        self.headerStyle = headerStyle
        self.token = token
        self.refreshToken = refreshToken
        self.expiresAt = expiresAt
        self.username = username
    }

    public init(
        provider: SourceControlProvider,
        host: URL,
        kind: TokenCredentialKind,
        headerStyle: CredentialHeaderStyle,
        token: String,
        refreshToken: String? = nil,
        expiresAt: Date? = nil,
        username: String? = nil
    ) {
        self.init(
            provider: provider,
            host: host.host ?? host.absoluteString,
            kind: kind,
            headerStyle: headerStyle,
            token: token,
            refreshToken: refreshToken,
            expiresAt: expiresAt,
            username: username
        )
    }

    public static func normalizedHost(_ host: String) -> String {
        host.trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }
}

public enum TokenStoreError: Error {
    case saveFailed
    case loadFailed
}

public enum TokenStoreStorage: Sendable {
    case keychain
    case file(URL)
}

public struct TokenStore: Sendable {
    public static var shared: TokenStore {
        TokenStore()
    }

    private let service: String
    private let accessGroup: String?
    private let storage: TokenStoreStorage
    private let logger = RepoBarLogging.logger("token-store")

    public init(
        service: String = "com.steipete.repobar.auth",
        accessGroup: String? = nil,
        storage: TokenStoreStorage? = nil
    ) {
        self.service = service
        self.accessGroup = accessGroup ?? Self.defaultAccessGroup()
        self.storage = storage ?? Self.defaultStorage()
    }

    public func save(tokens: OAuthTokens) throws {
        let data = try JSONEncoder().encode(tokens)
        try self.save(data: data, account: "default")
        try self.save(tokens: tokens, provider: .github, host: RepositoryHost.githubCom.webBaseURL)
    }

    public func save(tokens: OAuthTokens, provider: SourceControlProvider, host: URL) throws {
        try self.save(
            credential: ProviderCredential(
                provider: provider,
                host: host,
                kind: .oauth,
                headerStyle: Self.defaultHeaderStyle(provider: provider, kind: .oauth),
                token: tokens.accessToken,
                refreshToken: tokens.refreshToken,
                expiresAt: tokens.expiresAt
            )
        )
    }

    public func load() throws -> OAuthTokens? {
        if let tokens = try self.load(provider: .github, host: RepositoryHost.githubCom.webBaseURL) { return tokens }
        guard let data = try self.loadData(account: "default") else { return nil }

        let tokens = try JSONDecoder().decode(OAuthTokens.self, from: data)
        try? self.save(tokens: tokens)
        return tokens
    }

    public func load(provider: SourceControlProvider, host: URL) throws -> OAuthTokens? {
        guard let credential = try self.loadCredential(
            provider: provider,
            host: host,
            kind: .oauth
        ) else { return nil }

        return OAuthTokens(
            accessToken: credential.token,
            refreshToken: credential.refreshToken ?? "",
            expiresAt: credential.expiresAt
        )
    }

    public func save(clientCredentials: OAuthClientCredentials) throws {
        let data = try JSONEncoder().encode(clientCredentials)
        try self.save(data: data, account: "client")
        try self.save(clientCredentials: clientCredentials, provider: .github, host: RepositoryHost.githubCom.webBaseURL, kind: .oauth)
    }

    public func save(
        clientCredentials: OAuthClientCredentials,
        provider: SourceControlProvider,
        host: URL,
        kind: TokenCredentialKind
    ) throws {
        let data = try JSONEncoder().encode(clientCredentials)
        try self.save(
            data: data,
            account: Self.clientCredentialsAccount(
                provider: provider,
                host: host,
                kind: kind
            )
        )
    }

    public func loadClientCredentials() throws -> OAuthClientCredentials? {
        if let credentials = try self.loadClientCredentials(
            provider: .github,
            host: RepositoryHost.githubCom.webBaseURL,
            kind: .oauth
        ) {
            return credentials
        }
        guard let data = try self.loadData(account: "client") else { return nil }

        let credentials = try JSONDecoder().decode(OAuthClientCredentials.self, from: data)
        try? self.save(clientCredentials: credentials)
        return credentials
    }

    public func loadClientCredentials(
        provider: SourceControlProvider,
        host: URL,
        kind: TokenCredentialKind
    ) throws -> OAuthClientCredentials? {
        if let data = try self.loadData(
            account: Self.clientCredentialsAccount(
                provider: provider,
                host: host,
                kind: kind
            )
        ) {
            return try JSONDecoder().decode(OAuthClientCredentials.self, from: data)
        }

        guard provider == .github, kind == .oauth, ProviderCredential.normalizedHost(host.host ?? host.absoluteString) == "github.com",
              let data = try self.loadData(account: "client")
        else { return nil }

        let credentials = try JSONDecoder().decode(OAuthClientCredentials.self, from: data)
        try? self.save(clientCredentials: credentials, provider: provider, host: host, kind: kind)
        return credentials
    }

    public func clearClientCredentials(provider: SourceControlProvider, host: URL, kind: TokenCredentialKind) {
        self.clear(account: Self.clientCredentialsAccount(provider: provider, host: host, kind: kind))
    }

    public func clear() {
        self.clear(account: "default")
        self.clear(account: "client")
        self.clearPAT()
        self.clearCredential(provider: .github, host: RepositoryHost.githubCom.webBaseURL, kind: .oauth)
        self.clearCredential(provider: .github, host: RepositoryHost.githubCom.webBaseURL, kind: .pat)
        self.clear(
            account: Self.clientCredentialsAccount(
                provider: .github,
                host: RepositoryHost.githubCom.webBaseURL,
                kind: .oauth
            )
        )
    }

    // MARK: - PAT Storage

    public func savePAT(_ token: String) throws {
        let data = Data(token.utf8)
        try self.save(data: data, account: "pat")
        try self.savePAT(token, provider: .github, host: RepositoryHost.githubCom.webBaseURL)
    }

    public func loadPAT() throws -> String? {
        if let token = try self.loadPAT(provider: .github, host: RepositoryHost.githubCom.webBaseURL) { return token }
        guard let data = try self.loadData(account: "pat") else { return nil }

        let token = String(data: data, encoding: .utf8)
        if let token {
            try? self.savePAT(token)
        }
        return token
    }

    public func clearPAT() {
        self.clear(account: "pat")
        self.clearCredential(provider: .github, host: RepositoryHost.githubCom.webBaseURL, kind: .pat)
    }

    public func savePAT(_ token: String, provider: SourceControlProvider, host: URL) throws {
        if provider == .github, ProviderCredential.normalizedHost(host.host ?? host.absoluteString) == "github.com" {
            try self.save(data: Data(token.utf8), account: "pat")
        }
        try self.save(
            credential: ProviderCredential(
                provider: provider,
                host: host,
                kind: .pat,
                headerStyle: Self.defaultHeaderStyle(provider: provider, kind: .pat),
                token: token
            )
        )
    }

    public func loadPAT(provider: SourceControlProvider, host: URL) throws -> String? {
        try self.loadCredential(provider: provider, host: host, kind: .pat)?.token
    }

    public func saveAPIToken(_ token: String, username: String, provider: SourceControlProvider, host: URL) throws {
        try self.save(
            credential: ProviderCredential(
                provider: provider,
                host: host,
                kind: .apiToken,
                headerStyle: Self.defaultHeaderStyle(provider: provider, kind: .apiToken),
                token: token,
                username: username
            )
        )
    }

    public func save(credential: ProviderCredential) throws {
        let data = try JSONEncoder().encode(credential)
        try self.save(data: data, account: Self.credentialAccount(provider: credential.provider, host: credential.host, kind: credential.kind))
    }

    public func loadCredential(provider: SourceControlProvider, host: URL, kind: TokenCredentialKind) throws -> ProviderCredential? {
        try self.loadCredential(provider: provider, host: host.host ?? host.absoluteString, kind: kind)
    }

    public func loadCredential(provider: SourceControlProvider, host: String, kind: TokenCredentialKind) throws -> ProviderCredential? {
        let account = Self.credentialAccount(provider: provider, host: host, kind: kind)
        guard let data = try self.loadData(account: account) else {
            return try self.legacyCredential(provider: provider, host: host, kind: kind)
        }

        return try JSONDecoder().decode(ProviderCredential.self, from: data)
    }

    public func clearCredential(provider: SourceControlProvider, host: URL, kind: TokenCredentialKind) {
        self.clear(account: Self.credentialAccount(provider: provider, host: host, kind: kind))
    }

    public static func credentialAccount(provider: SourceControlProvider, host: URL, kind: TokenCredentialKind) -> String {
        self.credentialAccount(provider: provider, host: host.host ?? host.absoluteString, kind: kind)
    }

    public static func credentialAccount(provider: SourceControlProvider, host: String, kind: TokenCredentialKind) -> String {
        "\(provider.rawValue):\(ProviderCredential.normalizedHost(host)):\(kind.rawValue)"
    }

    public static func clientCredentialsAccount(provider: SourceControlProvider, host: URL, kind: TokenCredentialKind) -> String {
        "\(self.credentialAccount(provider: provider, host: host, kind: kind)):client"
    }

    public static func defaultHeaderStyle(provider: SourceControlProvider, kind: TokenCredentialKind) -> CredentialHeaderStyle {
        switch (provider, kind) {
        case (_, .oauth):
            .authorizationBearer
        case (.github, .pat):
            .authorizationBearer
        case (.gitlab, .pat):
            .privateToken
        case (.bitbucketCloud, .apiToken):
            .basic
        case (.forgejo, .pat), (.gitea, .pat):
            .authorizationToken
        case (_, .none), (_, .apiToken), (_, .pat):
            .none
        }
    }
}

private extension TokenStore {
    func legacyCredential(provider: SourceControlProvider, host: String, kind: TokenCredentialKind) throws -> ProviderCredential? {
        guard provider == .github, ProviderCredential.normalizedHost(host) == "github.com" else { return nil }

        switch kind {
        case .oauth:
            guard let data = try self.loadData(account: "default") else { return nil }

            let tokens = try JSONDecoder().decode(OAuthTokens.self, from: data)
            let credential = ProviderCredential(
                provider: .github,
                host: RepositoryHost.githubCom.webBaseURL,
                kind: .oauth,
                headerStyle: .authorizationBearer,
                token: tokens.accessToken,
                refreshToken: tokens.refreshToken,
                expiresAt: tokens.expiresAt
            )
            try? self.save(credential: credential)
            return credential
        case .pat:
            guard let data = try self.loadData(account: "pat"),
                  let token = String(data: data, encoding: .utf8)
            else { return nil }

            let credential = ProviderCredential(
                provider: .github,
                host: RepositoryHost.githubCom.webBaseURL,
                kind: .pat,
                headerStyle: .authorizationBearer,
                token: token
            )
            try? self.save(credential: credential)
            return credential
        case .apiToken, .none:
            return nil
        }
    }
}

extension TokenStore {
    static let sharedAccessGroupSuffix = "com.steipete.repobar.shared"
    private static let storageModeInfoKey = "RepoBarTokenStore"
    private static let storageModeEnvKey = "REPOBAR_TOKEN_STORE"

    static func defaultAccessGroup() -> String? {
        #if os(macOS)
            guard let task = SecTaskCreateFromSelf(nil),
                  let entitlement = SecTaskCopyValueForEntitlement(task, "keychain-access-groups" as CFString, nil)
            else {
                return nil
            }

            if let groups = entitlement as? [String] {
                return groups.first(where: { $0.hasSuffix(Self.sharedAccessGroupSuffix) })
            }
            return nil
        #else
            if let group = Bundle.main.object(forInfoDictionaryKey: "RepoBarKeychainAccessGroup") as? String {
                if group.isEmpty == false {
                    return group
                }
            }
            return nil
        #endif
    }

    static func defaultStorage() -> TokenStoreStorage {
        let configured = ProcessInfo.processInfo.environment[Self.storageModeEnvKey]
            ?? Bundle.main.object(forInfoDictionaryKey: Self.storageModeInfoKey) as? String
        switch configured?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "file", "disk":
            return .file(Self.defaultFileDirectory())
        case "keychain":
            return .keychain
        default:
            #if DEBUG
                return .file(Self.defaultFileDirectory())
            #else
                return .keychain
            #endif
        }
    }

    static func defaultFileDirectory() -> URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)
            .first ?? FileManager.default.homeDirectoryForCurrentUser
        return base
            .appendingPathComponent("RepoBar", isDirectory: true)
            .appendingPathComponent("DebugAuth", isDirectory: true)
    }
}

private extension TokenStore {
    func save(data: Data, account: String) throws {
        if case let .file(directory) = self.storage {
            try self.saveFile(data: data, account: account, directory: directory)
            return
        }

        let accessGroups = self.accessGroupsForOperation()
        var lastStatus: OSStatus = errSecSuccess
        for (index, group) in accessGroups.enumerated() {
            let query = self.baseQuery(account: account, accessGroup: group)
            let attributes: [CFString: Any] = [kSecValueData: data]
            var addQuery = query
            addQuery.merge(attributes) { _, new in new }
            var status = SecItemAdd(addQuery as CFDictionary, nil)
            if status == errSecDuplicateItem {
                status = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
            }
            if status == errSecSuccess { return }
            lastStatus = status
            let isFinalAttempt = index == accessGroups.count - 1
            if isFinalAttempt || self.shouldRetryWithoutAccessGroup(status: status, accessGroup: group) == false {
                break
            }
        }
        self.logFailure("save", status: lastStatus)
        throw TokenStoreError.saveFailed
    }

    func loadData(account: String) throws -> Data? {
        if case let .file(directory) = self.storage {
            return try self.loadFile(account: account, directory: directory)
        }

        let accessGroups = self.accessGroupsForOperation()
        var lastStatus: OSStatus = errSecSuccess
        for (index, group) in accessGroups.enumerated() {
            var query = self.baseQuery(account: account, accessGroup: group)
            query[kSecReturnData] = true
            var item: CFTypeRef?
            let status = SecItemCopyMatching(query as CFDictionary, &item)
            if status == errSecItemNotFound {
                if index == accessGroups.count - 1 { return nil }
                continue
            }
            if status == errSecSuccess, let data = item as? Data { return data }
            lastStatus = status
            let isFinalAttempt = index == accessGroups.count - 1
            if isFinalAttempt || self.shouldRetryWithoutAccessGroup(status: status, accessGroup: group) == false {
                break
            }
        }
        self.logFailure("load", status: lastStatus)
        throw TokenStoreError.loadFailed
    }

    func clear(account: String) {
        if case let .file(directory) = self.storage {
            try? FileManager.default.removeItem(at: self.fileURL(account: account, directory: directory))
            return
        }

        let accessGroups = self.accessGroupsForOperation()
        for group in accessGroups {
            let query = self.baseQuery(account: account, accessGroup: group)
            SecItemDelete(query as CFDictionary)
        }
    }

    func accessGroupsForOperation() -> [String?] {
        guard let accessGroup else { return [nil] }

        return [accessGroup, nil]
    }

    func baseQuery(account: String, accessGroup: String?) -> [CFString: Any] {
        var query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account
        ]
        if let accessGroup {
            query[kSecAttrAccessGroup] = accessGroup
        }
        return query
    }

    func shouldRetryWithoutAccessGroup(status: OSStatus, accessGroup: String?) -> Bool {
        guard accessGroup != nil else { return false }

        switch status {
        case errSecMissingEntitlement, errSecInteractionNotAllowed:
            return true
        default:
            return false
        }
    }

    func logFailure(_ action: String, status: OSStatus) {
        guard status != errSecSuccess else { return }

        let statusMessage = SecCopyErrorMessageString(status, nil) as String?
        if let statusMessage {
            self.logger.error("Keychain \(action) failed: \(statusMessage)")
        } else {
            self.logger.error("Keychain \(action) failed: OSStatus \(status)")
        }
    }

    func saveFile(data: Data, account: String, directory: URL) throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = self.fileURL(account: account, directory: directory)
        try data.write(to: url, options: [.atomic])
        try? FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
    }

    func loadFile(account: String, directory: URL) throws -> Data? {
        let url = self.fileURL(account: account, directory: directory)
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }

        return try Data(contentsOf: url)
    }

    func fileURL(account: String, directory: URL) -> URL {
        let serviceName = self.sanitizedFileComponent(self.service)
        let accountName = self.sanitizedFileComponent(account)
        return directory.appendingPathComponent("\(serviceName)-\(accountName).json", isDirectory: false)
    }

    func sanitizedFileComponent(_ value: String) -> String {
        let allowed = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: ".-_"))
        let scalars = value.unicodeScalars.map { scalar in
            allowed.contains(scalar) ? Character(scalar) : "-"
        }
        let result = String(scalars)
        return result.isEmpty ? "value" : result
    }
}
