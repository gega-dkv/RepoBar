namespace RepoBar.Cli;

public sealed class CliOptions
{
    private readonly Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> positionals = [];

    public bool Json { get; private set; }

    public bool Plain { get; private set; }

    public bool NoColor { get; private set; }

    public bool Help { get; private set; }

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        CliOptions options = new();
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--json" or "--json-output" or "-j":
                    options.Json = true;
                    break;
                case "--plain":
                    options.Plain = true;
                    break;
                case "--no-color":
                    options.NoColor = true;
                    break;
                case "--help" or "-h" or "help":
                    options.Help = true;
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        string key = arg[2..];
                        string? value = "true";
                        int equals = key.IndexOf('=', StringComparison.Ordinal);
                        if (equals >= 0)
                        {
                            value = key[(equals + 1)..];
                            key = key[..equals];
                        }
                        else if (index + 1 < args.Count && (args[index + 1].Length == 0 || args[index + 1][0] != '-'))
                        {
                            value = args[++index];
                        }

                        options.values[key] = value;
                    }
                    else
                    {
                        options.positionals.Add(arg);
                    }

                    break;
            }
        }

        return options;
    }

    public string CommandOrDefault(string fallback) => positionals.Count == 0 ? fallback : positionals[0];

    public string SubcommandOrDefault(string fallback) => positionals.Count < 2 ? fallback : positionals[1];

    public string? PositionalAt(int index) => index < positionals.Count ? positionals[index] : null;

    public string? Value(string name) => values.TryGetValue(name, out string? value) ? value : null;

    public bool Has(string name) => values.ContainsKey(name);

    public int Number(string name, int fallback) =>
        int.TryParse(Value(name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;

    public (string owner, string name) RepositoryTarget()
    {
        string target = PositionalAt(1) ?? throw new CliUsageException("Repository command requires <owner/name>.");
        string[] parts = target.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new CliUsageException("Repository must be in owner/name form.");
        }

        return (parts[0], parts[1]);
    }
}
