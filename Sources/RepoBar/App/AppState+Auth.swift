import Foundation
import RepoBarCore

extension AppState {
    /// Starts the OAuth flow using the default GitHub App credentials, invoked from the logged-out prompt.
    func quickLogin() async {
        self.session.account = .loggingIn
        self.session.settings.loopbackPort = self.defaultLoopbackPort
        self.session.settings.selectedProvider = .github
        await self.github.setProvider(.github)
        await self.github.setAPIHost(self.defaultAPIHost)
        self.session.settings.githubHost = self.defaultGitHubHost
        self.session.settings.enterpriseHost = nil
        self.session.settings.authMethod = .oauth
        self.persistSettings()

        do {
            try await self.auth.login(
                clientID: self.defaultClientID,
                clientSecret: self.defaultClientSecret,
                host: self.defaultGitHubHost,
                loopbackPort: self.defaultLoopbackPort
            )
            self.session.hasStoredTokens = true
            if let user = try? await self.github.currentUser() {
                self.session.account = .loggedIn(user)
                self.session.lastError = nil
            } else {
                self.session.account = .loggedIn(UserIdentity(username: "", host: self.defaultGitHubHost))
            }
            await self.refresh()
        } catch {
            self.session.account = .loggedOut
            self.session.lastError = error.userFacingMessage
        }
    }

    /// Authenticates with a Personal Access Token.
    func loginWithPAT(_ pat: String, host: URL) async {
        self.session.account = .loggingIn
        self.session.lastError = nil
        self.session.settings.selectedProvider = .github
        let apiHost = host.host == "github.com"
            ? URL(string: "https://api.github.com")!
            : host.appendingPathComponent("api/v3")
        await self.github.setAPIHost(apiHost)
        self.session.settings.githubHost = host
        if host.host?.lowercased() == "github.com" {
            self.session.settings.enterpriseHost = nil
        } else {
            self.session.settings.enterpriseHost = host
        }

        do {
            let user = try await self.patAuth.authenticate(pat: pat, host: host)
            self.session.settings.authMethod = .pat
            self.session.hasStoredTokens = true
            self.session.account = .loggedIn(user)
            self.session.lastError = nil
            self.persistSettings()
            await self.refresh()
        } catch {
            self.session.account = .loggedOut
            self.session.settings.authMethod = .oauth
            self.persistSettings()
            self.session.lastError = error.localizedDescription
        }
    }

    func loginWithProviderCredential(
        provider: SourceControlProvider,
        token: String,
        username: String?,
        host: URL,
        apiHost: URL?
    ) async {
        self.session.account = .loggingIn
        self.session.lastError = nil
        self.session.settings.selectedProvider = provider
        self.session.settings.authMethod = provider == .bitbucketCloud ? .apiToken : .pat
        self.upsertRepositoryHost(provider: provider, host: host, apiHost: apiHost)
        await self.github.setProvider(provider)
        if let apiHost {
            await self.github.setAPIHost(apiHost)
        }

        do {
            let authenticator = ProviderTokenAuthenticator()
            let user: UserIdentity = switch provider {
            case .gitlab:
                try await authenticator.authenticatePAT(provider: .gitlab, token: token, host: host)
            case .bitbucketCloud:
                try await authenticator.authenticateBitbucketAPIToken(email: username ?? "", token: token, host: host)
            case .github:
                try await self.patAuth.authenticate(pat: token, host: host)
            case .forgejo, .gitea:
                try await authenticator.authenticatePAT(provider: provider, token: token, host: host)
            case .customGit:
                throw ProviderTokenAuthError.unsupportedProvider(provider)
            }
            self.session.hasStoredTokens = true
            self.session.account = .loggedIn(user)
            self.session.lastError = nil
            self.persistSettings()
            await self.refresh()
        } catch {
            self.session.account = .loggedOut
            self.session.lastError = error.localizedDescription
            self.persistSettings()
        }
    }

    func saveProviderHost(
        provider: SourceControlProvider,
        host: URL,
        apiHost: URL?,
        authMethod: AuthMethod
    ) async {
        self.session.settings.selectedProvider = provider
        self.session.settings.authMethod = authMethod
        self.upsertRepositoryHost(provider: provider, host: host, apiHost: apiHost)
        await self.github.setProvider(provider)
        if let apiHost {
            await self.github.setAPIHost(apiHost)
        }
        self.persistSettings()
    }

    /// Logs out the current user, clearing tokens based on the current auth method.
    func logoutCurrentMethod() async {
        let provider = self.session.settings.selectedProvider
        let host = Self.credentialHost(from: self.session.settings)
        switch provider {
        case .github:
            await self.auth.logout(host: host)
            await self.patAuth.logout(host: host)
        case .gitlab, .forgejo, .gitea:
            TokenStore.shared.clearCredential(provider: provider, host: host, kind: .pat)
            TokenStore.shared.clearCredential(provider: provider, host: host, kind: .oauth)
        case .bitbucketCloud:
            TokenStore.shared.clearCredential(provider: .bitbucketCloud, host: host, kind: .apiToken)
        case .customGit:
            break
        }
        self.session.account = .loggedOut
        self.session.hasStoredTokens = false
        self.session.settings.authMethod = provider == .bitbucketCloud ? .apiToken : (provider == .github ? .oauth : .pat)
        self.persistSettings()
    }

    private func upsertRepositoryHost(provider: SourceControlProvider, host: URL, apiHost: URL?) {
        let authMethod = provider == .bitbucketCloud ? AuthMethod.apiToken : self.session.settings.authMethod
        let entry = RepositoryHost(
            provider: provider,
            displayName: host.host ?? provider.label,
            webBaseURL: host,
            apiBaseURL: apiHost,
            authMethod: authMethod
        )
        self.session.settings.repositoryHosts.removeAll { $0.provider == provider && $0.webBaseURL.host == host.host }
        self.session.settings.repositoryHosts.insert(entry, at: 0)

        if provider == .github {
            self.session.settings.githubHost = host
            self.session.settings.enterpriseHost = host.host?.lowercased() == "github.com" ? nil : host
        }
    }
}
