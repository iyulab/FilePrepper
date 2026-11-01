using System.Text;
using FilePrepper.CLI.Commands;
using FilePrepper.Utils;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A cell is a date to every command that reads dates or to none, and they read it as the same moment. <c>extract-date</c> read with
/// <c>DateTime.Parse</c>, so a value ending in <c>Z</c> or a UTC offset came back in the machine's own zone: <c>2026-01-02T23:30:00Z</c>
/// was the 3rd at 8 o'clock on a UTC+9 machine and the 2nd at 23 o'clock on any other. <c>datetime</c> and <c>merge-asof</c> took
/// <c>20260102</c> and <c>extract-date</c> did not. The oracle is the one reader, <see cref="DateText.TryParse(string?, out DateTime)"/>.
/// </summary>
public class DateCellCommandTests : CommandTestBase
{
    public static readonly string[] Cells =
    [
        "2026-01-02", "2026-1-2", "2026/01/02", "2026.01.02", "01/02/2026", "1/2/2026", "20260102", "202601020304", "20260102030405",
        "2026-01-02 03:04:05", "2026-01-02 03:04", "2026-01-02T03:04:05", "2026-01-02T23:30:00Z", "2026-01-02 23:30:00+09:00",
        "Jan 2, 2026", "2 Jan 2026", "2026-01-02 3:04 PM", " 2026-01-02 ", "0001-01-01", "9999-12-31", "2026-01",
        "13/01/2026", "2026-01-02 25:00", "2026-02-30", "2026-13-01", "２０２６-01-02", "26-01-02", "1767225600", "0000-00-00", "2026", "soon",
    ];

    public static TheoryData<string> CellData()
    {
        var data = new TheoryData<string>();
        foreach (var cell in Cells) data.Add(cell);
        return data;
    }

    private async Task<(int exit, Dictionary<string, string>? row)> RunAsync(string name, string cell, params string[] arguments)
    {
        var input = GetTempPath($"{name}-date-in.csv");
        await File.WriteAllTextAsync(input, $"Id,d\n1,\"{cell}\"\n", new UTF8Encoding(false));
        var output = GetTempPath($"{name}-date-out.csv");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);

        var exit = await RunCommandAsync(command, ["--input", input, "--output", output, .. arguments]);
        if (exit != 0 || !File.Exists(output))
            return (exit, null);

        var lines = (await File.ReadAllLinesAsync(output)).Select(line => line.TrimStart('﻿')).ToArray();
        var header = Fields(lines[0]);
        var values = Fields(lines[1]);
        return (exit, header.Zip(values, (h, v) => (h, v)).ToDictionary(pair => pair.h, pair => pair.v));
    }

    /// <summary>The fields of one CSV line, a quoted field keeping its commas.</summary>
    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted && c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }

        fields.Add(field.ToString());
        return fields;
    }

    [Theory]
    [MemberData(nameof(CellData))]
    public async Task A_cell_is_a_date_to_both_commands_or_to_neither_and_the_same_moment(string cell)
    {
        var isDate = DateText.TryParse(cell, out var expected);

        var extracted = await RunAsync("extract-date", cell, "--extractions", "d:Year,Month,Day,Hour");
        var featured = await RunAsync("datetime", cell, "--column", "d", "--mode", "features", "--features", "Year,Month,Day,Hour");

        (extracted.exit == 0).Should().Be(isDate, "extract-date refuses what is not a date");
        (featured.row?["d_Year"].Length > 0).Should().Be(isDate, "datetime");
        if (!isDate) return;

        var wanted = new[] { expected.Year, expected.Month, expected.Day, expected.Hour }.Select(n => n.ToString()).ToArray();
        new[] { extracted.row!["d_Year"], extracted.row["d_Month"], extracted.row["d_Day"], extracted.row["d_Hour"] }.Should().Equal(wanted, "extract-date");
        new[] { featured.row!["d_Year"], featured.row["d_Month"], featured.row["d_Day"], featured.row["d_Hour"] }.Should().Equal(wanted, "datetime");
    }

    [Fact]
    public void A_zoned_value_keeps_the_clock_it_was_written_with_on_any_machine()
    {
        // One of these two moves on every machine whose zone is not UTC+9 (and the other on UTC+9's neighbours), which is how the test
        // sees the old behaviour wherever it runs.
        DateText.TryParse("2026-01-02T23:30:00Z", out var utc).Should().BeTrue();
        DateText.TryParse("2026-01-02 23:30:00+09:00", out var offset).Should().BeTrue();

        (utc.Day, utc.Hour).Should().Be((2, 23));
        (offset.Day, offset.Hour).Should().Be((2, 23));
        Cells.Count(cell => DateText.TryParse(cell, out _)).Should().BeInRange(15, 22, "the oracle must see both kinds of cell");
    }
}
