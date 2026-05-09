import AppKit
@testable import RepoBar
import RepoBarCore
import Testing

struct StatusBarMenuManagerTests {
    @MainActor
    @Test
    func `main status item uses native AppKit menu`() throws {
        let manager = StatusBarMenuManager(appState: AppState(), statusBar: NSStatusBar())

        manager.ensureStatusItems()

        let item = try #require(manager.statusItem)
        let button = try #require(item.button)
        #expect(item.menu != nil)
        #expect(item.autosaveName == "repobar-main")
        #expect(button.isEnabled)
        #expect(button.target == nil)
        #expect(button.action == nil)
        #expect(!self.containsHostingView(button))
    }

    @MainActor
    @Test
    func `GitHub reference status item is collapsed between matches`() throws {
        let appState = AppState()
        let manager = StatusBarMenuManager(appState: appState, statusBar: NSStatusBar())
        appState.session.gitHubReferenceMatch = try self.makeMatch()

        manager.syncGitHubReferenceStatusItemForTesting()

        let item = try #require(manager.gitHubReferenceStatusItemForTesting())
        let menu = try #require(manager.gitHubReferenceMenuForTesting())
        #expect(item.isVisible)
        #expect(item.menu === menu)
        #expect(item.autosaveName == "repobar-github-reference")
        let button = try #require(item.button)
        #expect(button.isEnabled)
        #expect(button.target == nil)
        #expect(button.action == nil)
        #expect(!self.containsHostingView(button))
        #expect(menu.items.contains { $0.title == "Open #42 in Browser" })
        #expect(menu.items.contains { $0.view is MenuItemHostingView })
        #expect(button.title.contains("#42 Open owner/repo"))

        appState.session.gitHubReferenceMatch = nil
        manager.syncGitHubReferenceStatusItemForTesting()

        #expect(manager.gitHubReferenceStatusItemForTesting() === item)
        #expect(manager.gitHubReferenceMenuForTesting() == nil)
        #expect(item.isVisible)
        #expect(item.length == 0)
        #expect(item.menu == nil)
        #expect(button.isHidden)
        #expect(!button.isEnabled)
    }

    private func makeMatch() throws -> GitHubReferenceMatch {
        try GitHubReferenceMatch(
            query: .issueNumber(42),
            title: "Fix the menu click path when the watcher is enabled",
            url: #require(URL(string: "https://github.com/owner/repo/issues/42")),
            repositoryFullName: "owner/repo",
            kind: .issue,
            state: .open,
            createdAt: Date(timeIntervalSinceReferenceDate: 10),
            updatedAt: Date(timeIntervalSinceReferenceDate: 20)
        )
    }

    @MainActor
    private func containsHostingView(_ view: NSView?) -> Bool {
        guard let view else { return false }

        if String(describing: type(of: view)).contains("HostingView") {
            return true
        }
        return view.subviews.contains { self.containsHostingView($0) }
    }
}
