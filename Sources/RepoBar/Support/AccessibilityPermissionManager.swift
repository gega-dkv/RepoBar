import AppKit
import ApplicationServices
import Observation

@MainActor
@Observable
final class AccessibilityPermissionManager {
    private(set) var isTrusted: Bool

    init() {
        self.isTrusted = AXIsProcessTrusted()
    }

    @discardableResult
    func refresh() -> Bool {
        let trusted = AXIsProcessTrusted()
        guard trusted != self.isTrusted else { return false }

        self.isTrusted = trusted
        return true
    }

    func requestPrompt() {
        let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(600))
            self?.openSystemSettings()
        }
        self.scheduleRefresh()
    }

    func openSystemSettings() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") else { return }

        NSWorkspace.shared.open(url)
    }

    private func scheduleRefresh() {
        Task { [weak self] in
            try? await Task.sleep(for: .seconds(1))
            _ = self?.refresh()
        }
    }
}
