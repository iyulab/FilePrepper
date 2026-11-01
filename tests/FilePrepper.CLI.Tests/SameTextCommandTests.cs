using System.Text;
using FilePrepper.CLI.Commands;
using FluentAssertions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// A syllable written whole (NFC) and written as its letters (NFD, the form macOS gives many files) are the same text. A file of
/// either form must give the same answer as the other, from the command line: <c>filter-rows</c> kept no row of the NFD file,
/// <c>replace</c> replaced nothing and <c>conditional</c> took every row for the else — each with exit code 0. A file in CP949 is the
/// same table in other bytes and must too. Every comparison here is ordinal: the default string comparison of the test library
/// treats the two forms as equal, which is how the first draft of this class passed.
/// </summary>
public class SameTextCommandTests : CommandTestBase
{
    private static readonly string[][] Rows =
    [
        ["1", "서울", "가나다", "10"], ["2", "부산", "라마바", "20"], ["3", "서울", "가나다", "30"],
        ["4", "대구", "사아자", "40"], ["5", "부산", "라마바", "50"],
    ];

    private const string Header = "Id,City,Tag,Amount";

    private static string Table(NormalizationForm form) =>
        Header.Normalize(form) + "\n" + string.Join("\n", Rows.Select(row => string.Join(",", row.Select(cell => cell.Normalize(form))))) + "\n";

    public static TheoryData<string, string[]> Questions() => new()
    {
        { "filter-rows", ["--conditions", "City:Equals:서울"] },
        { "filter-rows", ["--conditions", "City:Contains:울"] },
        { "replace", ["--replacements", "City:서울=인천"] },
        { "conditional", ["--conditions", "City == 서울 : A", "--else", "B", "--output-column", "Flag"] },
        { "drop-duplicates", ["--columns", "City", "--subset-only", "--keep-first"] },
        { "aggregate", ["--group-by", "City", "--aggregates", "Amount:Sum:Total"] },
        { "one-hot-encoding", ["--columns", "City"] },
    };

    private async Task<string> AnswerAsync(string name, string[] arguments, byte[] fileBytes, string tag)
    {
        var input = GetTempPath($"{name}-{tag}-in.csv");
        await File.WriteAllBytesAsync(input, fileBytes);
        var output = GetTempPath($"{name}-{tag}-out.csv");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == name);

        var exit = await RunCommandAsync(command, ["--input", input, "--output", output, .. arguments]);

        exit.Should().Be(0);
        return await File.ReadAllTextAsync(output, Encoding.UTF8);
    }

    [Theory]
    [MemberData(nameof(Questions))]
    public async Task A_file_written_as_letters_gives_the_answer_of_the_file_written_as_syllables(string name, string[] arguments)
    {
        var whole = await AnswerAsync(name, arguments, new UTF8Encoding(false).GetBytes(Table(NormalizationForm.FormC)), "nfc");
        var letters = await AnswerAsync(name, arguments, new UTF8Encoding(false).GetBytes(Table(NormalizationForm.FormD)), "nfd");

        string.Equals(letters, whole, StringComparison.Ordinal).Should().BeTrue("the output is in one form whichever the input was in");
    }

    [Theory]
    [MemberData(nameof(Questions))]
    public async Task A_file_in_cp949_gives_the_answer_of_the_file_in_utf8(string name, string[] arguments)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var utf8 = await AnswerAsync(name, arguments, new UTF8Encoding(false).GetBytes(Table(NormalizationForm.FormC)), "utf8");
        var cp949 = await AnswerAsync(name, arguments, Encoding.GetEncoding(949).GetBytes(Table(NormalizationForm.FormC)), "cp949");

        string.Equals(cp949, utf8, StringComparison.Ordinal).Should().BeTrue();
    }

    [Fact]
    public async Task A_filter_finds_its_rows_in_a_file_written_as_letters_and_names_a_column_written_as_syllables()
    {
        var letters = Table(NormalizationForm.FormD);
        var input = GetTempPath("name-in.csv");
        await File.WriteAllTextAsync(input, letters, new UTF8Encoding(false));
        var output = GetTempPath("name-out.csv");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == "filter-rows");

        var exit = await RunCommandAsync(command, "--input", input, "--output", output, "--conditions", "City:Equals:부산".Normalize(NormalizationForm.FormC));

        exit.Should().Be(0);
        var lines = await File.ReadAllLinesAsync(output);
        lines.Should().HaveCount(3);
        lines[1].Normalize(NormalizationForm.FormC).Should().StartWith("2,부산");
    }

    [Fact]
    public async Task A_header_written_as_letters_is_found_by_a_name_written_as_syllables()
    {
        var input = GetTempPath("header-in.csv");
        await File.WriteAllTextAsync(input, "Id,도시\n1,a\n".Replace("도시", "도시".Normalize(NormalizationForm.FormD), StringComparison.Ordinal), new UTF8Encoding(false));
        var output = GetTempPath("header-out.csv");
        var command = CommandFactory.CreateAllCommands(LoggerFactory).Single(c => c.Name == "remove-columns");

        var exit = await RunCommandAsync(command, "--input", input, "--output", output, "--columns", "도시");

        exit.Should().Be(0);
        (await File.ReadAllLinesAsync(output)).Should().Equal("Id", "1");
    }

    [Fact]
    public void The_two_forms_are_different_strings_and_the_theory_sees_the_commands_it_polices()
    {
        string.Equals("서울", "서울".Normalize(NormalizationForm.FormD), StringComparison.Ordinal).Should().BeFalse();
        Table(NormalizationForm.FormD).Should().NotBe(Table(NormalizationForm.FormC));
        Questions().Count().Should().BeGreaterThan(5);
    }
}
