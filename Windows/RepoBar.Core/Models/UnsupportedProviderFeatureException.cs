namespace RepoBar.Core.Models;

public sealed class UnsupportedProviderFeatureException : NotSupportedException
{
    public UnsupportedProviderFeatureException(SourceControlProvider provider, string feature)
        : base($"{provider.Label()} does not support {feature}.")
    {
        Provider = provider;
        Feature = feature;
    }

    public SourceControlProvider Provider { get; }

    public string Feature { get; }
}
