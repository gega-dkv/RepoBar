using RepoBar.Core.Auth;
using RepoBar.Core.Models;
using RepoBar.Core.Storage;

namespace RepoBar.Core.Api;

public static class RepositoryServiceFactory
{
    public static IRepositoryService Create(UserSettings settings, ICredentialStore credentialStore)
    {
        HttpClient httpClient = new();
        return settings.SelectedProvider == SourceControlProvider.GitLab
            ? ConfigureGitLab(new GitLabClient(httpClient), settings, credentialStore)
            : ConfigureGitHub(new GitHubClient(httpClient), settings, credentialStore);
    }

    private static GitHubClient ConfigureGitHub(GitHubClient client, UserSettings settings, ICredentialStore credentialStore)
    {
        Uri host = settings.EnterpriseHost ?? settings.GitHubHost;
        if (settings.EnterpriseHost is not null)
        {
            client.SetApiHost(new Uri(settings.EnterpriseHost, "/api/v3/"));
        }

        CredentialRecord? pat = credentialStore.ReadAsync("provider-token", $"{SourceControlProvider.GitHub}:{host.Host}:pat").GetAwaiter().GetResult();
        if (pat is not null)
        {
            client.SetCredential(new ProviderCredential(SourceControlProvider.GitHub, host, AuthMethod.Pat, CredentialHeaderStyle.AuthorizationBearer, pat.Secret));
            return client;
        }

        OAuthTokens? tokens = new GitHubOAuthLoginService(
                new HttpClient(),
                credentialStore,
                new HttpListenerOAuthCallbackServer(),
                new SystemBrowserLauncher())
            .RefreshIfNeededAsync(host)
            .GetAwaiter()
            .GetResult();
        if (tokens is not null)
        {
            client.SetCredential(new ProviderCredential(SourceControlProvider.GitHub, host, AuthMethod.OAuth, CredentialHeaderStyle.AuthorizationBearer, tokens.AccessToken));
        }

        return client;
    }

    private static GitLabClient ConfigureGitLab(GitLabClient client, UserSettings settings, ICredentialStore credentialStore)
    {
        RepositoryHost host = settings.RepositoryHosts.FirstOrDefault(host => host.Provider == SourceControlProvider.GitLab) ?? RepositoryHost.GitLabCom;
        if (host.ApiBaseUrl is not null)
        {
            client.SetApiHost(host.ApiBaseUrl);
        }

        CredentialRecord? credential = credentialStore.ReadAsync("provider-token", $"{SourceControlProvider.GitLab}:{host.WebBaseUrl.Host}:pat").GetAwaiter().GetResult();
        if (credential is not null)
        {
            client.SetCredential(new ProviderCredential(SourceControlProvider.GitLab, host.WebBaseUrl, AuthMethod.Pat, CredentialHeaderStyle.PrivateToken, credential.Secret));
        }

        return client;
    }
}
