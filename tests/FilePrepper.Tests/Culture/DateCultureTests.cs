using System.Globalization;
using System.Text.RegularExpressions;
using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DateTimeOps;
using FilePrepper.Utils;

namespace FilePrepper.Tests.Culture;

/// <summary>
/// A date in a data file is written <c>2026-01-05 23:30</c> whatever the machine's culture and zone, so FilePrepper
/// reads and writes dates through <see cref="DateText"/>. Measured before: on th-TH the output format
/// <c>yyyy-MM-dd</c> wrote the year 2569, and a value ending in <c>Z</c> came back in the machine's zone.
/// </summary>
[Collection(nameof(CurrentCultureCollection))]
public class DateCultureTests : TaskBaseTest<DateTimeTask>
{
    public DateCultureTests(ITestOutputHelper output) : base(output)
    {
    }

    private static T In<T>(string culture, Func<T> act)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try { return act(); }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    private static readonly DateTime Moment = new(2026, 1, 5, 23, 30, 0);

    [Theory]
    [InlineData("th-TH")] // Buddhist calendar
    [InlineData("ar-SA")] // Um Al Qura calendar
    [InlineData("de-DE")]
    [InlineData("fi-FI")]
    public void A_date_is_written_the_same_in_every_culture(string culture)
    {
        Assert.Equal("2026-01-05 23:30:00", In(culture, () => DateText.Format(Moment)));
        Assert.Equal("05/01/2026 23:30", In(culture, () => DateText.Format(Moment, "dd/MM/yyyy HH:mm")));
    }

    [Fact]
    public void The_check_sees_a_culture_that_changes_the_year()
    {
        // The precondition the test above stands on: without DateText, th-TH writes another year.
        Assert.StartsWith("2569", In("th-TH", () => Moment.ToString("yyyy-MM-dd")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    [InlineData("en-GB")]
    public void A_slash_date_reads_month_first_in_every_culture(string culture)
    {
        Assert.True(In(culture, () => DateText.TryParse("01/02/2026", out var d) && d == new DateTime(2026, 1, 2)));
    }

    [Theory]
    [InlineData("2026-01-05T23:30:00Z")]
    [InlineData("2026-01-05T23:30:00+09:00")]
    [InlineData("2026-01-05T23:30:00-05:00")]
    [InlineData("2026-01-05 23:30:00")]
    [InlineData("2026-01-05T23:30")]
    public void The_clock_is_read_as_written_not_moved_into_the_machines_zone(string text)
    {
        Assert.True(DateText.TryParse(text, out var parsed));
        Assert.Equal(Moment, parsed);
    }

    [Fact]
    public void Text_that_is_not_a_date_is_not_one()
    {
        Assert.False(DateText.TryParse("not a date", out _));
        Assert.False(DateText.TryParse("", out _));
        Assert.False(DateText.TryParse(null, out _));
    }

    [Theory]
    [InlineData("2027-03-01", 9)]  // 2027 starts on a Friday: ISO week 1 begins on 4 January
    [InlineData("2012-12-31", 1)]  // belongs to week 1 of 2013
    [InlineData("2026-01-01", 1)]
    [InlineData("2020-12-31", 53)]
    public void Week_of_year_is_the_iso_week_on_every_path(string date, int week)
    {
        var rows = new List<Dictionary<string, string>> { new() { ["d"] = date } };
        var viaPipeline = DataPipeline.FromData(rows).ExtractDateFeatures("d", DateFeatures.WeekOfYear).ToDataFrame().GetColumn("d_WeekOfYear").Single();

        WriteTestFileLines("d", date);
        var option = new DateTimeOption { Mode = DateTimeMode.ExtractFeatures, Column = "d", Features = "WeekOfYear", InputPath = _testInputPath, OutputPath = _testOutputPath };
        Assert.True(new DateTimeTask(_mockLogger.Object).Execute(new TaskContext(option)));
        var viaTask = ReadOutputFileLines()[1].Split(',')[1];

        Assert.Equal(week.ToString(), viaPipeline);
        Assert.Equal(week.ToString(), viaTask);
    }

    [Theory]
    [InlineData("2026-01-05T23:30:00Z")]
    [InlineData("2026-01-05T23:30:00+09:00")]
    [InlineData("2026-01-05 23:30:00")]
    public void The_hour_and_day_of_a_value_are_the_ones_it_was_written_with(string text)
    {
        // Read through the machine's zone, 23:30Z came back as hour 8 of the next day on a UTC+9 machine.
        var rows = new List<Dictionary<string, string>> { new() { ["d"] = text } };
        var table = DataPipeline.FromData(rows).ExtractDateFeatures("d", DateFeatures.Day | DateFeatures.Hour).ToDataFrame();
        Assert.Equal(["5"], table.GetColumn("d_Day").ToList());
        Assert.Equal(["23"], table.GetColumn("d_Hour").ToList());

        WriteTestFileLines("d", text);
        var option = new DateTimeOption { Mode = DateTimeMode.ExtractFeatures, Column = "d", Features = "Day,Hour", InputPath = _testInputPath, OutputPath = _testOutputPath };
        Assert.True(new DateTimeTask(_mockLogger.Object).Execute(new TaskContext(option)));
        var cells = ReadOutputFileLines()[1].Split(',');
        Assert.Equal(["5", "23"], cells[^2..]);
    }

    [Fact]
    public void Parsing_a_format_on_a_thai_machine_writes_a_gregorian_year()
    {
        WriteTestFileLines("d", "20260105 2330");
        var option = new DateTimeOption { Mode = DateTimeMode.Parse, Column = "d", InputFormat = "yyyyMMdd HHmm", InputPath = _testInputPath, OutputPath = _testOutputPath };

        var lines = In("th-TH", () =>
        {
            Assert.True(new DateTimeTask(_mockLogger.Object).Execute(new TaskContext(option)));
            return ReadOutputFileLines();
        });

        Assert.Equal("2026-01-05 23:30:00", lines[1]);
    }

    // Source guard: a date read from text or written to text names its culture, or goes through DateText.
    private static readonly Regex CultureBound = new(
        @"\bDateTime(Offset)?\.(TryParse|Parse)\((?![^;]*CultureInfo|[^;]*culture)|(?<!ISOWeek\.)\bGetWeekOfYear\(|\.ToString\(""[^""]*[yMdHhms][^""]*""\)",
        RegexOptions.Compiled);

    internal static IEnumerable<string> Offenders(string source) =>
        source.Split('\n').Where(line => CultureBound.IsMatch(line) && !line.TrimStart().StartsWith("//"));

    [Fact]
    public void No_source_reads_or_writes_a_date_in_the_current_culture()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && Path.GetFileName(f) != "DateText.cs")
            .ToList();
        Assert.Contains(files, f => Path.GetFileName(f) == "DateTimeTask.cs"); // the scan reaches the tree

        var offenders = files
            .SelectMany(f => Offenders(File.ReadAllText(f)).Select(line => $"{Path.GetFileName(f)}: {line.Trim()}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Read and write dates through DateText:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void The_guard_sees_the_shapes_it_forbids_and_passes_the_ones_it_allows()
    {
        Assert.Single(Offenders("if (DateTime.TryParse(value, out var d))"));
        Assert.Single(Offenders("var d = DateTime.Parse(text);"));
        Assert.Single(Offenders("DateTimeOffset.TryParse(text, out var o)"));
        Assert.Single(Offenders("var week = calendar.GetWeekOfYear(d, rule, DayOfWeek.Monday);"));
        Assert.Single(Offenders("row[c] = d.ToString(\"yyyy-MM-dd\");"));
        Assert.Single(Offenders("row[c] = d.ToString(\"HH:mm:ss\");"));
        Assert.Empty(Offenders("DateTime.Parse(value, culture)"));
        Assert.Empty(Offenders("DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)"));
        Assert.Empty(Offenders("DateText.TryParse(value, out var d)"));
        Assert.Empty(Offenders("var week = ISOWeek.GetWeekOfYear(date);"));
        Assert.Empty(Offenders("row[c] = number.ToString(\"F2\", CultureInfo.InvariantCulture);"));
        Assert.Empty(Offenders("// DateTime.TryParse(value, out d) is what this replaces"));
    }
}
