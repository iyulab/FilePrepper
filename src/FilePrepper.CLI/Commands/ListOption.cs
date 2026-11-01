using System.CommandLine.Parsing;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Reading the value of a list-valued option: every occurrence of the option counts, and each is split
/// into its entries.
/// </summary>
/// <remarks>
/// A custom parser that reads one token of the argument by its position sees only the first occurrence,
/// so <c>--columns a --columns b</c> acted on <c>a</c> alone and succeeded — the second occurrence was
/// dropped without a word. Every list option reads its tokens here.
/// </remarks>
public static class ListOption
{
    /// <summary>
    /// The entries of every occurrence of the option, in the order given, empty entries dropped.
    /// </summary>
    /// <param name="result">The parse result of the option's argument.</param>
    /// <param name="separator">What separates the entries inside one occurrence.</param>
    /// <param name="trim">Whether to trim the white space around each entry.</param>
    public static string[] Split(ArgumentResult result, char separator = ',', bool trim = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Split(result.Tokens.Select(token => token.Value), separator, trim);
    }

    /// <summary>
    /// The whole numbers a list option holds. An entry that is not one is a command-line error naming it - the parse of
    /// <c>--lag-periods 1,x</c> used to throw out of the parser, with a stack trace and no word of which entry it was.
    /// </summary>
    public static int[] WholeNumbers(ArgumentResult result)
    {
        var numbers = new List<int>();
        foreach (var entry in Split(result))
        {
            if (int.TryParse(entry, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                numbers.Add(number);
            }
            else
            {
                result.AddError($"'{entry}' is not a whole number");
            }
        }

        return [.. numbers];
    }

    /// <summary>The same split over the raw values of the occurrences.</summary>
    public static string[] Split(IEnumerable<string> occurrences, char separator = ',', bool trim = false)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        var options = StringSplitOptions.RemoveEmptyEntries | (trim ? StringSplitOptions.TrimEntries : StringSplitOptions.None);
        return [.. occurrences.SelectMany(value => value.Split(separator, options))];
    }
}
