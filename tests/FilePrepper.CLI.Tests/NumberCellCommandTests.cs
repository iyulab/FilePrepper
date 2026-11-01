using System.Text;
using FilePrepper.CLI.Commands;
using FilePrepper.Utils;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A cell is a number to every command or to none. <c>aggregate</c>, <c>stats</c> and <c>column-interaction</c> stripped every comma
/// before reading — <c>1,5</c> was 15, <c>1,200</c> was 1200 — while <c>expression</c>, <c>filter-rows</c> and <c>conditional</c> did
/// not read either as a number; the latter accepted <c>Infinity</c> and a value too large for a double (<c>1e400</c>) and wrote
/// <c>Infinity</c> into the output. The oracle is the one reader, <see cref="NumberText.TryParse"/>.
/// </summary>
public class NumberCellCommandTests : CommandTestBase
{
    public static readonly string[] Cells =
    [
        "5", "2.5", "-3", "+4", "1e2", "1E-2", ".5", "5.", " 8 ", "-0.25",
        "１２", "1,234", "1,5", "12,34", "(5)", "12%", "$7", "₩1,200", "1.234,5", "1 234", "1_000", "0x10", "٣",
        "NaN", "inf", "-inf", "Infinity", "-Infinity", "1e400", "-1e400", "--5", "5-", "1/2", "five",
    ];

    public static TheoryData<string> CellData()
    {
        var data = new TheoryData<string>();
        foreach (var cell in Cells) data.Add(cell);
        return data;
    }

    private async Task<List<Dictionary<string, string>>> RunAsync(string name, string cell, params string[] arguments)
    {
        var input = GetTempPath($"{name}-cell-in.csv");
        // The cell sits among numbers so a command that needs a column to vary has one; quoting keeps its comma and spaces.
        await File.WriteAllTextAsync(input, $"Id,v\n1,\"{cell}\"\n2,7\n", new UTF8Encoding(false));
        var output = GetTempPath($"{name}-cell-out.csv");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);

        var exit = await RunCommandAsync(command, ["--input", input, "--output", output, .. arguments]);

        exit.Should().Be(0);
        var lines = (await File.ReadAllLinesAsync(output)).Select(line => line.TrimStart('﻿')).ToArray();
        var header = Fields(lines[0]);
        return [.. lines.Skip(1).Select(line => header.Zip(Fields(line), (h, v) => (h, v)).ToDictionary(pair => pair.h, pair => pair.v))];
    }

    /// <summary>The fields of one CSV line, a quoted field keeping its commas (the cells here are quoted for that).</summary>
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
    public async Task A_cell_is_a_number_to_every_command_or_to_none(string cell)
    {
        var isNumber = NumberText.TryParse(cell, out _);

        var expression = await RunAsync("expression", cell, "--expressions", "w=v*1");
        var filtered = await RunAsync("filter-rows", cell, "--conditions", "v:GreaterThan:-1e300");
        var conditional = await RunAsync("conditional", cell, "--conditions", "v > -1e300 : Y", "--else", "N", "--output-column", "w");
        var aggregated = await RunAsync("aggregate", cell, "--group-by", "Id", "--aggregates", "v:Sum:w");

        (expression.First(row => row["Id"] == "1")["w"].Length > 0).Should().Be(isNumber, "expression");
        filtered.Any(row => row["Id"] == "1").Should().Be(isNumber, "filter-rows");
        (conditional.First(row => row["Id"] == "1")["w"] == "Y").Should().Be(isNumber, "conditional");
        (aggregated.First(row => row["Id"] == "1").GetValueOrDefault("w", string.Empty).Length > 0).Should().Be(isNumber, "aggregate");
    }

    [Fact]
    public void The_one_reader_takes_plain_finite_numbers_and_nothing_else()
    {
        foreach (var number in new[] { "5", "2.5", "-3", "+4", "1e2", ".5", "5.", " 8 " })
            NumberText.TryParse(number, out _).Should().BeTrue(number);
        foreach (var notNumber in new[] { "1,200", "1,5", "NaN", "Infinity", "-Infinity", "1e400", "", " ", "five", "12%", "１２" })
            NumberText.TryParse(notNumber, out _).Should().BeFalse(notNumber);
        Cells.Count(cell => NumberText.TryParse(cell, out _)).Should().BeInRange(8, 12, "the oracle must see both kinds of cell");
    }
}
