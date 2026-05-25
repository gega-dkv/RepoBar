namespace RepoBar.Core.Support;

public static class ChangelogParser
{
    public static ChangelogSummary Parse(string markdown, string? releaseTag = null)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        List<ChangelogSection> sections = [];
        ChangelogSection? current = null;
        List<string> body = [];

        foreach (string line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                current = new ChangelogSection(line[3..].Trim(), "");
                continue;
            }

            if (current is not null)
            {
                body.Add(line);
            }
        }

        Flush();

        ChangelogSection? selected = releaseTag is null
            ? sections.FirstOrDefault()
            : sections.FirstOrDefault(section => section.Title.Contains(releaseTag, StringComparison.OrdinalIgnoreCase));

        return new ChangelogSummary(sections, selected);

        void Flush()
        {
            if (current is null)
            {
                return;
            }

            sections.Add(current with { Body = string.Join('\n', body).Trim() });
            body.Clear();
        }
    }
}

public sealed record ChangelogSummary(
    IReadOnlyList<ChangelogSection> Sections,
    ChangelogSection? Selected);

public sealed record ChangelogSection(string Title, string Body);
