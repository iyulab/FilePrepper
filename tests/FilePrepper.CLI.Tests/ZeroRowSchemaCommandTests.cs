using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A file with a header and no rows keeps the schema the command makes. The output header was read off the first row, so with
/// none it fell back to the input's header whatever the command did to the columns: <c>add-columns</c> without its new column,
/// <c>aggregate</c> without its group and total, <c>unpivot</c> as wide as it came (twelve of thirty commands). The oracle here is
/// the command itself on the same file with rows: the two headers must be the same.
/// </summary>
public class ZeroRowSchemaCommandTests : CommandTestBase
{
    /// <summary>Commands that cannot know their columns without rows (the values decide them) and say so, and the one that merges nothing.</summary>
    private static readonly string[] RefusesWithoutRows = ["merge", "one-hot-encoding", "split-list"];

    /// <summary>Columns taken out for what the values show: with no rows nothing shows, so none is taken out (a header kept as it was).</summary>
    private static readonly string[] KeepsTheHeaderWithoutRows = ["remove-constants"];

    /// <summary>The output of <c>convert-format</c> is JSON, which has no header to compare.</summary>
    private static readonly string[] HasNoHeader = ["convert-format"];

    public static TheoryData<string> CommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in EveryCommandRunsTests.Cases.Keys.Except(HasNoHeader).Order(StringComparer.Ordinal)) data.Add(name);
        return data;
    }

    private async Task<(int exit, string output)> RunOn(string name, string content, string tag)
    {
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);
        var input = CreateTestCsv($"{tag}.csv", content);
        var output = GetTempPath($"{name}-{tag}.csv");
        var inputs = name is "merge" or "merge-asof" ? [input, name == "merge" ? input : CreateTestCsv("right.csv", EveryCommandRunsTests.Right)] : new[] { input };

        var exit = await RunCommandAsync(command, ["--input", .. inputs, "--output", output, .. EveryCommandRunsTests.Cases[name]]);
        return (exit, output);
    }

    private static string FirstLine(string path) => File.ReadLines(path).First().TrimStart('﻿');

    private static string HeaderOnly => EveryCommandRunsTests.Data.Split('\n')[0] + "\n";

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task A_file_with_no_rows_is_headed_as_the_same_file_with_rows(string name)
    {
        var withRows = await RunOn(name, EveryCommandRunsTests.Data, "rows");
        var withoutRows = await RunOn(name, HeaderOnly, "none");

        withRows.exit.Should().Be(0);
        if (RefusesWithoutRows.Contains(name))
        {
            withoutRows.exit.Should().NotBe(0);
            return;
        }

        withoutRows.exit.Should().Be(0);
        FirstLine(withoutRows.output).Should().Be(KeepsTheHeaderWithoutRows.Contains(name) ? HeaderOnly.TrimEnd('\n') : FirstLine(withRows.output));
    }

    [Fact]
    public async Task A_lag_of_one_row_keeps_the_columns_of_the_lag_rows()
    {
        // One row has no row before it, so no row comes out; the output is still the group column and the lag column.
        var oneRow = HeaderOnly + EveryCommandRunsTests.Data.Split('\n')[1] + "\n";

        var (exit, output) = await RunOn("create-lag-features", oneRow, "one");

        exit.Should().Be(0);
        File.ReadAllLines(output).Should().Equal("Category,Price_lag1");
    }

    [Fact]
    public void The_oracle_sees_the_commands_it_polices()
    {
        CommandNames().Count().Should().BeGreaterThan(25);
        RefusesWithoutRows.Concat(HasNoHeader).Concat(KeepsTheHeaderWithoutRows).Should().OnlyContain(name => EveryCommandRunsTests.Cases.ContainsKey(name));
        HeaderOnly.Should().Be("Id,Name,Price,Quantity,Category,OrderDate,Tags,Sizes,Const,Amount\n");
    }
}
