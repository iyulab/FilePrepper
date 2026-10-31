using System.CommandLine;
using System.Text.RegularExpressions;
using FilePrepper.CLI.Commands;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// Every <c>fileprepper &lt;command&gt; ...</c> a document shows names a command that exists and options that command has. The
/// README's first CLI example was <c>fileprepper normalize-data --columns ... --method MinMax</c>, a command the tool has never had;
/// the guides still described the options of a version four releases back. Reading the documents once does not keep them right,
/// so this checks them against the real command tree.
/// </summary>
public class DocumentedInvocationsContractTests
{
    private static readonly string Root = FindRoot();

    /// <summary>
    /// Documents that still show commands or options the tool does not have. **This list may only shrink**: a document that is
    /// right again has to leave it, so the list keeps describing what is still wrong.
    /// </summary>
    private static readonly HashSet<string> Debt = new(StringComparer.Ordinal)
    {
        "docs/CLI-Guide.md",
        "docs/Common-Scenarios.md",
        "docs/Quick-Start.md",
        "docs/FILEPREPPER_PHASE2_COMPLETE.md",
    };

    private static readonly string[] Documents = ["README.md", "INSTALL.md", .. Directory.GetFiles(Path.Combine(Root, "docs"), "*.md").Select(f => "docs/" + Path.GetFileName(f)).Order()];

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FilePrepper.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("FilePrepper.sln not found above " + AppContext.BaseDirectory);
    }

    private static Dictionary<string, HashSet<string>> Tree() =>
        CommandFactory.CreateAllCommands(NullLoggerFactory.Instance).ToDictionary(
            c => c.Name,
            c => c.Options.SelectMany(o => o.Aliases.Prepend(o.Name)).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

    /// <summary>Global options the root command adds to every command.</summary>
    private static readonly string[] Global = ["--quiet", "-q", "-v", "--help", "-h", "-?"];

    /// <summary>The problems in the invocations <paramref name="document"/> shows, as "line: text".</summary>
    internal static List<string> Problems(string document, Dictionary<string, HashSet<string>> tree)
    {
        var problems = new List<string>();
        var lines = document.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        for (var i = 0; i < lines.Length; i++)
        {
            var start = Regex.Match(lines[i], @"^\s*fileprepper\s+([a-z][a-z0-9-]*)(.*)$");
            if (!start.Success || start.Groups[1].Value is "--help") continue;
            var command = start.Groups[1].Value;
            var text = start.Groups[2].Value;
            var first = i;
            while (text.TrimEnd().EndsWith('\\') && i + 1 < lines.Length)
                text += " " + lines[++i];
            if (!tree.TryGetValue(command, out var options))
            {
                problems.Add($"line {first + 1}: '{command}' is not a command");
                continue;
            }
            var unquoted = Regex.Replace(text, "\"[^\"]*\"|'[^']*'", " ");
            foreach (Match o in Regex.Matches(unquoted, @"(?<![\w-])(--?[A-Za-z][\w-]*)"))
                if (!options.Contains(o.Groups[1].Value) && !Global.Contains(o.Groups[1].Value))
                    problems.Add($"line {first + 1}: '{command}' has no option {o.Groups[1].Value}");
        }
        return problems;
    }

    [Fact]
    public void TheDocumentsShowOnlyRealCommandsAndOptions()
    {
        var tree = Tree();
        tree.Should().ContainKey("normalize");
        Documents.Should().Contain("README.md");

        var failing = Documents
            .Where(d => !Debt.Contains(d))
            .Select(d => (Document: d, Problems: Problems(File.ReadAllText(Path.Combine(Root, d)), tree)))
            .Where(x => x.Problems.Count > 0)
            .Select(x => x.Document + ":\n  " + string.Join("\n  ", x.Problems))
            .ToList();

        failing.Should().BeEmpty("these documents show a command or option the tool does not have:\n" + string.Join("\n", failing));
    }

    [Fact]
    public void ADocumentOnTheDebtListIsStillWrong()
    {
        var tree = Tree();

        var rightAgain = Debt.Where(d => Problems(File.ReadAllText(Path.Combine(Root, d)), tree).Count == 0).ToList();

        rightAgain.Should().BeEmpty("these documents are right now - remove them from the debt list");
    }

    [Fact]
    public void TheScan_SeesAWrongCommandAndAWrongOption()
    {
        var tree = new Dictionary<string, HashSet<string>> { ["normalize"] = ["--input", "-i", "--columns"] };
        const string doc = "```bash\nfileprepper normalize-data --input a.csv\nfileprepper normalize -i a.csv \\\n  --columns \"A,B\" --method MinMax\nfileprepper normalize -i a.csv --columns A\n```";

        var problems = Problems(doc, tree);

        problems.Should().Equal("line 2: 'normalize-data' is not a command", "line 3: 'normalize' has no option --method");
    }

    [Fact]
    public void TheReadmeTableNamesRealCommands()
    {
        var tree = Tree();
        var readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        var rows = Regex.Matches(readme, @"^\|[^|\n]*\|\s*`([a-z0-9-]+)`\s*\|", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();

        rows.Count.Should().BeGreaterThan(20, "the guard must see the table it polices");
        rows.Where(r => !tree.ContainsKey(r)).Should().BeEmpty();
        tree.Keys.Where(k => !rows.Contains(k)).Should().BeEmpty("every command has a row in the README table");
    }
}
