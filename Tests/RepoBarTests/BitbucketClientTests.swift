import Foundation
@testable import RepoBarCore
import Testing

struct BitbucketClientTests {
    @Test
    func `repository list maps permissions and follows next pagination`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            #expect(request.value(forHTTPHeaderField: "Authorization") == "Basic bWVAZXhhbXBsZS5jb206YmItdG9rZW4=")
            #expect(request.url?.path == "/2.0/user/permissions/repositories")
            let page = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)?
                .queryItems?.first(where: { $0.name == "page" })?.value
            if page == "2" {
                let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
                return (Data(Self.permissionPage(id: "2", fullName: "workspace/repo-two", name: "repo-two").utf8), response)
            }

            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (
                Data(Self.permissionPage(
                    id: "1",
                    fullName: "workspace/repo-one",
                    name: "repo-one",
                    next: "https://api.bitbucket.org/2.0/user/permissions/repositories?page=2"
                ).utf8),
                response
            )
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let repositories = try await Self.client(handlerID: handlerID).repositoryList(limit: nil)

        #expect(repositories.map(\.pathWithNamespace) == ["workspace/repo-one", "workspace/repo-two"])
        #expect(repositories.first?.provider == .bitbucketCloud)
        #expect(repositories.first?.owner == "workspace")
        #expect(repositories.first?.name == "repo-one")
        #expect(repositories.first?.webURL?.absoluteString == "https://bitbucket.org/workspace/repo-one")
    }

    @Test
    func `recent pull requests issues refs and commits map summaries`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            switch request.url?.path {
            case "/2.0/repositories/workspace/repo/pullrequests":
                return (Data(Self.page(values: """
                [{
                  "id": 7,
                  "title": "Add feature",
                  "updated_on": "2026-05-08T10:20:30.000000+00:00",
                  "created_on": "2026-05-07T10:20:30.000000+00:00",
                  "author": {"nickname": "ana"},
                  "comment_count": 3,
                  "task_count": 1,
                  "links": {"html": {"href": "https://bitbucket.org/workspace/repo/pull-requests/7"}},
                  "source": {"branch": {"name": "feature"}},
                  "destination": {"branch": {"name": "main"}}
                }]
                """).utf8), response)
            case "/2.0/repositories/workspace/repo/issues":
                return (Data(Self.page(values: """
                [{
                  "id": 5,
                  "title": "Fix issue",
                  "updated_on": "2026-05-08T10:20:30.000000+00:00",
                  "created_on": "2026-05-07T10:20:30.000000+00:00",
                  "reporter": {"nickname": "mira"},
                  "assignee": {"nickname": "sam"},
                  "comment_count": 2,
                  "links": {"html": {"href": "https://bitbucket.org/workspace/repo/issues/5"}}
                }]
                """).utf8), response)
            case "/2.0/repositories/workspace/repo/refs/branches":
                return (Data(Self.page(values: #"[{"name":"main","target":{"hash":"abc"}}]"#).utf8), response)
            case "/2.0/repositories/workspace/repo/refs/tags":
                return (Data(Self.page(values: #"[{"name":"v1.0","target":{"hash":"def"}}]"#).utf8), response)
            case "/2.0/repositories/workspace/repo/commits":
                return (Data(Self.page(values: """
                [{
                  "hash": "abc123",
                  "message": "Initial commit",
                  "date": "2026-05-08T10:20:30.000000+00:00",
                  "author": {"raw": "Ana <ana@example.com>", "user": {"nickname": "ana"}},
                  "links": {"html": {"href": "https://bitbucket.org/workspace/repo/commits/abc123"}}
                }]
                """).utf8), response)
            default:
                Issue.record("Unexpected path \(request.url?.path ?? "")")
                return (Data(Self.page(values: "[]").utf8), response)
            }
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let pulls = try await client.recentPullRequests(owner: "workspace", name: "repo", limit: 10)
        let issues = try await client.recentIssues(owner: "workspace", name: "repo", limit: 10)
        let branches = try await client.recentBranches(owner: "workspace", name: "repo", limit: 10)
        let tags = try await client.recentTags(owner: "workspace", name: "repo", limit: 10)
        let commits = try await client.recentCommits(owner: "workspace", name: "repo", limit: 10)

        #expect(pulls.first?.number == 7)
        #expect(pulls.first?.headRefName == "feature")
        #expect(issues.first?.number == 5)
        #expect(issues.first?.assigneeLogins == ["sam"])
        #expect(branches.first?.commitSHA == "abc")
        #expect(tags.first?.name == "v1.0")
        #expect(commits.items.first?.sha == "abc123")
    }

    @Test
    func `repo contents and file contents use source endpoints`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            if request.url?.path == "/2.0/repositories/workspace/repo/src/HEAD/README.md" {
                return (Data("hello".utf8), response)
            }
            #expect(request.url?.path == "/2.0/repositories/workspace/repo/src/HEAD")
            return (Data(Self.page(values: """
            [{
              "path": "README.md",
              "type": "commit_file",
              "size": 12,
              "links": {
                "self": {"href": "https://api.bitbucket.org/2.0/repositories/workspace/repo/src/HEAD/README.md"},
                "html": {"href": "https://bitbucket.org/workspace/repo/src/main/README.md"}
              }
            }]
            """).utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let client = try await Self.client(handlerID: handlerID)
        let contents = try await client.repoContents(owner: "workspace", name: "repo")
        let file = try await client.repoFileContents(owner: "workspace", name: "repo", path: "README.md")

        #expect(contents.first?.name == "README.md")
        #expect(contents.first?.type == .file)
        #expect(String(data: file, encoding: .utf8) == "hello")
    }

    @Test
    func `router delegates bitbucket repository list`() async throws {
        let handlerID = UUID().uuidString
        Self.MockURLProtocol.register(handlerID: handlerID) { request in
            let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
            return (Data(Self.permissionPage(id: "1", fullName: "workspace/repo", name: "repo").utf8), response)
        }
        defer { Self.MockURLProtocol.unregister(handlerID: handlerID) }

        let bitbucket = try await Self.client(handlerID: handlerID)
        let router = RepositoryServiceRouter(provider: .bitbucketCloud, bitbucketClient: bitbucket)
        let repositories = try await router.repositoryList(limit: 1)

        #expect(repositories.count == 1)
        #expect(repositories.first?.provider == .bitbucketCloud)
    }
}

private extension BitbucketClientTests {
    static func client(handlerID: String) async throws -> BitbucketClient {
        let client = try BitbucketClient(
            apiHost: #require(URL(string: "https://api.bitbucket.org/2.0")),
            session: Self.session(handlerID: handlerID)
        )
        await client.setCredentialProvider {
            ProviderCredential(
                provider: .bitbucketCloud,
                host: "bitbucket.org",
                kind: .apiToken,
                headerStyle: .basic,
                token: "bb-token",
                username: "me@example.com"
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

    static func page(values: String, next: String? = nil) -> String {
        """
        {
          "pagelen": 100,
          "values": \(values),
          "next": \(next.map { "\"\($0)\"" } ?? "null")
        }
        """
    }

    static func permissionPage(id: String, fullName: String, name: String, next: String? = nil) -> String {
        self.page(values: """
        [{
          "type": "repository_permission",
          "permission": "admin",
          "repository": \(self.repositoryJSON(id: id, fullName: fullName, name: name))
        }]
        """, next: next)
    }

    static func repositoryJSON(id: String, fullName: String, name: String) -> String {
        """
        {
          "uuid": "{\(id)}",
          "full_name": "\(fullName)",
          "name": "\(name)",
          "scm": "git",
          "updated_on": "2026-05-08T10:20:30.000000+00:00",
          "has_issues": true,
          "is_private": true,
          "owner": {"username": "\(fullName.split(separator: "/").first ?? "")"},
          "links": {
            "html": {"href": "https://bitbucket.org/\(fullName)"},
            "self": {"href": "https://api.bitbucket.org/2.0/repositories/\(fullName)"}
          },
          "project": {"key": "PROJ", "name": "Project", "uuid": "{project}"}
        }
        """
    }

    final class MockURLProtocol: URLProtocol {
        private static let handlersLock = NSLock()
        private nonisolated(unsafe) static var handlers: [String: @Sendable (URLRequest) throws -> (Data, URLResponse)] = [:]

        static func register(handlerID: String, handler: @escaping @Sendable (URLRequest) throws -> (Data, URLResponse)) {
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
