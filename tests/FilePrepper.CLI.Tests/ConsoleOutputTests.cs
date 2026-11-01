using FilePrepper.CLI;
using FilePrepper.CLI.Commands;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// What the command line says. <c>--quiet</c> was declared ("Suppress non-error output") and read by nothing, <c>-v</c> changed
/// nothing, and a failure printed the same stack trace three times before one line saying what to change.
/// </summary>
public class ConsoleOutputTests : CommandTestBase
{
    private sealed class Captured : ILoggerFactory, ILogger
    {
        public List<(LogLevel Level, Exception? Error)> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }

    [Theory]
    [InlineData("remove-columns -i a.csv -o b.csv -c x", LogLevel.Information)]
    [InlineData("remove-columns -i a.csv -o b.csv -c x -q", LogLevel.Error)]
    [InlineData("remove-columns -i a.csv -o b.csv -c x --quiet", LogLevel.Error)]
    [InlineData("remove-columns -i a.csv -o b.csv -c x -v", LogLevel.Debug)]
    [InlineData("remove-columns -i a.csv -o b.csv -c x --verbose", LogLevel.Debug)]
    [InlineData("remove-columns -i a.csv -o b.csv -c x -q -v", LogLevel.Error)]
    [InlineData("-v", LogLevel.Information)] // on its own -v shows the version
    public void The_arguments_choose_the_log_level(string commandLine, LogLevel expected)
    {
        Program.LogLevelFor(commandLine.Split(' ')).Should().Be(expected);
    }

    [Fact]
    public void A_mistake_of_the_caller_is_not_logged_as_an_error_with_a_stack()
    {
        BaseCommand.IsExpected(new ArgumentException("bad value")).Should().BeTrue();
        BaseCommand.IsExpected(new InvalidOperationException("left no columns")).Should().BeTrue();
        BaseCommand.IsExpected(new FileNotFoundException("no such file")).Should().BeTrue();
        BaseCommand.IsExpected(new ValidationException("Column 'x' not found", ValidationExceptionErrorCode.General)).Should().BeTrue();
    }

    [Fact]
    public void A_defect_keeps_its_stack_in_the_log()
    {
        BaseCommand.IsExpected(new NullReferenceException()).Should().BeFalse();
        BaseCommand.IsExpected(new KeyNotFoundException()).Should().BeFalse();
        BaseCommand.IsExpected(new DivideByZeroException()).Should().BeFalse();
    }

    [Fact]
    public async Task A_failing_command_logs_no_error_for_a_mistake_of_the_caller()
    {
        var log = new Captured();
        var input = CreateTestCsv("one.csv", ["a", "b"], [["1", "2"]]);

        var exit = await RunCommandAsync(new RemoveConstantsCommand(log), "-i", input, "-o", GetTempPath("o.csv"));

        exit.Should().NotBe(0);
        log.Entries.Where(entry => entry.Level >= LogLevel.Error).Should().BeEmpty("the one line the command prints says what to change");
    }
}
