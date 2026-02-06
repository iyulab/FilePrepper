using CommandLine;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools;

public interface ICommandParameters
{
    Type GetHandlerType();
    bool Validate(ILogger logger);
    CommonOptionParameters GetCommonOptions();
    bool HasHeader { get; }
    bool IgnoreErrors { get; }
    string OutputPath { get; }
}

public interface IDefaultValueParameters
{
    string? DefaultValue { get; }
}

public interface IAppendableParameters
{
    bool AppendToSource { get; }
    string? OutputColumnTemplate { get; }
}

/// <summary>
/// 모든 CLI 매개변수의 기본 추상 클래스
/// </summary>
public abstract class BaseParameters : ICommandParameters
{
    [Option("has-header", Default = "true",
        HelpText = "Whether input files have headers (true/false).")]
    public string HasHeaderOption { get; set; } = "true";

    [Option("no-header", Default = false,
        HelpText = "Specify that input files do not have headers.")]
    public bool NoHeader { get; set; }

    public bool HasHeader => NoHeader
        ? false
        : (!bool.TryParse(HasHeaderOption, out var result) || result);

    [Option("ignore-errors", Required = false, Default = false,
        HelpText = "Whether to ignore errors during processing. Use --ignore-errors=true to enable.")]
    public bool IgnoreErrors { get; set; }

    [Option("encoding", Required = false, Default = "auto",
        HelpText = "File encoding (auto, utf-8, cp949, euc-kr)")]
    public string Encoding { get; set; } = "auto";

    [Option("skip-rows", Required = false, Default = 0,
        HelpText = "Number of rows to skip before header")]
    public int SkipRows { get; set; } = 0;

    [Option('o', "output", Required = true,
        HelpText = "Output file path")]
    public string OutputPath { get; set; } = string.Empty;

    public abstract Type GetHandlerType();

    public virtual bool Validate(ILogger logger)
    {
        if (!ValidateOutputPath(OutputPath, logger))
        {
            return false;
        }

        return ValidateInternal(logger);
    }

    public CommonOptionParameters GetCommonOptions()
    {
        return new CommonOptionParameters
        {
            HasHeader = HasHeader,
            IgnoreErrors = IgnoreErrors,
            OutputPath = OutputPath
        };
    }

    protected virtual bool ValidateInternal(ILogger logger) => true;

    protected bool ValidateOutputPath(string outputPath, ILogger logger)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            logger.LogError("Output path is not specified");
            return false;
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            logger.LogError($"Output directory does not exist: {outputDir}");
            return false;
        }

        return true;
    }

    protected bool ValidateInputPath(string inputPath, ILogger logger)
    {
        if (string.IsNullOrEmpty(inputPath))
        {
            logger.LogError("Input path is not specified");
            return false;
        }

        if (!File.Exists(inputPath))
        {
            logger.LogError($"Input file does not exist: {inputPath}");
            return false;
        }

        return true;
    }

    public abstract string? GetExample();
}

/// <summary>
/// 단일 입력 파일을 처리하는 명령어를 위한 기본 클래스
/// </summary>
public abstract class SingleInputParameters : BaseParameters, IDefaultValueParameters, IAppendableParameters
{
    [Option('i', "input", Required = true,
        HelpText = "Input file path")]
    public string InputPath { get; set; } = string.Empty;

    [Option("default-value", Required = false,
        HelpText = "Default value to use when encountering errors")]
    public string? DefaultValue { get; set; }

    [Option("append-to-source", Required = false, Default = false,
        HelpText = "Whether to append the result to source columns")]
    public bool AppendToSource { get; set; }

    [Option("output-column", Required = false,
        HelpText = "Template for the output column name")]
    public string? OutputColumnTemplate { get; set; }

    protected override bool ValidateInternal(ILogger logger)
    {
        return ValidateInputPath(InputPath, logger);
    }
}

/// <summary>
/// 다중 입력 파일을 처리하는 명령어를 위한 기본 클래스
/// </summary>
public abstract class MultipleInputParameters : BaseParameters, IDefaultValueParameters, IAppendableParameters
{
    [Value(0, Required = true, Min = 2,
        HelpText = "Input CSV files to merge (minimum 2 files required)")]
    public IEnumerable<string> InputFiles { get; set; } = [];

    [Option("default-value", Required = false,
        HelpText = "Default value to use when encountering errors")]
    public string? DefaultValue { get; set; }

    [Option("append-to-source", Required = false, Default = false,
        HelpText = "Whether to append the result to source columns")]
    public bool AppendToSource { get; set; }

    [Option("output-column", Required = false,
        HelpText = "Template for the output column name")]
    public string? OutputColumnTemplate { get; set; }

    protected override bool ValidateInternal(ILogger logger)
    {
        var inputList = InputFiles.ToList();
        if (inputList.Count < 2)
        {
            logger.LogError("At least two input files are required");
            return false;
        }

        return inputList.All(path => ValidateInputPath(path, logger));
    }
}

/// <summary>
/// 컬럼 기반 명령어를 위한 기본 클래스
/// </summary>
public abstract class BaseColumnParameters : SingleInputParameters
{
    [Option('c', "columns", Required = true, Separator = ',',
        HelpText = "Target columns to process")]
    public IEnumerable<string> TargetColumns { get; set; } = Array.Empty<string>();

    protected override bool ValidateInternal(ILogger logger)
    {
        if (!base.ValidateInternal(logger))
            return false;

        if (!TargetColumns.Any())
        {
            logger.LogError("At least one target column must be specified");
            return false;
        }

        return true;
    }
}