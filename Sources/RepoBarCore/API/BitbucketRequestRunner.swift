import Foundation

public enum BitbucketAPIError: Error, LocalizedError, Equatable, Sendable {
    case invalidHost
    case missingCredential
    case badStatus(code: Int, message: String?)

    public var errorDescription: String? {
        switch self {
        case .invalidHost:
            "Invalid Bitbucket API host."
        case .missingCredential:
            "Missing Bitbucket credential."
        case let .badStatus(code, message):
            if let message, message.isEmpty == false {
                "Bitbucket request failed (HTTP \(code)). \(message)"
            } else {
                "Bitbucket request failed (HTTP \(code))."
            }
        }
    }
}

struct BitbucketRequestRunner {
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
        var nextURL: URL? = try Self.url(
            apiHost: apiHost,
            path: path,
            queryItems: queryItems + [URLQueryItem(name: "pagelen", value: "\(pageSize)")]
        )
        var output: [T] = []

        while let url = nextURL {
            let (data, _) = try await self.response(url: url, credential: credential)
            let page = try self.decoder.decode(BitbucketPage<T>.self, from: data)
            output.append(contentsOf: page.values)
            if let limit, output.count >= limit {
                return Array(output.prefix(limit))
            }
            nextURL = page.next
        }
        return output
    }

    func count(
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem] = [],
        credential: ProviderCredential
    ) async throws -> Int? {
        let data = try await self.data(
            apiHost: apiHost,
            path: path,
            queryItems: queryItems + [URLQueryItem(name: "pagelen", value: "1")],
            credential: credential
        )
        return try self.decoder.decode(BitbucketCountPage.self, from: data).size
    }

    func data(
        apiHost: URL,
        path: String,
        queryItems: [URLQueryItem] = [],
        credential: ProviderCredential
    ) async throws -> Data {
        try await self.response(
            url: Self.url(apiHost: apiHost, path: path, queryItems: queryItems),
            credential: credential
        ).0
    }

    private func response(url: URL, credential: ProviderCredential) async throws -> (Data, HTTPURLResponse) {
        var request = URLRequest(url: url)
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        credential.headerStyle.apply(to: &request, credential: credential)

        let (data, response) = try await self.session.data(for: request)
        guard let http = response as? HTTPURLResponse else {
            throw BitbucketAPIError.badStatus(code: -1, message: "Unexpected response.")
        }
        guard (200 ..< 300).contains(http.statusCode) else {
            throw BitbucketAPIError.badStatus(code: http.statusCode, message: Self.errorMessage(from: data))
        }

        return (data, http)
    }

    private static func url(apiHost: URL, path: String, queryItems: [URLQueryItem]) throws -> URL {
        guard var components = URLComponents(url: apiHost, resolvingAgainstBaseURL: false) else {
            throw BitbucketAPIError.invalidHost
        }

        let basePath = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let suffix = path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        components.percentEncodedPath = "/" + [basePath, suffix].filter { $0.isEmpty == false }.joined(separator: "/")
        components.queryItems = queryItems.isEmpty ? nil : queryItems
        guard let url = components.url else { throw BitbucketAPIError.invalidHost }

        return url
    }

    private static func decodeDate(from decoder: Decoder) throws -> Date {
        let value = try decoder.singleValueContainer().decode(String.self)
        let formatters = BitbucketDateFormatters.shared
        let date = formatters.iso8601WithFractional.date(from: value)
            ?? formatters.iso8601.date(from: value)
            ?? formatters.dateTimeWithMicroseconds.date(from: value)
            ?? formatters.dateTimeWithoutFraction.date(from: value)
        if let date {
            return date
        }
        throw DecodingError.dataCorrupted(
            .init(codingPath: decoder.codingPath, debugDescription: "Invalid Bitbucket date: \(value)")
        )
    }

    private static func errorMessage(from data: Data) -> String? {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return String(data: data, encoding: .utf8)
        }

        if let error = object["error"] as? [String: Any], let message = error["message"] as? String {
            return message
        }
        if let message = object["message"] as? String { return message }
        return nil
    }
}

private struct BitbucketCountPage: Decodable {
    let size: Int?
}

private final class BitbucketDateFormatters: @unchecked Sendable {
    static let shared = BitbucketDateFormatters()
    let iso8601WithFractional: ISO8601DateFormatter
    let iso8601: ISO8601DateFormatter
    let dateTimeWithMicroseconds: DateFormatter
    let dateTimeWithoutFraction: DateFormatter

    private init() {
        self.iso8601WithFractional = ISO8601DateFormatter()
        self.iso8601WithFractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        self.iso8601 = ISO8601DateFormatter()
        self.iso8601.formatOptions = [.withInternetDateTime]
        self.dateTimeWithMicroseconds = DateFormatter()
        self.dateTimeWithMicroseconds.locale = Locale(identifier: "en_US_POSIX")
        self.dateTimeWithMicroseconds.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSSSSXXXXX"
        self.dateTimeWithoutFraction = DateFormatter()
        self.dateTimeWithoutFraction.locale = Locale(identifier: "en_US_POSIX")
        self.dateTimeWithoutFraction.dateFormat = "yyyy-MM-dd'T'HH:mm:ssXXXXX"
    }
}
