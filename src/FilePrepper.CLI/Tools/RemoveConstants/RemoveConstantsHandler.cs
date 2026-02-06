using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveConstants;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.RemoveConstants;

public class RemoveConstantsHandler : BaseCommandHandler<RemoveConstantsParameters>
{
    public RemoveConstantsHandler(
        ILoggerFactory loggerFactory,
        ILogger<RemoveConstantsHandler> logger)
        : base(loggerFactory, logger)
    {
    }

    public override async Task<int> ExecuteAsync(ICommandParameters parameters)
    {
        var opts = (RemoveConstantsParameters)parameters;
        if (!ValidateParameters(opts))
        {
            return ExitCodes.InvalidArguments;
        }

        return await HandleExceptionAsync(async () =>
        {
            var options = new RemoveConstantsOption
            {
                InputPath = opts.InputPath,
                OutputPath = opts.OutputPath,
                UniqueRatioThreshold = opts.Threshold,
                ReportOnly = opts.ReportOnly,
                HasHeader = opts.HasHeader,
                IgnoreErrors = opts.IgnoreErrors,
                Encoding = opts.Encoding,
                SkipRows = opts.SkipRows
            };

            var taskLogger = _loggerFactory.CreateLogger<RemoveConstantsTask>();
            var task = new RemoveConstantsTask(taskLogger);
            var context = new TaskContext(options);

            _logger.LogInformation("Removing constant columns from {Input} (threshold: {Threshold})",
                opts.InputPath, opts.Threshold);

            var success = await task.ExecuteAsync(context);
            return success ? ExitCodes.Success : ExitCodes.Error;
        });
    }
}
