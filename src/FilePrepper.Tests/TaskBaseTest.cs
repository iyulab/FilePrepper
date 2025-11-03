using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace FilePrepper.Tests;

public abstract class TaskBaseTest<TTask> : BaseTests
    where TTask : class
{
    protected readonly Mock<ILogger<TTask>> _mockLogger;
    protected readonly string _testInputPath = Path.GetTempFileName();
    protected readonly string _testOutputPath = Path.GetTempFileName();

    protected TaskBaseTest(ITestOutputHelper output) : base(output)
    {
        _mockLogger = new Mock<ILogger<TTask>>();
        _mockLogger
            .Setup(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var eventId = (EventId)invocation.Arguments[1];
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                if (state != null && formatter != null)
                {
                    var formatterDelegate = formatter as Delegate;
                    if (formatterDelegate != null)
                    {
                        var message = formatterDelegate.DynamicInvoke(state, exception);
                        _output.WriteLine($"[{logLevel}] {message}");
                    }
                }
            }));
    }

    public override void Dispose()
    {
        if (File.Exists(_testInputPath)) File.Delete(_testInputPath);
        if (File.Exists(_testOutputPath)) File.Delete(_testOutputPath);
    }

    protected void WriteTestFile(string content)
    {
        File.WriteAllText(_testInputPath, content);
        _output.WriteLine("Test input file created:");
        _output.WriteLine(File.ReadAllText(_testInputPath));
    }

    protected void WriteTestFileLines(params string[] lines)
    {
        File.WriteAllLines(_testInputPath, lines);
        _output.WriteLine("Test input file created:");
        _output.WriteLine(File.ReadAllText(_testInputPath));
    }

    protected string[] ReadOutputFileLines()
    {
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            var content = File.ReadAllText(_testOutputPath);
            _output.WriteLine(content);
            return File.ReadAllLines(_testOutputPath);
        }
        _output.WriteLine("Output file was not created!");
        return Array.Empty<string>();
    }
}