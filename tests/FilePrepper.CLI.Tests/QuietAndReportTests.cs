using FilePrepper.CLI.Commands;
using FluentAssertions;
using Spectre.Console;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// What a command says on the console. <c>--quiet</c> promises to suppress non-error output, yet the spinner's line of text was
/// written under it by all thirty-one commands; the report a <c>remove-constants --report-only</c> run exists to give went to the log,
/// which <c>--quiet</c> turns off; and <c>stats</c> said "Validation failed" without which statistic was unknown, the table that
/// says so having been lost from the one of thirteen copies that mattered.
/// </summary>
public class QuietAndReportTests : CommandTestBase
{
    public static TheoryData<string> CommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in EveryCommandRunsTests.Cases.Keys.Order(StringComparer.Ordinal)) data.Add(name);
        return data;
    }

    private async Task<(int exit, string said)> RunSayingAsync(bool quiet, string name, params string[] arguments)
    {
        var said = new StringWriter();
        var previous = AnsiConsole.Console;
        var wasQuiet = BaseCommand.Quiet;
        try
        {
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(said),
                Ansi = AnsiSupport.No,
                Interactive = InteractionSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
            });
            BaseCommand.Quiet = quiet;
            var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);
            var exit = await RunCommandAsync(command, arguments);
            return (exit, said.ToString());
        }
        finally
        {
            AnsiConsole.Console = previous;
            BaseCommand.Quiet = wasQuiet;
        }
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task A_command_that_works_says_nothing_under_quiet(string name)
    {
        var input = CreateTestCsv("quiet.csv", EveryCommandRunsTests.Data);
        var output = GetTempPath(name + (name == "convert-format" ? ".json" : ".csv"));
        var inputs = name is "merge" or "merge-asof" ? [input, name == "merge" ? input : CreateTestCsv("right.csv", EveryCommandRunsTests.Right)] : new[] { input };

        var (exit, said) = await RunSayingAsync(true, name, ["--input", .. inputs, "--output", output, .. EveryCommandRunsTests.Cases[name]]);

        exit.Should().Be(0);
        said.Should().BeEmpty();
    }

    [Fact]
    public async Task A_report_only_run_gives_its_report_even_under_quiet()
    {
        var input = CreateTestCsv("report.csv", ["a", "b", "c"], [["1", "x", "3"], ["1", "y", "4"], ["1", "z", "5"]]);

        var (exit, said) = await RunSayingAsync(true, "remove-constants", "--input", input, "--output", GetTempPath("report-out.csv"), "--report-only");

        exit.Should().Be(0);
        said.Should().Contain("Constant or low-variety columns (1): a");
        said.Should().NotContain("complete", "the commentary around the report is what --quiet turns off");
    }

    [Fact]
    public async Task A_report_only_run_that_finds_nothing_says_so()
    {
        var input = CreateTestCsv("none.csv", ["a", "b"], [["1", "x"], ["2", "y"]]);

        var (exit, said) = await RunSayingAsync(true, "remove-constants", "--input", input, "--output", GetTempPath("none-out.csv"), "--report-only");

        exit.Should().Be(0);
        said.Should().Contain("No constant or low-variety columns.");
    }

    [Fact]
    public async Task An_unknown_statistic_is_named_in_the_refusal()
    {
        var input = CreateTestCsv("stats.csv", ["a", "c"], [["1", "3"], ["2", "4"]]);

        var (exit, said) = await RunSayingAsync(false, "stats", "--input", input, "--output", GetTempPath("stats-out.csv"), "--columns", "a,c", "--stats", "Mean,Typo");

        exit.Should().NotBe(0);
        said.Should().Contain("Invalid statistic: 'Typo'");
    }

    [Fact]
    public async Task A_refusal_is_not_silenced_by_quiet()
    {
        var input = CreateTestCsv("stats-q.csv", ["a", "c"], [["1", "3"], ["2", "4"]]);

        var (exit, said) = await RunSayingAsync(true, "stats", "--input", input, "--output", GetTempPath("stats-q-out.csv"), "--columns", "a,c", "--stats", "Typo");

        exit.Should().NotBe(0);
        said.Should().Contain("Invalid statistic: 'Typo'");
    }
}
