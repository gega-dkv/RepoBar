import Foundation

public enum GitLabAPIError: Error, LocalizedError, Equatable, Sendable {
    case invalidHost
    case missingCredential
    case badStatus(code: Int, message: String?)

    public var errorDescription: String? {
        switch self {
        case .invalidHost:
            "Invalid GitLab host."
        case .missingCredential:
            "Missing GitLab credential."
        case let .badStatus(code, message):
            if let message, message.isEmpty == false {
                "GitLab request failed (HTTP \(code)). \(message)"
            } else {
                "GitLab request failed (HTTP \(code))."
            }
        }
    }
}

struct GitLabRequestRunner {
    private let session: URLSession
    private let decoder: JSONDecoder

    init(session: URLSession = .shared) {
        self.session = session
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom(Self.decodeDate)
        self.decoder = decoder
    }

    func get<T: Decodable>(
        _ type: T.Type,
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem] = [],
        credential: ProviderCredential
    ) async throws -> T {
        let data = try await self.data(apiHost: apiHost, path: path, queryItems: queryItems, credential: credential)
        return try self.decoder.decode(type, from: data)
    }

    func paginated<T: Decodable>(
        _: T.Type,
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem] = [],
        limit: Int?,
        credential: ProviderCredential
    ) async throws -> [T] {
        let pageSize = max(1, min(limit ?? 100, 100))
        var page = 1
        var output: [T] = []

        while true {
            var items = queryItems
            items.append(URLQueryItem(name: "page", value: "\(page)"))
            items.append(URLQueryItem(name: "per_page", value: "\(pageSize)"))

            let (data, response) = try await self.response(
                apiHost: apiHost,
                path: path,
                queryItems: items,
                credential: credential
            )
            try output.append(contentsOf: self.decoder.decode([T].self, from: data))
            if let limit, output.count >= limit {
                return Array(output.prefix(limit))
            }

            guard let next = Self.nextPage(from: response) else { return output }

            page = next
        }
    }

    func data(
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem] = [],
        credential: ProviderCredential
    ) async throws -> Data {
        try await self.response(apiHost: apiHost, path: path, queryItems: queryItems, credential: credential).0
    }

    private func response(
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem],
        credential: ProviderCredential
    ) async throws -> (Data, HTTPURLResponse) {
        var request = try URLRequest(url: Self.url(apiHost: apiHost, path: path, queryItems: queryItems))
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        credential.headerStyle.apply(to: &request, credential: credential)

        let (data, response) = try await self.session.data(for: request)
        guard let http = response as? HTTPURLResponse else {
            throw GitLabAPIError.badStatus(code: -1, message: "Unexpected response.")
        }
        guard (200 ..< 300).contains(http.statusCode) else {
            throw GitLabAPIError.badStatus(code: http.statusCode, message: Self.errorMessage(from: data))
        }

        return (data, http)
    }

    private static func url(apiHost: URL, path: String, queryItems: [URLQueryItem]) throws -> URL {
        guard var components = URLComponents(url: apiHost, resolvingAgainstBaseURL: false) else {
            throw GitLabAPIError.invalidHost
        }

        let basePath = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let suffix = path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        components.percentEncodedPath = "/" + [basePath, suffix].filter { $0.isEmpty == false }.joined(separator: "/")
        components.queryItems = queryItems.isEmpty ? nil : queryItems
        guard let url = components.url else { throw GitLabAPIError.invalidHost }

        return url
    }

    private static func decodeDate(from decoder: Decoder) throws -> Date {
        let value = try decoder.singleValueContainer().decode(String.self)
        let formatters = GitLabDateFormatters.shared
        if let date = formatters.iso8601WithFractional.date(from: value) ?? formatters.iso8601.date(from: value) {
            return date
        }
        throw DecodingError.dataCorrupted(
            .init(codingPath: decoder.codingPath, debugDescription: "Invalid GitLab date: \(value)")
        )
    }

    private static func errorMessage(from data: Data) -> String? {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return String(data: data, encoding: .utf8)
        }

        if let message = object["message"] as? String { return message }
        if let error = object["error"] as? String { return error }
        if let message = object["message"] as? [String: Any] {
            return message.map { "\($0.key): \($0.value)" }.joined(separator: ", ")
        }
        return nil
    }

    private static func nextPage(from response: HTTPURLResponse) -> Int? {
        let header = response.value(forHTTPHeaderField: "x-next-page")?
            .trimmingCharacters(in: .whitespacesAndNewlines)
        if let header, header.isEmpty == false, let next = Int(header) {
            return next
        }

        guard let link = response.value(forHTTPHeaderField: "Link") else { return nil }

        return link.split(separator: ",").compactMap { part -> Int? in
            let text = String(part)
            guard text.contains(#"rel="next""#),
                  let start = text.firstIndex(of: "<"),
                  let end = text.firstIndex(of: ">"),
                  start < end
            else { return nil }

            let urlString = String(text[text.index(after: start) ..< end])
            guard let url = URL(string: urlString),
                  let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
                  let value = components.queryItems?.first(where: { $0.name == "page" })?.value
            else { return nil }

            return Int(value)
        }.first
    }
}

private final class GitLabDateFormatters: @unchecked Sendable {
    static let shared = GitLabDateFormatters()
    let iso8601WithFractional: ISO8601DateFormatter
    let iso8601: ISO8601DateFormatter

    private init() {
        self.iso8601WithFractional = ISO8601DateFormatter()
        self.iso8601WithFractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        self.iso8601 = ISO8601DateFormatter()
        self.iso8601.formatOptions = [.withInternetDateTime]
    }
}
