namespace RepoBar.Core.Models;

public enum SourceControlProvider
{
    GitHub,
    GitLab,
    BitbucketCloud,
    Forgejo,
    Gitea,
    CustomGit,
}

public static class SourceControlProviderExtensions
{
    public static string Label(this SourceControlProvider provider) =>
        provider switch
        {
            SourceControlProvider.GitHub => "GitHub",
            SourceControlProvider.GitLab => "GitLab",
            SourceControlProvider.BitbucketCloud => "Bitbucket Cloud",
            SourceControlProvider.Forgejo => "Forgejo",
            SourceControlProvider.Gitea => "Gitea",
            SourceControlProvider.CustomGit => "Custom Git",
            _ => provider.ToString(),
        };
}
