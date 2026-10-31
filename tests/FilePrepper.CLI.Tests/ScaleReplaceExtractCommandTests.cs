using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// Three commands that failed on any input from the command line: scale and replace did not tell their task which columns they work on
/// ("At least one columns must be specified"), and extract-date split its option on the comma that separates a column's components,
/// so a column could be given one component only.
/// </summary>
public class ScaleReplaceExtractCommandTests : CommandTestBase
{
    private string Sample() => CreateTestCsv("sample.csv", ["Price", "Status", "OrderDate"],
    [
        ["10", "N/A", "2026-03-04"],
        ["20", "Active", "2026-05-06"],
        ["30", "Unknown", "2026-07-08"],
    ]);

    [Fact]
    public async Task Scale_FromTheCommandLine_ScalesTheColumn()
    {
        var output = GetTempPath("scaled.csv");

        var exit = await RunCommandAsync(new ScaleDataCommand(LoggerFactory), "--input", Sample(), "--output", output, "--scaling", "Price:MinMax");

        exit.Should().Be(0);
        File.ReadAllLines(output).Skip(1).Select(l => l.Split(',')[0]).Should().Equal("0", "0.5", "1");
    }

    [Fact]
    public async Task Replace_FromTheCommandLine_ReplacesTheValues()
    {
        var output = GetTempPath("replaced.csv");

        var exit = await RunCommandAsync(new ValueReplaceCommand(LoggerFactory), "--input", Sample(), "--output", output,
            "--replacements", "Status:N/A=Pending;Unknown=Pending");

        exit.Should().Be(0);
        File.ReadAllLines(output).Skip(1).Select(l => l.Split(',')[1]).Should().Equal("Pending", "Active", "Pending");
    }

    [Fact]
    public async Task ExtractDate_TakesSeveralComponentsOfOneColumn()
    {
        var output = GetTempPath("dated.csv");

        var exit = await RunCommandAsync(new DateExtractionCommand(LoggerFactory), "--input", Sample(), "--output", output,
            "--extractions", "OrderDate:Year,Month");

        exit.Should().Be(0);
        var header = File.ReadAllLines(output)[0].Split(',');
        header.Should().Contain(h => h.Contains("Year")).And.Contain(h => h.Contains("Month"));
    }

    [Fact]
    public async Task ExtractDate_TakesSeveralColumnsSeparatedBySemicolons()
    {
        var output = GetTempPath("dated2.csv");
        var input = CreateTestCsv("two.csv", ["A", "B"], [["2026-03-04", "2025-01-02"]]);

        var exit = await RunCommandAsync(new DateExtractionCommand(LoggerFactory), "--input", input, "--output", output,
            "--extractions", "A:Year,Month;B:Year");

        exit.Should().Be(0);
        File.ReadAllLines(output)[0].Split(',').Count(h => h.Contains("Year")).Should().Be(2);
    }
}
