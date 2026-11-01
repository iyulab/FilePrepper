using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DateTimeOps;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// The features <c>datetime --mode features</c> adds are named, and a name that is not a feature is refused: a mistyped
/// <c>Mnth</c> used to be skipped with a warning, the command said it extracted the features that were left and exited 0, and
/// the file lacked a column the caller asked for. <c>extract-date</c> already refused its own mistyped component.
/// </summary>
public class DateTimeFeaturesTests : TaskBaseTest<DateTimeTask>
{
    public DateTimeFeaturesTests(ITestOutputHelper output) : base(output)
    {
    }

    private DateTimeOption Option(string features) => new()
    {
        Mode = DateTimeMode.ExtractFeatures,
        Column = "d",
        Features = features,
        InputPath = _testInputPath,
        OutputPath = _testOutputPath
    };

    private bool Run(string features) => new DateTimeTask(_mockLogger.Object).Execute(new TaskContext(Option(features)));

    [Fact]
    public void A_mistyped_feature_is_refused_and_nothing_is_written()
    {
        WriteTestFileLines("d", "2026-01-05");

        var error = Assert.Throws<ValidationException>(() => Run("Year,Mnth"));

        Assert.Contains("Unknown feature(s): Mnth", error.Message);
        Assert.Contains("Valid features: Year, Month, Day", error.Message);
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Theory]
    [InlineData("3")]        // a number is not a feature, although Enum.TryParse reads it as flag bits
    [InlineData("None")]
    [InlineData("Year,Month,Quarterly")]
    public void A_name_that_is_not_one_feature_is_refused(string features)
    {
        var errors = Option(features).Validate();

        Assert.Contains(errors, e => e.Contains("Unknown feature(s)"));
    }

    [Theory]
    [InlineData(",")]
    [InlineData(" , ")]
    public void A_list_with_no_feature_in_it_is_refused(string features)
    {
        Assert.Contains(Option(features).Validate(), e => e.Contains("Features are required"));
    }

    [Fact]
    public void Names_are_not_case_sensitive_and_may_have_white_space_around_them()
    {
        WriteTestFileLines("d", "2026-01-05");

        Assert.True(Run(" year , MONTH "));

        Assert.Equal(["d,d_Year,d_Month", "2026-01-05,2026,1"], ReadOutputFileLines());
    }

    [Fact]
    public void Parsing_returns_the_features_and_the_names_it_did_not_know()
    {
        var unknown = new List<string>();

        var features = DateTimeOption.ParseFeatures("Day, hour,Nope,7", unknown);

        Assert.Equal(DateFeatures.Day | DateFeatures.Hour, features);
        Assert.Equal(["Nope", "7"], unknown);
    }
}
