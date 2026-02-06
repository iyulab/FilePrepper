using FilePrepper.Tasks;
using FilePrepper.Tasks.Merge;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.Merge;

public class MergeHandler : BaseCommandHandler<MergeParameters>
{
    public MergeHandler(
        ILoggerFactory loggerFactory,
        ILogger<MergeHandler> logger)
        : base(loggerFactory, logger)
    {
    }

    public override async Task<int> ExecuteAsync(ICommandParameters parameters)
    {
        var opts = (MergeParameters)parameters;
        if (!ValidateParameters(opts))
        {
            return ExitCodes.InvalidArguments;
        }

        return await HandleExceptionAsync(async () =>
        {
            if (!Enum.TryParse<MergeType>(opts.MergeType, true, out var mergeType))
            {
                _logger.LogError("Invalid merge type: {Type}", opts.MergeType);
                return ExitCodes.InvalidArguments;
            }

            if (!Enum.TryParse<JoinType>(opts.JoinType, true, out var joinType))
            {
                _logger.LogError("Invalid join type: {Type}", opts.JoinType);
                return ExitCodes.InvalidArguments;
            }

            // Resolve input files from pattern if specified
            var inputFiles = ResolveInputFiles(opts);
            if (inputFiles == null || inputFiles.Count < 2)
            {
                _logger.LogError("At least 2 input files are required");
                return ExitCodes.InvalidArguments;
            }

            var options = new MergeOption
            {
                InputPaths = inputFiles,
                OutputPath = opts.OutputPath,
                MergeType = mergeType,
                JoinType = joinType,
                JoinKeyColumns = opts.JoinKeyColumns.Select(column =>
                {
                    if (int.TryParse(column, out int index))
                    {
                        return ColumnIdentifier.ByIndex(index);
                    }
                    return ColumnIdentifier.ByName(column);
                }).ToList(),
                HasHeader = opts.HasHeader,
                IgnoreErrors = opts.IgnoreErrors,
                Encoding = opts.Encoding,
                SkipRows = opts.SkipRows
            };

            var taskLogger = _loggerFactory.CreateLogger<MergeTask>();
            var task = new MergeTask(taskLogger);
            var context = new TaskContext(options);

            _logger.LogInformation("Merging {Count} files using {Type} merge type",
                inputFiles.Count, mergeType);

            var success = await task.ExecuteAsync(context);
            return success ? ExitCodes.Success : ExitCodes.Error;
        });
    }

    private List<string>? ResolveInputFiles(MergeParameters opts)
    {
        if (!string.IsNullOrEmpty(opts.InputPattern))
        {
            var dir = Path.GetDirectoryName(opts.InputPattern) ?? ".";
            var pattern = Path.GetFileName(opts.InputPattern);

            if (!Directory.Exists(dir))
            {
                _logger.LogError("Directory not found: {Dir}", dir);
                return null;
            }

            var globFiles = Directory.GetFiles(dir, pattern).OrderBy(f => f).ToList();
            _logger.LogInformation("Pattern '{Pattern}' matched {Count} file(s)", opts.InputPattern, globFiles.Count);
            return globFiles;
        }

        return opts.InputFiles.ToList();
    }
}
