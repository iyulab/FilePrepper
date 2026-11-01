using System.Text.RegularExpressions;
using Xunit;

namespace FilePrepper.Tests.Documentation;

/// <summary>
/// No command reads a list option through <c>result.Tokens[n]</c>: that is the first occurrence only, and
/// the others are dropped without a word (<c>remove-columns -c a -c b</c> removed <c>a</c>). The tokens are
/// read through <c>ListOption</c>, which uses all of them.
/// </summary>
public class ListOptionContractTests
{
    /// <summary>A single token of a parse result read by position — the shape of the defect.</summary>
    private static readonly Regex TokenByPosition = new(@"\.Tokens\s*\[\s*\d+\s*\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static IEnumerable<string> CliSources() =>
        RepoSourceTree.PublishedTextFiles()
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal))
            .Where(file => RepoSourceTree.RelativeToRoot(file).Replace('\\', '/').StartsWith("src/FilePrepper.CLI/", StringComparison.Ordinal));

    private static IEnumerable<string> Offenders(IEnumerable<string> files) =>
        files.Where(file => TokenByPosition.IsMatch(File.ReadAllText(file))).Select(RepoSourceTree.RelativeToRoot);

    [Fact]
    public void No_command_reads_one_token_of_a_list_option_by_position()
    {
        Assert.Empty(Offenders(CliSources()));
    }

    [Fact]
    public void The_scan_sees_the_commands_it_polices()
    {
        // A scan whose file list came back empty would report "no offenders" too.
        var sources = CliSources().ToList();
        Assert.True(sources.Count(file => file.EndsWith("Command.cs", StringComparison.Ordinal)) >= 20,
            "expected the command sources under src/FilePrepper.CLI to be scanned");
        Assert.Contains(sources, file => file.EndsWith("ListOption.cs", StringComparison.Ordinal)
                                         && File.ReadAllText(file).Contains(".Tokens.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CustomParser = result => result.Tokens[0].Value.Split(',')")]
    [InlineData("var first = result.Tokens [ 1 ].Value;")]
    public void The_pattern_flags_a_token_read_by_position(string source)
    {
        Assert.Matches(TokenByPosition, source);
    }

    [Theory]
    [InlineData("CustomParser = result => ListOption.Split(result)")]
    [InlineData("var count = result.Tokens.Count;")]
    [InlineData("result.Tokens.Select(token => token.Value)")]
    public void The_pattern_leaves_the_tokens_read_as_a_whole(string source)
    {
        Assert.DoesNotMatch(TokenByPosition, source);
    }
}
