import Foundation
@testable import RepoBarCore
import Testing

struct ProviderTokenAuthenticatorTests {
    @Test
    func `gitlab PAT validates current user and saves credential`() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = TokenStore(service: "com.steipete.repobar.auth.tests.\(UUID().uuidString)", storage: .file(directory))
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            #expect(request.url?.absoluteString == "https://gitlab.com/api/v4/user")
            #expect(request.value(forHTTPHeaderField: "PRIVATE-TOKEN") == "glpat-token")

            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(#"{"username":"gitlab-user"}"#.utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let authenticator = ProviderTokenAuthenticator(
            tokenStore: store,
            session: Self.session(handlerID: handlerID)
        )
        let user = try await authenticator.authenticatePAT(
            provider: .gitlab,
            token: "glpat-token",
            host: #require(URL(string: "https://gitlab.com"))
        )
        let credential = try store.loadCredential(
            provider: .gitlab,
            host: #require(URL(string: "https://gitlab.com")),
            kind: .pat
        )

        #expect(user.username == "gitlab-user")
        #expect(credential?.token == "glpat-token")
        #expect(credential?.headerStyle == .privateToken)
    }

    @Test
    func `bitbucket API token uses basic auth and saves credential`() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = TokenStore(service: "com.steipete.repobar.auth.tests.\(UUID().uuidString)", storage: .file(directory))
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            #expect(request.url?.absoluteString == "https://api.bitbucket.org/2.0/user")
            #expect(request.value(forHTTPHeaderField: "Authorization") == "Basic bWVAZXhhbXBsZS5jb206YmItdG9rZW4=")

            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(#"{"display_name":"Bitbucket User","account_id":"abc"}"#.utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let authenticator = ProviderTokenAuthenticator(
            tokenStore: store,
            session: Self.session(handlerID: handlerID)
        )
        let user = try await authenticator.authenticateBitbucketAPIToken(email: "me@example.com", token: "bb-token")
        let credential = try store.loadCredential(
            provider: .bitbucketCloud,
            host: #require(URL(string: "https://bitbucket.org")),
            kind: .apiToken
        )

        #expect(user.username == "Bitbucket User")
        #expect(credential?.token == "bb-token")
        #expect(credential?.username == "me@example.com")
        #expect(credential?.headerStyle == .basic)
    }

    @Test
    func `forgejo PAT uses token auth and saves credential`() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = TokenStore(service: "com.steipete.repobar.auth.tests.\(UUID().uuidString)", storage: .file(directory))
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            #expect(request.url?.absoluteString == "https://codeberg.org/api/v1/user")
            #expect(request.value(forHTTPHeaderField: "Authorization") == "token forgejo-token")

            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(#"{"login":"forgejo-user"}"#.utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let authenticator = ProviderTokenAuthenticator(
            tokenStore: store,
            session: Self.session(handlerID: handlerID)
        )
        let user = try await authenticator.authenticatePAT(
            provider: .forgejo,
            token: "forgejo-token",
            host: #require(URL(string: "https://codeberg.org"))
        )
        let credential = try store.loadCredential(
            provider: .forgejo,
            host: #require(URL(string: "https://codeberg.org")),
            kind: .pat
        )

        #expect(user.username == "forgejo-user")
        #expect(credential?.token == "forgejo-token")
        #expect(credential?.headerStyle == .authorizationToken)
    }

    @Test
    func `failed provider token validation does not save credential`() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("repobar-token-store-\(UUID().uuidString)", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = TokenStore(service: "com.steipete.repobar.auth.tests.\(UUID().uuidString)", storage: .file(directory))
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 401, httpVersion: nil, headerFields: nil)!
            return (Data(), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let authenticator = ProviderTokenAuthenticator(
            tokenStore: store,
            session: Self.session(handlerID: handlerID)
        )
        do {
            _ = try await authenticator.authenticatePAT(
                provider: .gitlab,
                token: "bad-token",
                host: #require(URL(string: "https://gitlab.com"))
            )
            Issue.record("Expected invalid token")
        } catch let error as ProviderTokenAuthError {
            #expect(error == .invalidToken)
        }

        let credential = try store.loadCredential(
            provider: .gitlab,
            host: #require(URL(string: "https://gitlab.com")),
            kind: .pat
        )
        #expect(credential == nil)
    }
}

private extension ProviderTokenAuthenticatorTests {
    static func session(handlerID: String) -> URLSession {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        config.httpAdditionalHeaders = ["X-Handler-ID": handlerID]
        return URLSession(configuration: config)
    }

    final class MockURLProtocol: URLProtocol {
        private static let handlersLock = NSLock()
        private nonisolated(unsafe) static var handlers: [String: @Sendable (URLRequest) throws -> (Data, URLResponse)] = [:]

        static func register(
            handlerID: String,
            handler: @escaping @Sendable (URLRequest) throws -> (Data, URLResponse)
        ) {
            self.handlersLock.lock()
            self.handlers[handlerID] = handler
            self.handlersLock.unlock()
        }

        static func unregister(handlerID: String) {
            self.handlersLock.lock()
            self.handlers.removeValue(forKey: handlerID)
            self.handlersLock.unlock()
        }

        override static func canInit(with _: URLRequest) -> Bool {
            true
        }

        override static func canonicalRequest(for request: URLRequest) -> URLRequest {
            request
        }

        override func startLoading() {
            guard let handlerID = self.request.value(forHTTPHeaderField: "X-Handler-ID") else {
                self.client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
                return
            }

            Self.handlersLock.lock()
            let handler = Self.handlers[handlerID]
            Self.handlersLock.unlock()

            guard let handler else {
                self.client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
                return
            }

            do {
                let (data, response) = try handler(self.request)
                self.client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
                self.client?.urlProtocol(self, didLoad: data)
                self.client?.urlProtocolDidFinishLoading(self)
            } catch {
                self.client?.urlProtocol(self, didFailWithError: error)
            }
        }

        override func stopLoading() {}
    }
}
