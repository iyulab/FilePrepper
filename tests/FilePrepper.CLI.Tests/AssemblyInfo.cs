using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// The console (Spectre's AnsiConsole.Console) and BaseCommand.Quiet are one per process; a test that swaps them to read what a
// command says would read what a test running beside it says too. [Collection] orders only the classes that share its name.
[assembly: Parallelization(Mode = ParallelMode.None)]
