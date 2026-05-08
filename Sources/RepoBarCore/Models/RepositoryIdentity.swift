import Foundation

public struct RepositoryIdentity: Equatable, Codable, Hashable, Sendable {
    public var provider: SourceControlProvider
    public var id: String
    public var name: String
    public var namespacePath: String
    public var pathWithNamespace: String
    public var slug: String
    public var webURL: URL?
    public var apiURL: URL?
    public var providerSpecificID: String?

    public init(
        provider: SourceControlProvider,
        id: String,
        name: String,
        namespacePath: String,
        pathWithNamespace: String? = nil,
        slug: String? = nil,
        webURL: URL? = nil,
        apiURL: URL? = nil,
        providerSpecificID: String? = nil
    ) {
        self.provider = provider
        self.id = id
        self.name = name
        self.namespacePath = namespacePath
        let resolvedPath = pathWithNamespace ?? Self.makePath(namespacePath: namespacePath, name: name)
        self.pathWithNamespace = resolvedPath
        self.slug = slug ?? name
        self.webURL = webURL
        self.apiURL = apiURL
        self.providerSpecificID = providerSpecificID
    }

    public static func github(
        id: String,
        owner: String,
        name: String,
        webURL: URL? = nil,
        apiURL: URL? = nil,
        providerSpecificID: String? = nil
    ) -> RepositoryIdentity {
        RepositoryIdentity(
            provider: .github,
            id: id,
            name: name,
            namespacePath: owner,
            webURL: webURL,
            apiURL: apiURL,
            providerSpecificID: providerSpecificID
        )
    }

    private static func makePath(namespacePath: String, name: String) -> String {
        let namespace = namespacePath.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        guard !namespace.isEmpty else { return name }

        return "\(namespace)/\(name)"
    }
}
