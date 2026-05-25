namespace RepoBar.Core.Api;

public static class Pagination
{
    public static Uri? GitHubNextLink(IEnumerable<string> linkHeaders)
    {
        foreach (string header in linkHeaders)
        {
            foreach (string segment in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = segment.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !parts.Any(part => part.Equals("rel=\"next\"", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string rawUri = parts[0].Trim();
                if (rawUri.StartsWith('<') && rawUri.EndsWith('>'))
                {
                    rawUri = rawUri[1..^1];
                }

                return Uri.TryCreate(rawUri, UriKind.Absolute, out Uri? uri) ? uri : null;
            }
        }

        return null;
    }

    public static int? GitLabNextPage(IEnumerable<string> nextPageHeaders)
    {
        string? value = nextPageHeaders.FirstOrDefault(header => !string.IsNullOrWhiteSpace(header));
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int page)
            ? page
            : null;
    }
}
