import Foundation
@testable import RepoBarCore
import Testing

struct RateLimitStatusFormatterTests {
    @Test
    func `compact summary uses observed cached rate limits`() {
        let now = Date(timeIntervalSinceReferenceDate: 1000)
        let summary = RepoBarCacheSummary(
            databasePath: "/tmp/cache.sqlite",
            exists: true,
            apiResponseCount: 1,
            graphQLResponseCount: 0,
            rateLimitCount: 0,
            latestResponses: [
                RepoBarCachedResponseSummary(
                    method: "GET",
                    url: "https://api.github.com/user/repos",
                    hasETag: true,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "core",
                    rateLimitRemaining: 4901,
                    rateLimitReset: now.addingTimeInterval(600)
                )
            ],
            rateLimits: []
        )

        let text = RateLimitStatusFormatter.compactSummary(
            diagnostics: .empty,
            cacheSummary: summary,
            now: now
        )

        #expect(text.contains("core"))
        #expect(text.contains("4.9K left"))
    }

    @Test
    func `sections separate observed and active limits`() {
        let now = Date(timeIntervalSinceReferenceDate: 2000)
        let summary = RepoBarCacheSummary(
            databasePath: "/tmp/cache.sqlite",
            exists: true,
            apiResponseCount: 1,
            graphQLResponseCount: 0,
            rateLimitCount: 1,
            latestResponses: [
                RepoBarCachedResponseSummary(
                    method: "GET",
                    url: "https://api.github.com/search/issues",
                    hasETag: false,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "search",
                    rateLimitRemaining: 29,
                    rateLimitReset: now.addingTimeInterval(60)
                )
            ],
            rateLimits: [
                RepoBarRateLimitSummary(
                    resource: "core",
                    remaining: 0,
                    resetAt: now.addingTimeInterval(120),
                    lastError: "API rate limit exceeded"
                )
            ]
        )

        let sections = RateLimitStatusFormatter.sections(
            diagnostics: .empty,
            cacheSummary: summary,
            now: now
        )

        #expect(sections.map(\.title) == ["REST Search", "Active Limits"])
        #expect(sections[0].rows.first?.contains("search") == true)
        #expect(sections[1].rows.first?.contains("API rate limit exceeded") == true)
    }

    @Test
    func `sections group observed resources by github bucket family`() {
        let now = Date(timeIntervalSinceReferenceDate: 3000)
        let summary = RepoBarCacheSummary(
            databasePath: "/tmp/cache.sqlite",
            exists: true,
            apiResponseCount: 3,
            graphQLResponseCount: 0,
            rateLimitCount: 0,
            latestResponses: [
                RepoBarCachedResponseSummary(
                    method: "GET",
                    url: "https://api.github.com/repos/owner/name",
                    hasETag: false,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "core",
                    rateLimitLimit: 5000,
                    rateLimitRemaining: 4990,
                    rateLimitReset: now.addingTimeInterval(600)
                ),
                RepoBarCachedResponseSummary(
                    method: "GET",
                    url: "https://api.github.com/search/issues",
                    hasETag: false,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "search",
                    rateLimitLimit: 30,
                    rateLimitRemaining: 25,
                    rateLimitReset: now.addingTimeInterval(600)
                ),
                RepoBarCachedResponseSummary(
                    method: "POST",
                    url: "https://api.github.com/graphql",
                    hasETag: false,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "graphql",
                    rateLimitLimit: 5000,
                    rateLimitRemaining: 4800,
                    rateLimitReset: now.addingTimeInterval(600)
                )
            ],
            rateLimits: []
        )

        let sections = RateLimitStatusFormatter.sections(
            diagnostics: .empty,
            cacheSummary: summary,
            now: now
        )

        #expect(sections.map(\.title) == ["REST Core", "REST Search", "GraphQL"])
        #expect(sections[0].rows.first?.contains("core") == true)
        #expect(sections[1].rows.first?.contains("search") == true)
        #expect(sections[2].rows.first?.contains("graphql") == true)
    }

    @Test
    func `sections render live rate limit endpoint resources`() throws {
        let now = Date(timeIntervalSinceReferenceDate: 4000)
        let diagnostics = try DiagnosticsSummary(
            apiHost: #require(URL(string: "https://api.github.com")),
            rateLimitReset: nil,
            lastRateLimitError: nil,
            etagEntries: 0,
            backoffEntries: 0,
            restRateLimit: nil,
            graphQLRateLimit: nil,
            rateLimitResources: RateLimitResourcesSnapshot(
                fetchedAt: now,
                resources: [
                    "graphql": RateLimitSnapshot(
                        resource: "graphql",
                        limit: 5000,
                        remaining: 4200,
                        used: 800,
                        reset: now.addingTimeInterval(600),
                        fetchedAt: now
                    ),
                    "core": RateLimitSnapshot(
                        resource: "core",
                        limit: 5000,
                        remaining: 3900,
                        used: 1100,
                        reset: now.addingTimeInterval(600),
                        fetchedAt: now
                    )
                ]
            )
        )

        let sections = RateLimitStatusFormatter.sections(
            diagnostics: diagnostics,
            cacheSummary: nil,
            now: now
        )

        #expect(sections.map(\.title) == ["REST Core", "GraphQL"])
        #expect(sections[0].rows[0].contains("core · 3900/5000 · resets in 10 min."))
        #expect(sections[0].resourceRows[0].quotaText == "3900/5000")
        #expect(sections[0].resourceRows[0].percentRemaining == 78)
        #expect(sections[1].rows[0].contains("graphql · 4200/5000 · resets in 10 min."))
        #expect(sections[1].resourceRows[0].percentRemaining == 84)

        let cachedCore = RepoBarCacheSummary(
            databasePath: "/tmp/cache.sqlite",
            exists: true,
            apiResponseCount: 1,
            graphQLResponseCount: 0,
            rateLimitCount: 0,
            latestResponses: [
                RepoBarCachedResponseSummary(
                    method: "GET",
                    url: "https://api.github.com/user/repos",
                    hasETag: true,
                    statusCode: 200,
                    fetchedAt: now,
                    rateLimitResource: "core",
                    rateLimitLimit: 5000,
                    rateLimitRemaining: 3800,
                    rateLimitReset: now.addingTimeInterval(600)
                )
            ],
            rateLimits: []
        )

        let sectionsWithCache = RateLimitStatusFormatter.sections(
            diagnostics: diagnostics,
            cacheSummary: cachedCore,
            now: now
        )
        #expect(sectionsWithCache.map(\.title) == ["REST Core", "GraphQL"])
    }

    @Test
    func `sections show endpoint cooldowns next to healthy quota buckets`() throws {
        let now = Date(timeIntervalSinceReferenceDate: 5000)
        let diagnostics = try DiagnosticsSummary(
            apiHost: #require(URL(string: "https://api.github.com")),
            rateLimitReset: nil,
            lastRateLimitError: nil,
            etagEntries: 0,
            backoffEntries: 1,
            endpointCooldowns: [
                EndpointCooldownSummary(
                    endpoint: "commit activity",
                    repository: "openclaw/clawsweeper-state",
                    url: "https://api.github.com/repos/openclaw/clawsweeper-state/stats/commit_activity",
                    retryAfter: now.addingTimeInterval(60)
                )
            ],
            restRateLimit: nil,
            graphQLRateLimit: nil,
            rateLimitResources: RateLimitResourcesSnapshot(
                fetchedAt: now,
                resources: [
                    "graphql": RateLimitSnapshot(
                        resource: "graphql",
                        limit: 5000,
                        remaining: 4898,
                        used: 102,
                        reset: now.addingTimeInterval(1000),
                        fetchedAt: now
                    )
                ]
            )
        )

        let sections = RateLimitStatusFormatter.sections(
            diagnostics: diagnostics,
            cacheSummary: nil,
            now: now
        )

        #expect(sections.map(\.title) == ["GraphQL", "Endpoint Cooldowns"])
        #expect(sections[1].rows == ["openclaw/clawsweeper-state commit activity · retry in 1 min."])
    }

    @Test
    func `compact summary mentions endpoint cooldown first`() throws {
        let now = Date(timeIntervalSinceReferenceDate: 6000)
        let diagnostics = try DiagnosticsSummary(
            apiHost: #require(URL(string: "https://api.github.com")),
            rateLimitReset: nil,
            lastRateLimitError: nil,
            etagEntries: 0,
            backoffEntries: 1,
            endpointCooldowns: [
                EndpointCooldownSummary(
                    endpoint: "commit activity",
                    repository: "openclaw/openclaw",
                    url: "https://api.github.com/repos/openclaw/openclaw/stats/commit_activity",
                    retryAfter: now.addingTimeInterval(30)
                )
            ],
            restRateLimit: RateLimitSnapshot(
                resource: "core",
                limit: 5000,
                remaining: 4400,
                used: 600,
                reset: now.addingTimeInterval(600),
                fetchedAt: now
            ),
            graphQLRateLimit: nil,
            rateLimitResources: nil
        )

        let summary = RateLimitStatusFormatter.compactSummary(
            diagnostics: diagnostics,
            cacheSummary: nil,
            now: now
        )
        let state = RateLimitDisplayState(diagnostics: diagnostics)

        #expect(summary == "Endpoint cooldown · openclaw/openclaw commit activity · retry in 30 sec.")
        #expect(state.isLimited(now: now))
    }
}
