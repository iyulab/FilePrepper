using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveConstants;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// A task that fails reports it once. It used to log the failure (with the stack), rethrow it with <c>throw ex</c> (which
/// restarted the stack at the logging call), and let the caller log it again - three copies of one error on the command line.
/// </summary>
public class FailureReportingTests : TaskBaseTest<RemoveConstantsTask>
{
    public FailureReportingTests(ITestOutputHelper output) : base(output)
    {
    }

    private RemoveConstantsOption Option(bool ignoreErrors = false) => new()
    {
        InputPath = _testInputPath,
        OutputPath = _testOutputPath,
        IgnoreErrors = ignoreErrors
    };

    private void VerifyLogged(LogLevel level, Times times) =>
        _mockLogger.Verify(logger => logger.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    [Fact]
    public void A_failure_is_thrown_with_its_own_stack_and_not_logged_by_the_task()
    {
        WriteTestFileLines("a,b", "1,2");

        var error = Assert.Throws<InvalidOperationException>(() => new RemoveConstantsTask(_mockLogger.Object).Execute(new TaskContext(Option())));

        // The stack starts where the task failed, not at a handler that caught and rethrew it.
        Assert.Contains("RemoveConstantsTask.ProcessRecordsAsync", error.StackTrace);
        VerifyLogged(LogLevel.Error, Times.Never());
    }

    [Fact]
    public void A_validation_failure_is_thrown_and_not_logged_by_the_task()
    {
        WriteTestFileLines("a,b", "1,2");
        var option = Option();
        option.UniqueRatioThreshold = 2;

        Assert.Throws<ValidationException>(() => new RemoveConstantsTask(_mockLogger.Object).Execute(new TaskContext(option)));

        VerifyLogged(LogLevel.Error, Times.Never());
    }

    [Fact]
    public void With_ignore_errors_the_failure_is_a_warning_and_the_task_returns_false()
    {
        WriteTestFileLines("a,b", "1,2");

        var success = new RemoveConstantsTask(_mockLogger.Object).Execute(new TaskContext(Option(ignoreErrors: true)));

        Assert.False(success);
        VerifyLogged(LogLevel.Warning, Times.AtLeastOnce());
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Fact]
    public void With_ignore_errors_a_validation_failure_is_logged_as_an_error_because_nothing_is_thrown()
    {
        WriteTestFileLines("a,b", "1,2");
        var option = Option(ignoreErrors: true);
        option.UniqueRatioThreshold = 2;

        var success = new RemoveConstantsTask(_mockLogger.Object).Execute(new TaskContext(option));

        Assert.False(success);
        VerifyLogged(LogLevel.Error, Times.AtLeastOnce());
    }
}
