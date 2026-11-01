using System.Text.RegularExpressions;
using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A column name that is not in the file is an error, from the command line, for every command that acts on a column. Thirteen
/// commands used to succeed on a mistyped column having done nothing to it: <c>BaseTask.GetRequiredColumns</c> was overridden by
/// nine tasks and called by none. The three commands that apply a list of names that may differ from file to file
/// (<c>remove-columns</c>, <c>rename-columns</c>, <c>reorder-columns</c>) skip a name that is not there and warn.
/// </summary>
public class MistypedColumnTests : CommandTestBase
{
    private static readonly string[] ColumnNamesOfTheSmokeFile = ["Price", "Quantity", "Name", "Category", "Const", "OrderDate", "Tags", "Sizes", "Id", "Amount"];

    /// <summary>Commands that skip a name that is not a column and say so, by design (one list serves files that differ).</summary>
    private static readonly string[] Tolerant = ["remove-columns", "rename-columns", "reorder-columns"];

    /// <summary>Commands whose invocation names no existing column (they add one, or work on the whole file).</summary>
    private static readonly string[] NamingNoExistingColumn = ["add-columns", "convert-format", "data-sampling", "merge", "remove-constants"];

    private static readonly Regex AnyOfThem = new(@"\b(" + string.Join("|", ColumnNamesOfTheSmokeFile) + @")\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string[] Mistype(IEnumerable<string> arguments) => [.. arguments.Select(argument => AnyOfThem.Replace(argument, "Nope"))];

    public static TheoryData<string> CommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in EveryCommandRunsTests.Cases.Keys.Order(StringComparer.Ordinal)) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task A_column_that_is_not_in_the_file_is_refused_or_skipped_with_a_warning(string name)
    {
        var arguments = EveryCommandRunsTests.Cases[name];
        var mistyped = Mistype(arguments);
        if (NamingNoExistingColumn.Contains(name))
        {
            mistyped.Should().Equal(arguments, "this command's invocation names no existing column");
            return;
        }

        mistyped.Should().NotEqual(arguments, "the case must name a column to mistype");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);
        var input = CreateTestCsv("smoke.csv", EveryCommandRunsTests.Data);
        var output = GetTempPath(name + ".csv");
        var inputs = name == "merge-asof" ? [input, CreateTestCsv("right.csv", EveryCommandRunsTests.Right)] : new[] { input };

        var exit = await RunCommandAsync(command, ["--input", .. inputs, "--output", output, .. mistyped]);

        if (Tolerant.Contains(name))
        {
            exit.Should().Be(0);
            FileExists(output).Should().BeTrue();
        }
        else
        {
            exit.Should().NotBe(0);
            FileExists(output).Should().BeFalse();
        }
    }

    [Fact]
    public void The_sweep_sees_the_commands_it_polices()
    {
        // Every command is either swept, tolerant by design, or listed as naming no existing column.
        EveryCommandRunsTests.Cases.Keys.Count(name => !NamingNoExistingColumn.Contains(name)).Should().BeGreaterThan(20);
        Tolerant.Concat(NamingNoExistingColumn).Should().OnlyContain(name => EveryCommandRunsTests.Cases.ContainsKey(name));
        Mistype(["--columns", "Price,Quantity"]).Should().Equal("--columns", "Nope,Nope");
        Mistype(["--mode", "upper"]).Should().Equal("--mode", "upper");
    }
}
