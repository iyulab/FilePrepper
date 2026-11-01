using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class StringCommandTests : CommandTestBase
{
    private readonly StringCommand _command;

    public StringCommandTests()
    {
        _command = new StringCommand(LoggerFactory);
    }

    [Fact]
    public async Task Replace_can_swap_a_space_for_another_character()
    {
        var input = CreateTestCsv("names.csv", ["name"], [["title one"], ["a b c"]]);
        var output = GetTempPath("names_out.csv");

        var exitCode = await RunCommandAsync(_command, "-i", input, "-o", output, "-m", "replace", "-c", "name", "-old", " ", "-new", "_");

        exitCode.Should().Be(0);
        var (_, rows) = ReadCsvWithHeaders(output);
        rows.Select(r => r[0]).Should().Equal("title_one", "a_b_c");
    }

    [Fact]
    public async Task A_trim_mode_that_is_not_one_of_the_three_is_refused_not_read_as_both()
    {
        var input = CreateTestCsv("pad.csv", ["v"], [["  x  "]]);
        var output = GetTempPath("pad_out.csv");

        var exitCode = await RunCommandAsync(_command, "-i", input, "-o", output, "-m", "trim", "-c", "v", "-tm", "tail");

        exitCode.Should().NotBe(0);
        FileExists(output).Should().BeFalse();
    }

    [Fact]
    public async Task A_negative_length_is_refused_not_written_as_empty_cells()
    {
        var input = CreateTestCsv("len.csv", ["name"], [["title one"]]);
        var output = GetTempPath("len_out.csv");

        var exitCode = await RunCommandAsync(_command, "-i", input, "-o", output, "-m", "substring", "-c", "name", "-l", "-3");

        exitCode.Should().NotBe(0);
        FileExists(output).Should().BeFalse();
    }
}
