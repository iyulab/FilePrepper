using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// From the command line, for every command: a file whose header names a column twice, and an empty file, are refused with no
/// output. The sweep that found them ran each command on a file of an unusual shape — the first kept only the last of two
/// columns of one name (29 of 31 commands), the second let nine commands write a file headed <c>NoData</c> while twenty-two others
/// failed on it in ways of their own.
/// </summary>
public class InputShapeCommandTests : CommandTestBase
{
    private static readonly string TwiceNamed = EveryCommandRunsTests.Data.Replace("Const,Amount\n", "Const,Amount,Price\n", StringComparison.Ordinal)
        .Replace("\"\n", "\",9\n", StringComparison.Ordinal);

    public static TheoryData<string> CommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in EveryCommandRunsTests.Cases.Keys.Order(StringComparer.Ordinal)) data.Add(name);
        return data;
    }

    private async Task<(int exit, string output)> RunOn(string name, string content)
    {
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);
        var input = CreateTestCsv("shape.csv", content);
        var output = GetTempPath(name + (name == "convert-format" ? ".json" : ".csv"));
        var inputs = name is "merge" or "merge-asof" ? [input, name == "merge" ? input : CreateTestCsv("right.csv", EveryCommandRunsTests.Right)] : new[] { input };

        var exit = await RunCommandAsync(command, ["--input", .. inputs, "--output", output, .. EveryCommandRunsTests.Cases[name]]);
        return (exit, output);
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task A_file_with_a_column_named_twice_is_refused(string name)
    {
        var (exit, output) = await RunOn(name, TwiceNamed);

        exit.Should().NotBe(0);
        FileExists(output).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task An_empty_file_is_refused(string name)
    {
        var (exit, output) = await RunOn(name, string.Empty);

        exit.Should().NotBe(0);
        FileExists(output).Should().BeFalse();
    }

    [Fact]
    public void The_twice_named_file_has_the_column_twice_and_the_sweep_sees_the_commands()
    {
        TwiceNamed.Split('\n')[0].Split(',').Count(c => c == "Price").Should().Be(2);
        TwiceNamed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Should().OnlyContain(row => row.EndsWith(",9", StringComparison.Ordinal));
        EveryCommandRunsTests.Cases.Keys.Count.Should().BeGreaterThan(25);
    }
}
