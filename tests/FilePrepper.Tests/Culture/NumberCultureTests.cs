using System.Globalization;
using System.Text.RegularExpressions;
using FilePrepper.Pipeline;
using FilePrepper.Tasks.NormalizeData;
using FilePrepper.Utils;

namespace FilePrepper.Tests.Culture;

/// <summary>
/// A data file writes <c>1.5</c> whatever the machine's culture, so FilePrepper reads and writes numbers in the
/// invariant culture (<see cref="NumberText"/>). Measured before: on de-DE the cells <c>1.5</c> and <c>2.5</c> had a
/// mean of 20.
/// </summary>
[Collection(nameof(CurrentCultureCollection))]
public class NumberCultureTests
{
    private static T OnGerman<T>(Func<T> act)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try { return act(); }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Statistics_read_the_files_numbers_on_a_comma_decimal_machine()
    {
        var stats = OnGerman(() => ColumnStatistics.Of("x", ["1.5", "2.5"]));

        Assert.Equal(2.0, stats.Mean, 10);
    }

    [Fact]
    public void A_computed_number_is_written_back_with_a_point()
    {
        var values = OnGerman(() => DataPipeline
            .FromData(new List<Dictionary<string, string>> { new() { ["x"] = "1" }, new() { ["x"] = "2" }, new() { ["x"] = "3" } })
            .Normalize("x", NormalizationMethod.MinMax)
            .ToDataFrame()
            .GetColumn("x_normalized")
            .ToList());

        Assert.Equal(["0", "0.5", "1"], values);
    }

    // Source guard: a number read or written from text names its culture, or goes through NumberText.
    private static readonly Regex CultureBound = new(
        @"\bdouble\.(TryParse|Parse)\b(?![^;]*CultureInfo)|\.ToString\(""G""\)", RegexOptions.Compiled);

    internal static IEnumerable<string> Offenders(string source) =>
        source.Split('\n').Where(line => CultureBound.IsMatch(line));

    [Fact]
    public void No_source_reads_or_writes_a_number_in_the_current_culture()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && Path.GetFileName(f) != "NumberText.cs")
            .ToList();
        Assert.Contains(files, f => Path.GetFileName(f) == "DataPipeline.cs"); // the scan reaches the tree

        var offenders = files
            .SelectMany(f => Offenders(File.ReadAllText(f)).Select(line => $"{Path.GetFileName(f)}: {line.Trim()}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Read and write numbers through NumberText:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void The_guard_sees_the_shapes_it_forbids_and_passes_the_ones_it_allows()
    {
        Assert.Single(Offenders("if (double.TryParse(value, out var v))"));
        Assert.Single(Offenders("x = double.Parse(text);"));
        Assert.Single(Offenders("var values = cells.Select(double.Parse).ToList();")); // a method group, no parenthesis
        Assert.Single(Offenders("row[c] = mean.ToString(\"G\");"));
        Assert.Empty(Offenders("double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)"));
        Assert.Empty(Offenders("NumberText.TryParse(value, out var v)"));
    }
}

[CollectionDefinition(nameof(CurrentCultureCollection), DisableParallelization = true)]
public sealed class CurrentCultureCollection;
