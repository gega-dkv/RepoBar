import Foundation
@testable import RepoBarCore
import Testing

struct GitLabClientTests {
    @Test
    func `repository list maps projects and follows x next page`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            #expect(request.value(forHTTPHeaderField: "PRIVATE-TOKEN") == "glpat-token")
            #expect(request.url?.path == "/api/v4/projects")
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            let page = components?.queryItems?.first(where: { $0.name == "page" })?.value
            let response: HTTPURLResponse
            let data: Data

            if page == "1" {
                response = HTTPURLResponse(
                    url: request.url!,
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["x-next-page": "2"]
                )!
                data = Data(Self.projectJSONArray(id: 10, path: "group/subgroup/repo-one", name: "repo-one").utf8)
            } else {
                response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
                data = Data(Self.projectJSONArray(id: 11, path: "group/repo-two", name: "repo-two").utf8)
            }
            return (data, response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let repositories = try await client.repositoryList(limit: nil)

        #expect(repositories.map(\.pathWithNamespace) == ["group/subgroup/repo-one", "group/repo-two"])
        #expect(repositories[0].provider == .gitlab)
        #expect(repositories[0].owner == "group/subgroup")
        #expect(repositories[0].name == "repo-one")
        #expect(repositories[0].openIssues == 3)
        #expect(repositories[0].stars == 7)
        #expect(repositories[0].forks == 2)
        #expect(repositories[0].webURL?.absoluteString == "https://gitlab.com/group/subgroup/repo-one")
    }

    @Test
    func `full repository URL encodes namespace path`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            #expect(components?.percentEncodedPath == "/api/v4/projects/group%2Fsubgroup%2Frepo")

            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(Self.projectJSONObject(id: 42, path: "group/subgroup/repo", name: "repo").utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let repository = try await client.fullRepository(owner: "group/subgroup", name: "repo")

        #expect(repository.id == "42")
        #expect(repository.pathWithNamespace == "group/subgroup/repo")
    }

    @Test
    func `repository list falls back to link pagination`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            let page = components?.queryItems?.first(where: { $0.name == "page" })?.value
            let response: HTTPURLResponse
            let data: Data

            if page == "1" {
                response = HTTPURLResponse(
                    url: request.url!,
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: ["Link": #"<https://gitlab.com/api/v4/projects?page=2&per_page=100>; rel="next""#]
                )!
                data = Data(Self.projectJSONArray(id: 20, path: "group/one", name: "one").utf8)
            } else {
                response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
                data = Data(Self.projectJSONArray(id: 21, path: "group/two", name: "two").utf8)
            }
            return (data, response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let repositories = try await client.repositoryList(limit: nil)

        #expect(repositories.map(\.name) == ["one", "two"])
    }

    @Test
    func `recent issues and merge requests map summaries`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            if request.url?.path.hasSuffix("/issues") == true {
                return (
                    Data("""
                    [{
                      "iid": 5,
                      "title": "Fix issue",
                      "web_url": "https://gitlab.com/group/repo/-/issues/5",
                      "updated_at": "2026-05-08T10:20:30.000Z",
                      "created_at": "2026-05-07T10:20:30.000Z",
                      "author": {"username": "ana", "avatar_url": "https://gitlab.com/avatar.png"},
                      "assignees": [{"username": "sam"}],
                      "user_notes_count": 4,
                      "labels": ["bug"]
                    }]
                    """.utf8),
                    response
                )
            }

            #expect(request.url?.path.hasSuffix("/merge_requests") == true)
            return (
                Data("""
                [{
                  "iid": 7,
                  "title": "Add feature",
                  "web_url": "https://gitlab.com/group/repo/-/merge_requests/7",
                  "updated_at": "2026-05-08T11:20:30Z",
                  "created_at": "2026-05-07T11:20:30Z",
                  "author": {"username": "mira"},
                  "user_notes_count": 2,
                  "labels": ["feature"],
                  "source_branch": "feature",
                  "target_branch": "main",
                  "draft": true
                }]
                """.utf8),
                response
            )
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let issues = try await client.recentIssues(owner: "group", name: "repo", limit: 10)
        let mergeRequests = try await client.recentPullRequests(owner: "group", name: "repo", limit: 10)

        #expect(issues.first?.number == 5)
        #expect(issues.first?.authorLogin == "ana")
        #expect(issues.first?.assigneeLogins == ["sam"])
        #expect(issues.first?.labels.map(\.name) == ["bug"])
        #expect(mergeRequests.first?.number == 7)
        #expect(mergeRequests.first?.isDraft == true)
        #expect(mergeRequests.first?.headRefName == "feature")
        #expect(mergeRequests.first?.baseRefName == "main")
    }

    @Test
    func `router delegates gitlab repository list`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(Self.projectJSONArray(id: 12, path: "group/repo", name: "repo").utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let gitLab = try await Self.client(handlerID: handlerID)
        let router = RepositoryServiceRouter(provider: .gitlab, gitLabClient: gitLab)
        let repositories = try await router.repositoryList(limit: 1)

        #expect(repositories.count == 1)
        #expect(repositories.first?.provider == .gitlab)
    }
}

private extension GitLabClientTests {
    static func client(handlerID: String) async throws -> GitLabClient {
        let client = try GitLabClient(
            apiHost: #require(URL(string: "https://gitlab.com/api/v4")),
            session: Self.session(handlerID: handlerID)
        )
        await client.setCredentialProvider {
            ProviderCredential(
                provider: .gitlab,
                host: "gitlab.com",
                kind: .pat,
                headerStyle: .privateToken,
                token: "glpat-token"
            )
        }
        return client
    }

    static func session(handlerID: String) -> URLSession {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        config.httpAdditionalHeaders = ["X-Handler-ID": handlerID]
        return URLSession(configuration: config)
    }

    static func projectJSONArray(id: Int, path: String, name: String) -> String {
        "[\(self.projectJSONObject(id: id, path: path, name: name))]"
    }

    static func projectJSONObject(id: Int, path: String, name: String) -> String {
        """
        {
          "id": \(id),
          "name": "\(name)",
          "path": "\(name)",
          "path_with_namespace": "\(path)",
          "web_url": "https://gitlab.com/\(path)",
          "archived": false,
          "star_count": 7,
          "forks_count": 2,
          "open_issues_count": 3,
          "last_activity_at": "2026-05-08T10:20:30.000Z",
          "default_branch": "main",
          "namespace": {"full_path": "\(path.split(separator: "/").dropLast().joined(separator: "/"))"},
          "issues_enabled": true,
          "merge_requests_enabled": true,
          "jobs_enabled": true
        }
        """
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
