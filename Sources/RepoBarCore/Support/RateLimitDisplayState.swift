import Foundation

public struct RateLimitDisplayState: Sendable {
    public let diagnostics: DiagnosticsSummary
    public let cacheSummary: RepoBarCacheSummary?

    public init(
        diagnostics: DiagnosticsSummary,
        cacheSummary: RepoBarCacheSummary? = nil
    ) {
        self.diagnostics = diagnostics
        self.cacheSummary = cacheSummary
    }

    public var juice: RateLimitJuice {
        RateLimitJuice(
            diagnostics: self.diagnostics,
            cacheSummary: self.cacheSummary
        )
    }

    public func compactSummary(now: Date = Date()) -> String {
        RateLimitStatusFormatter.compactSummary(
            diagnostics: self.diagnostics,
            cacheSummary: self.cacheSummary,
            now: now
        )
    }

    public func sections(now: Date = Date()) -> [RateLimitDisplaySection] {
        RateLimitStatusFormatter.sections(
            diagnostics: self.diagnostics,
            cacheSummary: self.cacheSummary,
            now: now
        )
    }

    public func isLimited(now: Date = Date()) -> Bool {
        if self.diagnostics.rateLimitReset.map({ $0 > now }) ?? false {
            return true
        }
        if self.diagnostics.endpointCooldowns.contains(where: { $0.retryAfter > now }) {
            return true
        }
        return self.cacheSummary?.rateLimits.contains { $0.resetAt > now } ?? false
    }
}
