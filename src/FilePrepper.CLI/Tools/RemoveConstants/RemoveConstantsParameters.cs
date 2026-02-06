using CommandLine;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.RemoveConstants;

[Verb("remove-constants", HelpText = "Remove constant or near-constant columns from the input file")]
public class RemoveConstantsParameters : SingleInputParameters
{
    [Option('t', "threshold", Default = 0.0,
        HelpText = "Unique ratio threshold (0.0 = exact constants only)")]
    public double Threshold { get; set; } = 0.0;

    [Option("report-only", Default = false,
        HelpText = "Only report constant columns without removing them")]
    public bool ReportOnly { get; set; }

    public override Type GetHandlerType() => typeof(RemoveConstantsHandler);

    protected override bool ValidateInternal(ILogger logger)
    {
        if (!base.ValidateInternal(logger))
            return false;

        if (Threshold < 0 || Threshold > 1)
        {
            logger.LogError("Threshold must be between 0.0 and 1.0");
            return false;
        }

        return true;
    }

    public override string? GetExample() =>
        "remove-constants -i input.csv -o output.csv\n" +
        "remove-constants -i input.csv -o output.csv --threshold 0.01\n" +
        "remove-constants -i input.csv -o output.csv --report-only";
}
