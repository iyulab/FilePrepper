using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A list option given more than once uses every occurrence. <c>remove-columns -c a -c b</c> removed
/// <c>a</c> and nothing else, said "Columns removed" and exited 0: each list option read the first token
/// of its argument only, so the others were dropped without a word.
/// </summary>
public class ListOptionTests : CommandTestBase
{
    [Fact]
    public async Task Remove_columns_removes_the_columns_of_every_occurrence_of_the_option()
    {
        var input = CreateTestCsv("wide.csv", ["a", "b", "c", "d"], [["1", "2", "3", "4"], ["5", "6", "7", "8"]]);
        var output = GetTempPath("wide_out.csv");

        var exitCode = await RunCommandAsync(new RemoveColumnsCommand(LoggerFactory), "-i", input, "-o", output, "-c", "a", "-c", "b,d");

        exitCode.Should().Be(0);
        var (headers, rows) = ReadCsvWithHeaders(output);
        headers.Should().Equal("c");
        rows.Select(row => row[0]).Should().Equal("3", "7");
    }

    [Fact]
    public async Task Add_columns_adds_the_columns_of_every_occurrence_of_the_option()
    {
        var input = CreateTestCsv("base.csv", ["id"], [["1"]]);
        var output = GetTempPath("base_out.csv");

        var exitCode = await RunCommandAsync(new AddColumnsCommand(LoggerFactory), "-i", input, "-o", output, "-c", "Country=KR", "-c", "City=Seoul");

        exitCode.Should().Be(0);
        var (headers, rows) = ReadCsvWithHeaders(output);
        headers.Should().Equal("id", "Country", "City");
        rows.Single().Should().Equal("1", "KR", "Seoul");
    }

    [Fact]
    public void The_entries_of_every_occurrence_are_listed_in_the_order_given()
    {
        ListOption.Split(["a,b", "c", "d,e"]).Should().Equal("a", "b", "c", "d", "e");
    }

    [Fact]
    public void An_empty_entry_is_dropped_and_white_space_is_trimmed_only_when_asked()
    {
        ListOption.Split([" a, ,b ,"]).Should().Equal(" a", " ", "b ");
        ListOption.Split([" a, ,b ,"], trim: true).Should().Equal("a", "b");
    }

    [Fact]
    public void The_separator_is_the_one_given()
    {
        ListOption.Split(["Date:Year,Month;Other:Day", "Last:Hour"], ';').Should().Equal("Date:Year,Month", "Other:Day", "Last:Hour");
    }

    [Fact]
    public void An_option_never_given_has_no_entries()
    {
        ListOption.Split(Array.Empty<string>()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_lag_period_that_is_not_a_whole_number_is_a_command_line_error_not_an_exception()
    {
        var input = CreateTestCsv("lag.csv", ["g", "t", "v"], [["A", "1", "5"], ["A", "2", "6"]]);
        var output = GetTempPath("lag_out.csv");

        var exit = await RunCommandAsync(new CreateLagFeaturesCommand(LoggerFactory), "-i", input, "-o", output,
            "--group-by", "g", "--time-column", "t", "--lag-columns", "v", "--lag-periods", "1,x");

        exit.Should().NotBe(0);
        FileExists(output).Should().BeFalse();
    }
}
