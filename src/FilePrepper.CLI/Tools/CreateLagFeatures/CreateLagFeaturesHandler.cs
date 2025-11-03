using FilePrepper.Tasks;
using FilePrepper.Tasks.CreateLagFeatures;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.CreateLagFeatures;

/// <summary>
/// CLI handler for create-lag-features command
/// </summary>
public class CreateLagFeaturesHandler : BaseCommandHandler<CreateLagFeaturesParameters>
{
    public CreateLagFeaturesHandler(
        ILoggerFactory loggerFactory,
        ILogger<CreateLagFeaturesHandler> logger)
        : base(loggerFactory, logger)
    {
    }

    public override async Task<int> ExecuteAsync(ICommandParameters parameters)
    {
        var opts = (CreateLagFeaturesParameters)parameters;
        if (!ValidateParameters(opts))
        {
            return ExitCodes.InvalidArguments;
        }

        return await HandleExceptionAsync(async () =>
        {
            var options = new CreateLagFeaturesOption
            {
                InputPath = opts.InputPath,
                OutputPath = opts.OutputPath,
                GroupByColumn = opts.GroupByColumn,
                TimeColumn = opts.TimeColumn,
                LagColumns = opts.LagColumns.ToList(),
                LagPeriods = opts.LagPeriods.ToList(),
                TargetColumn = opts.TargetColumn,
                DropNullRows = opts.DropNullRows,
                KeepColumns = opts.KeepColumns.ToList(),
                HasHeader = opts.HasHeader,
                IgnoreErrors = opts.IgnoreErrors
            };

            var taskLogger = _loggerFactory.CreateLogger<CreateLagFeaturesTask>();
            var task = new CreateLagFeaturesTask(taskLogger);
            var context = new TaskContext(options);

            _logger.LogInformation(
                "Creating lag features for {LagColumnCount} columns with {LagPeriodCount} lag periods from {InputPath}",
                opts.LagColumns.Count(), opts.LagPeriods.Count(), opts.InputPath);

            var success = await task.ExecuteAsync(context);
            return success ? ExitCodes.Success : ExitCodes.Error;
        });
    }
}
