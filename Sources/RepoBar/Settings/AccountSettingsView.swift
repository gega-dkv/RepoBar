import Foundation
import RepoBarCore
import SwiftUI

// swiftlint:disable:next type_body_length
struct AccountSettingsView: View {
    @Bindable var session: Session
    let appState: AppState
    @State private var clientID = "Iv23liGm2arUyotWSjwJ"
    @State private var clientSecret = ""
    @State private var enterpriseHost = ""
    @State private var providerHost = ""
    @State private var providerAPIHost = ""
    @State private var bitbucketEmail = ""
    @State private var selectedProvider: SourceControlProvider = .github
    @State private var hostMode: HostMode = .githubCom
    @State private var authMethod: AuthMethod = .oauth
    @State private var patInput = ""
    @State private var isValidatingPAT = false
    @State private var validationError: String?
    @State private var tokenValidation: TokenValidationState = .unknown
    private let enterpriseFieldMinWidth: CGFloat = 260
    private let spinnerSize: CGFloat = 14
    private let tokenCheckTimeout: TimeInterval = 8
    private let tokenRefreshTimeout: TimeInterval = 12

    var body: some View {
        Form {
            Section("Account") {
                Picker("Provider", selection: self.$selectedProvider) {
                    ForEach(SourceControlProvider.allCases, id: \.self) { provider in
                        Text(provider.label).tag(provider)
                    }
                }
                .pickerStyle(.segmented)

                if self.selectedProvider == .github {
                    Picker("Host", selection: self.$hostMode) {
                        ForEach(HostMode.allCases, id: \.self) { mode in
                            Text(mode.label).tag(mode)
                        }
                    }
                    .pickerStyle(.segmented)
                }

                switch self.session.account {
                case let .loggedIn(user):
                    VStack(alignment: .leading, spacing: 12) {
                        HStack(alignment: .top, spacing: 12) {
                            HStack(spacing: 10) {
                                Image(systemName: "checkmark.seal.fill")
                                    .foregroundStyle(.green)
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Signed in")
                                        .font(.headline)
                                    Text("\(user.username) · \(user.host.host ?? "github.com")")
                                        .font(.subheadline)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            Spacer()
                            Button("Log out") {
                                Task {
                                    await self.appState.logoutCurrentMethod()
                                    self.authMethod = .oauth
                                    self.patInput = ""
                                }
                            }
                            .buttonStyle(.bordered)
                        }
                        if let status = self.tokenStatusText {
                            HStack(spacing: 8) {
                                if self.tokenValidation == .checking {
                                    ProgressView()
                                        .controlSize(.small)
                                        .frame(width: self.spinnerSize, height: self.spinnerSize)
                                }
                                Text(status)
                                    .font(.caption)
                                    .foregroundStyle(self.tokenStatusColor)
                            }
                        }
                        HStack(spacing: 8) {
                            Button("Check token") {
                                Task { await self.validateToken() }
                            }
                            .disabled(self.tokenValidation == .checking)
                            if self.session.settings.authMethod == .oauth {
                                Button("Refresh token") {
                                    Task { await self.refreshToken() }
                                }
                                .disabled(self.tokenValidation == .checking)
                            }
                        }
                        .buttonStyle(.bordered)
                        if self.session.settings.authMethod == .oauth, self.session.settings.enterpriseHost == nil {
                            Text("Private organization repositories are visible only after the RepoBar GitHub App is installed on that organization or selected repository.")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            Link("Manage GitHub App installation", destination: RepoBarAuthDefaults.appInstallURL)
                                .font(.caption)
                        }
                    }
                    .padding(.vertical, 4)
                default:
                    if self.selectedProvider == .github {
                        self.githubLoginFields
                    } else {
                        self.providerLoginFields
                    }
                }
            }

            if let validationError {
                Text(validationError)
                    .font(.caption)
                    .foregroundStyle(.red)
            }

            if let lastError = self.session.lastError, validationError == nil {
                Text(lastError)
                    .font(.caption)
                    .foregroundStyle(.red)
            }
        }
        .formStyle(.grouped)
        .padding(.horizontal, 20)
        .padding(.vertical, 16)
        .onAppear {
            self.selectedProvider = self.session.settings.selectedProvider
            if let enterprise = self.session.settings.enterpriseHost {
                self.enterpriseHost = enterprise.absoluteString
                self.hostMode = .enterprise
            }
            if self.session.settings.enterpriseHost == nil {
                self.hostMode = .githubCom
                if self.clientID.isEmpty {
                    self.clientID = RepoBarAuthDefaults.clientID
                }
                if self.clientSecret.isEmpty {
                    self.clientSecret = RepoBarAuthDefaults.clientSecret
                }
            }
            self.authMethod = self.session.settings.authMethod
            self.loadProviderFields()
        }
        .onChange(of: self.selectedProvider) { _, provider in
            self.applyProviderDefaults(provider)
        }
        .task(id: self.session.account) {
            guard case .loggedIn = self.session.account else {
                self.tokenValidation = .unknown
                return
            }

            await self.validateToken()
        }
    }

    @ViewBuilder
    private var githubLoginFields: some View {
        Picker("Authentication", selection: self.$authMethod) {
            ForEach(AuthMethod.allCases, id: \.self) { method in
                Text(method.label).tag(method)
            }
        }
        .pickerStyle(.segmented)

        if self.authMethod == .pat {
            LabeledContent("Token") {
                SecureField("ghp_...", text: self.$patInput)
                    .frame(minWidth: self.enterpriseFieldMinWidth)
                    .layoutPriority(1)
            }
            Text("Recommended for SAML SSO organizations. Required scopes: repo, read:org")
                .font(.caption)
                .foregroundStyle(.secondary)
            Link("Create a token on GitHub", destination: self.createTokenURL())
                .font(.caption)
            if self.hostMode == .enterprise {
                LabeledContent("Enterprise Base URL") {
                    TextField("https://ghe.example.com", text: self.$enterpriseHost)
                        .frame(minWidth: self.enterpriseFieldMinWidth)
                        .layoutPriority(1)
                }
            }
            HStack(spacing: 8) {
                if self.isValidatingPAT {
                    ProgressView()
                        .controlSize(.small)
                        .frame(width: self.spinnerSize, height: self.spinnerSize)
                }
                Button(self.isValidatingPAT ? "Signing in..." : "Sign in with Token") {
                    self.loginWithPAT()
                }
                .disabled(self.patInput.isEmpty || self.isValidatingPAT)
                .buttonStyle(.borderedProminent)
            }
        } else {
            if self.hostMode == .enterprise {
                LabeledContent("Enterprise Base URL") {
                    TextField("https://ghe.example.com", text: self.$enterpriseHost)
                        .frame(minWidth: self.enterpriseFieldMinWidth)
                        .layoutPriority(1)
                }
                LabeledContent("Client ID") {
                    TextField("", text: self.$clientID)
                        .frame(minWidth: self.enterpriseFieldMinWidth)
                        .layoutPriority(1)
                }
                LabeledContent("Client Secret") {
                    SecureField("", text: self.$clientSecret)
                        .frame(minWidth: self.enterpriseFieldMinWidth)
                        .layoutPriority(1)
                }
                Text("Create an OAuth App in your enterprise server. Callback URL: http://127.0.0.1:53682/callback")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                Text("Uses the built-in GitHub.com OAuth app.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text("Private organization repositories require the RepoBar GitHub App installation to include that organization or repository.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Link("Install RepoBar GitHub App", destination: RepoBarAuthDefaults.appInstallURL)
                    .font(.caption)
            }
            HStack(spacing: 8) {
                if self.session.account == .loggingIn {
                    ProgressView()
                        .controlSize(.small)
                        .frame(width: self.spinnerSize, height: self.spinnerSize)
                }
                Button(self.session.account == .loggingIn ? "Signing in..." : self.hostMode == .enterprise ? "Sign in to Enterprise" : "Sign in to GitHub.com") {
                    self.login()
                }
                .disabled(self.session.account == .loggingIn)
                .buttonStyle(.borderedProminent)
            }
            Text("Uses browser-based OAuth. Tokens are stored by RepoBar's configured auth store.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    @ViewBuilder
    private var providerLoginFields: some View {
        LabeledContent("Web Base URL") {
            TextField(self.defaultHostPlaceholder(for: self.selectedProvider), text: self.$providerHost)
                .frame(minWidth: self.enterpriseFieldMinWidth)
                .layoutPriority(1)
        }

        if self.selectedProvider != .customGit {
            LabeledContent("API Base URL") {
                TextField(self.defaultAPIPlaceholder(for: self.selectedProvider), text: self.$providerAPIHost)
                    .frame(minWidth: self.enterpriseFieldMinWidth)
                    .layoutPriority(1)
            }
        }

        if self.selectedProvider == .bitbucketCloud {
            LabeledContent("Atlassian Email") {
                TextField("you@example.com", text: self.$bitbucketEmail)
                    .frame(minWidth: self.enterpriseFieldMinWidth)
                    .layoutPriority(1)
            }
        }

        if self.selectedProvider == .gitlab || self.selectedProvider == .bitbucketCloud || self.selectedProvider == .forgejo || self.selectedProvider == .gitea {
            LabeledContent(self.selectedProvider == .bitbucketCloud ? "API Token" : "Token") {
                SecureField(self.tokenPlaceholder(for: self.selectedProvider), text: self.$patInput)
                    .frame(minWidth: self.enterpriseFieldMinWidth)
                    .layoutPriority(1)
            }
            Text(self.providerHelpText(for: self.selectedProvider))
                .font(.caption)
                .foregroundStyle(.secondary)
            if let tokenURL = self.providerTokenURL(for: self.selectedProvider) {
                Link(self.providerTokenLinkTitle(for: self.selectedProvider), destination: tokenURL)
                    .font(.caption)
            }
            HStack(spacing: 8) {
                if self.isValidatingPAT {
                    ProgressView()
                        .controlSize(.small)
                        .frame(width: self.spinnerSize, height: self.spinnerSize)
                }
                Button(self.isValidatingPAT ? "Signing in..." : "Sign in to \(self.selectedProvider.label)") {
                    self.loginWithProviderToken()
                }
                .disabled(!self.canSubmitProviderToken || self.isValidatingPAT)
                .buttonStyle(.borderedProminent)
            }
        } else {
            Text(self.providerHelpText(for: self.selectedProvider))
                .font(.caption)
                .foregroundStyle(.secondary)
            Button("Save \(self.selectedProvider.label) Host") {
                self.saveProviderHost()
            }
            .disabled(self.normalizedProviderHost() == nil)
            .buttonStyle(.borderedProminent)
        }
    }

    private func login() {
        Task { @MainActor in
            self.session.account = .loggingIn
            self.session.lastError = nil
            let enterpriseURL = self.hostMode == .enterprise ? self.normalizedEnterpriseHost() : nil

            if self.hostMode == .enterprise, let enterpriseURL {
                self.session.settings.selectedProvider = .github
                await self.appState.github.setProvider(.github)
                self.session.settings.enterpriseHost = enterpriseURL
                await self.appState.github.setAPIHost(enterpriseURL.appending(path: "/api/v3"))
                self.session.settings.githubHost = enterpriseURL
                self.validationError = nil
            } else {
                if self.hostMode == .enterprise {
                    self.validationError = "Enterprise Base URL must be a valid https:// URL with a trusted certificate."
                    self.session.account = .loggedOut
                    return
                }
                self.session.settings.selectedProvider = .github
                await self.appState.github.setProvider(.github)
                await self.appState.github.setAPIHost(URL(string: "https://api.github.com")!)
                self.session.settings.githubHost = URL(string: "https://github.com")!
                self.session.settings.enterpriseHost = nil
                self.validationError = nil
            }
            let usingEnterprise = self.session.settings.enterpriseHost != nil
            let effectiveClientID = self.clientID.isEmpty && !usingEnterprise
                ? RepoBarAuthDefaults.clientID
                : self.clientID
            let effectiveClientSecret = self.clientSecret.isEmpty && !usingEnterprise
                ? RepoBarAuthDefaults.clientSecret
                : self.clientSecret
            if usingEnterprise, effectiveClientID.isEmpty || effectiveClientSecret.isEmpty {
                self.validationError = "Client ID and Client Secret are required for enterprise login."
                self.session.account = .loggedOut
                return
            }
            do {
                try await self.appState.auth.login(
                    clientID: effectiveClientID,
                    clientSecret: effectiveClientSecret,
                    host: self.session.settings.enterpriseHost ?? self.session.settings.githubHost,
                    loopbackPort: self.session.settings.loopbackPort,
                    scope: usingEnterprise ? "repo read:org" : nil
                )
                self.session.settings.authMethod = .oauth
                self.appState.persistSettings()
                self.session.hasStoredTokens = true
                if let user = try? await appState.github.currentUser() {
                    self.session.account = .loggedIn(user)
                    self.session.lastError = nil
                } else {
                    self.session.account = .loggedIn(UserIdentity(username: "", host: self.session.settings.githubHost))
                }
                await self.appState.refresh()
            } catch {
                self.session.account = .loggedOut
                self.session.lastError = error.userFacingMessage
            }
        }
    }

    private func loginWithPAT() {
        Task { @MainActor in
            self.isValidatingPAT = true
            self.validationError = nil

            let host: URL
            if self.hostMode == .enterprise {
                guard let enterpriseURL = self.normalizedEnterpriseHost() else {
                    self.validationError = "Enterprise Base URL must be a valid https:// URL with a trusted certificate."
                    self.isValidatingPAT = false
                    return
                }

                self.session.settings.enterpriseHost = enterpriseURL
                host = enterpriseURL
            } else {
                self.session.settings.enterpriseHost = nil
                host = URL(string: "https://github.com")!
            }

            await self.appState.loginWithPAT(self.patInput, host: host)
            self.isValidatingPAT = false

            if case .loggedIn = self.session.account {
                self.patInput = ""
            }
        }
    }

    private func loginWithProviderToken() {
        Task { @MainActor in
            guard let host = self.normalizedProviderHost() else {
                self.validationError = "Web Base URL must be a valid https:// URL."
                return
            }

            let apiHost = self.normalizedProviderAPIHost(for: self.selectedProvider, webHost: host)
            if self.selectedProvider != .customGit, apiHost == nil {
                self.validationError = "API Base URL must be a valid https:// URL."
                return
            }

            self.isValidatingPAT = true
            self.validationError = nil
            await self.appState.loginWithProviderCredential(
                provider: self.selectedProvider,
                token: self.patInput,
                username: self.bitbucketEmail,
                host: host,
                apiHost: apiHost
            )
            self.isValidatingPAT = false
            if case .loggedIn = self.session.account {
                self.patInput = ""
            }
        }
    }

    private func saveProviderHost() {
        Task { @MainActor in
            guard let host = self.normalizedProviderHost() else {
                self.validationError = "Web Base URL must be a valid https:// URL."
                return
            }

            let apiHost = self.normalizedProviderAPIHost(for: self.selectedProvider, webHost: host)
            self.validationError = nil
            await self.appState.saveProviderHost(
                provider: self.selectedProvider,
                host: host,
                apiHost: apiHost,
                authMethod: self.selectedProvider == .customGit ? .pat : self.authMethod
            )
        }
    }

    private var canSubmitProviderToken: Bool {
        if self.patInput.isEmpty { return false }
        if self.normalizedProviderHost() == nil { return false }
        if self.selectedProvider == .bitbucketCloud, self.bitbucketEmail.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return false
        }
        return self.selectedProvider == .gitlab
            || self.selectedProvider == .bitbucketCloud
            || self.selectedProvider == .forgejo
            || self.selectedProvider == .gitea
    }

    private func createTokenURL() -> URL {
        let baseHost = self.hostMode == .enterprise
            ? (self.normalizedEnterpriseHost()?.absoluteString ?? "https://github.com")
            : "https://github.com"
        return URL(string: "\(baseHost)/settings/tokens/new?scopes=repo,read:org&description=RepoBar")!
    }

    private func normalizedEnterpriseHost() -> URL? {
        guard !self.enterpriseHost.isEmpty else { return nil }
        guard var components = URLComponents(string: enterpriseHost) else { return nil }

        if components.scheme == nil { components.scheme = "https" }
        guard components.scheme?.lowercased() == "https", components.host != nil else { return nil }

        components.path = ""
        components.query = nil
        components.fragment = nil
        return components.url
    }

    private func normalizedProviderHost() -> URL? {
        self.normalizedURL(self.providerHost)
    }

    private func normalizedProviderAPIHost(for provider: SourceControlProvider, webHost: URL) -> URL? {
        if self.providerAPIHost.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false {
            return self.normalizedURL(self.providerAPIHost)
        }
        switch provider {
        case .gitlab:
            return webHost.appending(path: "api/v4")
        case .bitbucketCloud:
            return RepositoryHost.bitbucketCloud.apiBaseURL
        case .forgejo:
            return webHost.appending(path: "api/v1")
        case .gitea:
            return webHost.appending(path: "api/v1")
        case .github:
            return webHost.host?.lowercased() == "github.com" ? RepoBarAuthDefaults.apiHost : webHost.appending(path: "api/v3")
        case .customGit:
            return nil
        }
    }

    private func normalizedURL(_ value: String) -> URL? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, var components = URLComponents(string: trimmed) else { return nil }

        if components.scheme == nil { components.scheme = "https" }
        guard components.scheme?.lowercased() == "https", components.host != nil else { return nil }

        components.query = nil
        components.fragment = nil
        return components.url
    }

    private func loadProviderFields() {
        let provider = self.session.settings.selectedProvider
        let host = self.session.settings.repositoryHosts.first(where: { $0.provider == provider })
        switch provider {
        case .github:
            break
        case .gitlab:
            self.providerHost = host?.webBaseURL.absoluteString ?? RepositoryHost.gitlabCom.webBaseURL.absoluteString
            self.providerAPIHost = host?.apiBaseURL?.absoluteString ?? RepositoryHost.gitlabCom.apiBaseURL?.absoluteString ?? ""
        case .bitbucketCloud:
            self.providerHost = host?.webBaseURL.absoluteString ?? RepositoryHost.bitbucketCloud.webBaseURL.absoluteString
            self.providerAPIHost = host?.apiBaseURL?.absoluteString ?? RepositoryHost.bitbucketCloud.apiBaseURL?.absoluteString ?? ""
        case .forgejo:
            self.providerHost = host?.webBaseURL.absoluteString ?? RepositoryHost.codeberg.webBaseURL.absoluteString
            self.providerAPIHost = host?.apiBaseURL?.absoluteString ?? RepositoryHost.codeberg.apiBaseURL?.absoluteString ?? ""
        case .gitea, .customGit:
            self.providerHost = host?.webBaseURL.absoluteString ?? ""
            self.providerAPIHost = host?.apiBaseURL?.absoluteString ?? ""
        }
    }

    private func applyProviderDefaults(_ provider: SourceControlProvider) {
        self.session.settings.selectedProvider = provider
        self.authMethod = switch provider {
        case .github:
            self.session.settings.authMethod == .apiToken ? .oauth : self.session.settings.authMethod
        case .bitbucketCloud:
            .apiToken
        case .gitlab, .forgejo, .gitea, .customGit:
            .pat
        }
        self.session.settings.authMethod = self.authMethod
        self.loadProviderFields()
        self.validationError = nil
    }

    private func defaultHostPlaceholder(for provider: SourceControlProvider) -> String {
        switch provider {
        case .github:
            "https://github.com"
        case .gitlab:
            "https://gitlab.com"
        case .bitbucketCloud:
            "https://bitbucket.org"
        case .forgejo:
            "https://codeberg.org"
        case .gitea:
            "https://gitea.example.com"
        case .customGit:
            "https://git.example.com"
        }
    }

    private func defaultAPIPlaceholder(for provider: SourceControlProvider) -> String {
        switch provider {
        case .github:
            "https://api.github.com"
        case .gitlab:
            "https://gitlab.com/api/v4"
        case .bitbucketCloud:
            "https://api.bitbucket.org/2.0"
        case .forgejo:
            "https://codeberg.org/api/v1"
        case .gitea:
            "https://gitea.example.com/api/v1"
        case .customGit:
            ""
        }
    }

    private func tokenPlaceholder(for provider: SourceControlProvider) -> String {
        switch provider {
        case .gitlab:
            "glpat-..."
        case .bitbucketCloud:
            "API token"
        case .forgejo, .gitea:
            "access token"
        case .github, .customGit:
            "token"
        }
    }

    private func providerTokenURL(for provider: SourceControlProvider) -> URL? {
        switch provider {
        case .gitlab:
            let base = self.normalizedProviderHost()?.absoluteString ?? "https://gitlab.com"
            return URL(string: "\(base)/-/user_settings/personal_access_tokens")
        case .bitbucketCloud:
            return URL(string: "https://id.atlassian.com/manage-profile/security/api-tokens")
        case .forgejo:
            return URL(string: "https://forgejo.org/docs/latest/user/token-scope/")
        case .gitea:
            return URL(string: "https://docs.gitea.com/usage/profile-readme#applications")
        case .github, .customGit:
            return nil
        }
    }

    private func providerTokenLinkTitle(for provider: SourceControlProvider) -> String {
        switch provider {
        case .gitlab:
            "Create a GitLab token"
        case .bitbucketCloud:
            "Create an Atlassian API token"
        case .forgejo:
            "Forgejo token documentation"
        case .gitea:
            "Gitea token documentation"
        case .github, .customGit:
            "Token documentation"
        }
    }

    private func providerHelpText(for provider: SourceControlProvider) -> String {
        switch provider {
        case .gitlab:
            "Use a GitLab personal access token with read_user, read_api, and read_repository scopes."
        case .bitbucketCloud:
            "Use your Atlassian email with a Bitbucket API token. RepoBar stores it as Basic auth credentials."
        case .forgejo:
            "Use a Forgejo access token. API requests use the documented Authorization: token header."
        case .gitea:
            "Use a Gitea access token. API requests use the documented Authorization: token header."
        case .customGit:
            "Custom Git hosts are local-only for now. RepoBar can save the host for remote URL matching and browser links."
        case .github:
            ""
        }
    }

    private func validateToken() async {
        guard case .loggedIn = self.session.account else { return }

        if self.tokenValidation == .checking { return }
        self.tokenValidation = .checking
        let started = Date()
        await self.logAuth("Auth: token check started")
        do {
            let user = try await self.withTimeout(seconds: self.tokenCheckTimeout) {
                try await self.appState.github.currentUser()
            }
            self.session.account = .loggedIn(user)
            self.session.lastError = nil
            self.tokenValidation = .valid
            await self.logAuth("Auth: token check ok in \(Self.formatElapsed(since: started))")
        } catch {
            if error.isAuthenticationFailure {
                self.tokenValidation = .invalid("Authentication required.")
                await self.logAuth("Auth: token check auth failure in \(Self.formatElapsed(since: started))")
                await self.appState.handleAuthenticationFailure(error)
                return
            }
            self.tokenValidation = .invalid(error.userFacingMessage)
            await self.logAuth("Auth: token check failed in \(Self.formatElapsed(since: started)): \(error.userFacingMessage)")
        }
    }

    private func refreshToken() async {
        guard case .loggedIn = self.session.account else { return }

        if self.tokenValidation == .checking { return }
        self.tokenValidation = .checking
        let started = Date()
        await self.logAuth("Auth: token refresh started")
        do {
            let host = self.session.settings.enterpriseHost ?? self.session.settings.githubHost
            let refreshed = try await self.withTimeout(seconds: self.tokenRefreshTimeout) {
                try await self.appState.auth.refreshIfNeeded(host: host, force: true)
            }
            guard refreshed != nil else {
                throw URLError(.userAuthenticationRequired)
            }

            await self.logAuth("Auth: token refresh ok in \(Self.formatElapsed(since: started))")
            await self.validateToken()
        } catch {
            if error.isAuthenticationFailure {
                self.tokenValidation = .invalid("Authentication required.")
                await self.logAuth("Auth: token refresh auth failure in \(Self.formatElapsed(since: started))")
                await self.appState.handleAuthenticationFailure(error)
                return
            }
            self.tokenValidation = .invalid(error.userFacingMessage)
            await self.logAuth("Auth: token refresh failed in \(Self.formatElapsed(since: started)): \(error.userFacingMessage)")
        }
    }

    private var tokenStatusText: String? {
        switch self.tokenValidation {
        case .unknown:
            "Token status not checked yet."
        case .checking:
            "Checking token…"
        case .valid:
            "Token is valid."
        case let .invalid(message):
            "Token invalid: \(message)"
        }
    }

    private var tokenStatusColor: Color {
        switch self.tokenValidation {
        case .valid:
            .green
        case .invalid:
            .red
        default:
            .secondary
        }
    }

    private func logAuth(_ message: String) async {
        await DiagnosticsLogger.shared.message(message)
    }

    private func withTimeout<T: Sendable>(
        seconds: TimeInterval,
        operation: @escaping @Sendable () async throws -> T
    ) async throws -> T {
        try await withThrowingTaskGroup(of: T.self) { group in
            group.addTask { try await operation() }
            group.addTask {
                try await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
                throw URLError(.timedOut)
            }
            let result = try await group.next()!
            group.cancelAll()
            return result
        }
    }

    private static func formatElapsed(since start: Date) -> String {
        let elapsed = Date().timeIntervalSince(start)
        return String(format: "%.2fs", elapsed)
    }
}

private enum TokenValidationState: Equatable {
    case unknown
    case checking
    case valid
    case invalid(String)
}

private enum HostMode: String, CaseIterable {
    case githubCom
    case enterprise

    var label: String {
        switch self {
        case .githubCom:
            "GitHub.com"
        case .enterprise:
            "Enterprise"
        }
    }
}
