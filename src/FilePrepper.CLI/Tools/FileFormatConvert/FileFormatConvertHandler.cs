using FilePrepper.Tasks;
using FilePrepper.Tasks.FileFormatConvert;
using Microsoft.Extensions.Logging;
using System.Text;

namespace FilePrepper.CLI.Tools.FileFormatConvert;

public class FileFormatConvertHandler : BaseCommandHandler<FileFormatConvertParameters>
{
    public FileFormatConvertHandler(
        ILoggerFactory loggerFactory,
        ILogger<FileFormatConvertHandler> logger)
        : base(loggerFactory, logger)
    {
    }

    public override async Task<int> ExecuteAsync(ICommandParameters parameters)
    {
        var opts = (FileFormatConvertParameters)parameters;
        if (!ValidateParameters(opts))
        {
            return ExitCodes.InvalidArguments;
        }

        return await HandleExceptionAsync(async () =>
        {
            if (!Enum.TryParse<FileFormat>(opts.TargetFormat, true, out var format))
            {
                _logger.LogError("Invalid target format: {Format}", opts.TargetFormat);
                return ExitCodes.InvalidArguments;
            }

            var outputEncoding = Encoding.GetEncoding(opts.OutputEncoding);

            var options = new FileFormatConvertOption
            {
                InputPath = opts.InputPath,
                OutputPath = opts.OutputPath,
                TargetFormat = format,
                OutputEncoding = outputEncoding,
                HasHeader = opts.HasHeader,
                PrettyPrint = opts.PrettyPrint,
                RootElementName = opts.RootElementName,
                ItemElementName = opts.ItemElementName,
                IgnoreErrors = opts.IgnoreErrors,
                Encoding = opts.Encoding,
                SkipRows = opts.SkipRows
            };

            var taskLogger = _loggerFactory.CreateLogger<FileFormatConvertTask>();
            var task = new FileFormatConvertTask(taskLogger);
            var context = new TaskContext(options);

            _logger.LogInformation("Converting {Input} to {Format} format",
                opts.InputPath, format);

            var success = await task.ExecuteAsync(context);
            return success ? ExitCodes.Success : ExitCodes.Error;
        });
    }
}