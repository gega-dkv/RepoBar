@testable import RepoBar
@testable import RepoBarCore
import Testing

@MainActor
struct KeyboardIssueMonitorTests {
    @Test
    func `bare numbers and issue prefixes become issue queries`() {
        #expect(KeyboardIssueMonitor.query(from: "73655") == .issueNumber(73655))
        #expect(KeyboardIssueMonitor.query(from: "7") == .issueNumber(7))
        #expect(KeyboardIssueMonitor.query(from: "#7") == .issueNumber(7))
        #expect(KeyboardIssueMonitor.query(from: "gh-42") == .issueNumber(42))
        #expect(KeyboardIssueMonitor.query(from: "a73655") == nil)
    }

    @Test
    func `commit hashes become commit queries`() {
        #expect(KeyboardIssueMonitor.query(from: "ffd212ca43") == .commitHash("ffd212ca43"))
        #expect(KeyboardIssueMonitor.query(from: "1234567") == .issueNumber(1_234_567))
        #expect(KeyboardIssueMonitor.query(from: "abcdef") == nil)
    }

    @Test
    func `github issue and pr urls become repository scoped issue queries`() {
        #expect(
            KeyboardIssueMonitor.query(from: "https://github.com/openclaw/openclaw/issues/73655") ==
                .repositoryIssueNumber(repositoryFullName: "openclaw/openclaw", number: 73655)
        )
        #expect(
            KeyboardIssueMonitor.query(from: "https://github.com/openclaw/openclaw/pull/123") ==
                .repositoryIssueNumber(repositoryFullName: "openclaw/openclaw", number: 123)
        )
    }

    @Test
    func `github commit urls become repository scoped commit queries`() {
        #expect(
            KeyboardIssueMonitor.query(from: "https://github.com/openclaw/openclaw/commit/ffd212ca43abcdef") ==
                .repositoryCommitHash(repositoryFullName: "openclaw/openclaw", hash: "ffd212ca43abcdef")
        )
        #expect(
            KeyboardIssueMonitor.query(from: "https://github.com/openclaw/openclaw/commits/ffd212ca43") ==
                .repositoryCommitHash(repositoryFullName: "openclaw/openclaw", hash: "ffd212ca43")
        )
    }
}
