import AppKit
import RepoBarCore
import WebKit

@MainActor
final class GitHubReferenceBrowserMenuItemView: NSView {
    private enum Metrics {
        static let width: CGFloat = 740
        static let height: CGFloat = 680
    }

    private let url: URL
    private let webView: WKWebView?
    private var hasLoaded = false

    override var intrinsicContentSize: NSSize {
        NSSize(width: Metrics.width, height: Metrics.height)
    }

    init(match: GitHubReferenceMatch) {
        self.url = match.url
        if Self.shouldCreateWebView {
            let configuration = WKWebViewConfiguration()
            configuration.websiteDataStore = .default()
            configuration.preferences.javaScriptCanOpenWindowsAutomatically = false
            self.webView = WKWebView(frame: .zero, configuration: configuration)
        } else {
            self.webView = nil
        }
        super.init(frame: NSRect(origin: .zero, size: NSSize(width: Metrics.width, height: Metrics.height)))
        self.configureView()
    }

    @available(*, unavailable)
    required init?(coder _: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        guard self.window != nil else { return }

        self.loadIfNeeded()
    }

    func preload() {
        self.loadIfNeeded()
    }

    private func configureView() {
        guard let webView = self.webView else { return }

        webView.translatesAutoresizingMaskIntoConstraints = false
        webView.allowsBackForwardNavigationGestures = false
        self.addSubview(webView)

        NSLayoutConstraint.activate([
            webView.leadingAnchor.constraint(equalTo: self.leadingAnchor),
            webView.trailingAnchor.constraint(equalTo: self.trailingAnchor),
            webView.topAnchor.constraint(equalTo: self.topAnchor),
            webView.bottomAnchor.constraint(equalTo: self.bottomAnchor)
        ])
    }

    private func loadIfNeeded() {
        guard !self.hasLoaded, let webView = self.webView else { return }

        self.hasLoaded = true
        webView.load(URLRequest(url: self.url))
    }

    private static var shouldCreateWebView: Bool {
        ProcessInfo.processInfo.environment["CI"] != "true" &&
            ProcessInfo.processInfo.environment["XCTestConfigurationFilePath"] == nil
    }
}
