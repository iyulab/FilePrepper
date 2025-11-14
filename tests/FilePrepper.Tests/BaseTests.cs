using Xunit.Abstractions;

namespace FilePrepper.Tests;

public abstract class BaseTests : IDisposable
{
    protected readonly ITestOutputHelper _output;

    protected BaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public abstract void Dispose();
}
