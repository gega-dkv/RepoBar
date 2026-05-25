namespace RepoBar.Core.Support;

public static class MarkdownPlainTextRenderer
{
    public static string Render(string markdown, int width = 100, bool noWrap = false)
    {
        IEnumerable<string> lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(NormalizeLine);

        if (noWrap)
        {
            return string.Join(Environment.NewLine, lines);
        }

        int targetWidth = Math.Max(20, width);
        return string.Join(Environment.NewLine, lines.Select(line => Wrap(line, targetWidth)));
    }

    private static string NormalizeLine(string line)
    {
        string trimmed = line.TrimEnd();
        if (trimmed.StartsWith('#'))
        {
            return trimmed.TrimStart('#').Trim();
        }

        return trimmed
            .Replace("**", "", StringComparison.Ordinal)
            .Replace("__", "", StringComparison.Ordinal)
            .Replace("`", "", StringComparison.Ordinal);
    }

    private static string Wrap(string line, int width)
    {
        if (line.Length <= width || string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        List<string> output = [];
        string remaining = line;
        while (remaining.Length > width)
        {
            int split = remaining.LastIndexOf(' ', width);
            if (split <= 0)
            {
                split = width;
            }

            output.Add(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }

        output.Add(remaining);
        return string.Join(Environment.NewLine, output);
    }
}
