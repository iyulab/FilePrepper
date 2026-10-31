using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// Every command runs, once, on a small file from the command line. scale, replace and extract-date failed on any input for as long as
/// they existed because the tests drove the tasks with options built by hand, and nothing drove the command that builds them from
/// arguments. A command with no case here fails the build, so a new command has to show one invocation that works.
/// </summary>
public class EveryCommandRunsTests : CommandTestBase
{
    private const string Data =
        "Id,Name,Price,Quantity,Category,OrderDate,Tags,Sizes,Const,Amount\n" +
        "0, Item0 ,10,1,B,2026-01-10 00:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,200\"\n" +
        "1, Item1 ,11,2,A,2026-02-11 01:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,201\"\n" +
        "2, Item2 ,12,3,B,2026-03-12 02:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,202\"\n" +
        "3, Item3 ,13,1,A,2026-04-13 03:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,203\"\n" +
        "4, Item4 ,,2,B,2026-05-14 04:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,204\"\n" +
        "5, Item5 ,15,3,A,2026-06-15 05:30:00,\"[\"\"x\"\",\"\"y\"\"]\",\"[\"\"1\"\",\"\"2\"\"]\",k,\"1,205\"\n";

    private const string Right = "OrderDate,Rate\n2026-01-10 00:00:00,1.5\n2026-03-10 00:00:00,1.7\n";

    /// <summary>The arguments after --input and --output that make each command do its work on <see cref="Data"/>.</summary>
    private static readonly Dictionary<string, string[]> Cases = new(StringComparer.Ordinal)
    {
        ["add-columns"] = ["--columns", "Country=KR"],
        ["aggregate"] = ["--group-by", "Category", "--aggregates", "Price:Sum:Total"],
        ["clean"] = ["--columns", "Amount"],
        ["column-interaction"] = ["--source", "Price,Quantity", "--type", "Multiply", "--column", "Revenue", "--ignore-errors", "--default-value", "0"],
        ["conditional"] = ["--conditions", "Price > 12 : High", "--else", "Low", "--output-column", "Level"],
        ["convert-format"] = ["--target", "JSON"],
        ["convert-type"] = ["--conversions", "Price:Decimal"],
        ["create-lag-features"] = ["--group-by", "Category", "--time-column", "Id", "--lag-columns", "Price", "--lag-periods", "1"],
        ["data-sampling"] = ["--method", "Random", "--size", "4", "--seed", "1"],
        ["datetime"] = ["--column", "OrderDate", "--mode", "features", "--features", "Year,Month"],
        ["drop-duplicates"] = ["--columns", "Category", "--subset-only", "--keep-first"],
        ["explode"] = ["--columns", "Tags,Sizes"],
        ["expression"] = ["--expressions", "Total=Price*Quantity"],
        ["extract-date"] = ["--extractions", "OrderDate:Year,Month"],
        ["fill-missing"] = ["--methods", "Price:Mean"],
        ["filter-rows"] = ["--conditions", "Price:GreaterThan:11"],
        ["merge"] = ["--type", "Vertical"],
        ["merge-asof"] = ["--left-on", "OrderDate", "--right-on", "OrderDate"],
        ["normalize"] = ["--columns", "Price", "--method", "MinMax"],
        ["one-hot-encoding"] = ["--columns", "Category"],
        ["remove-columns"] = ["--columns", "Const"],
        ["remove-constants"] = [],
        ["rename-columns"] = ["--mappings", "Price:Cost"],
        ["reorder-columns"] = ["--order", "Id,Name"],
        ["replace"] = ["--replacements", "Category:A=Alpha"],
        ["scale"] = ["--scaling", "Quantity:MinMax"],
        ["split-list"] = ["--columns", "Tags"],
        ["stats"] = ["--columns", "Quantity", "--stats", "Mean"],
        ["string"] = ["--mode", "upper", "--column", "Name"],
        ["unpivot"] = ["--base-columns", "Id", "--column-groups", "Price", "Quantity", "--value-columns", "Value"],
        ["window"] = ["--type", "rolling", "--method", "mean", "--columns", "Quantity"],
    };

    private static IEnumerable<string> Names => CommandFactory.CreateAllCommands(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).Select(c => c.Name);

    public static TheoryData<string> CommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Names.Order(StringComparer.Ordinal)) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task TheCommandRunsOnASmallFile(string name)
    {
        Cases.Should().ContainKey(name, "every command needs a case that shows it working");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);
        var input = CreateTestCsv("smoke.csv", Data);
        var output = GetTempPath(name + (name == "convert-format" ? ".json" : ".csv"));
        var inputs = name is "merge" or "merge-asof" ? [input, name == "merge" ? input : CreateTestCsv("right.csv", Right)] : new[] { input };

        var exit = await RunCommandAsync(command, ["--input", .. inputs, "--output", output, .. Cases[name]]);

        exit.Should().Be(0);
        FileExists(output).Should().BeTrue();
        new FileInfo(output).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EveryCaseNamesARealCommand()
    {
        Names.Should().HaveCountGreaterThan(25, "the guard must see the commands it polices");
        Cases.Keys.Where(k => !Names.Contains(k)).Should().BeEmpty();
    }
}
