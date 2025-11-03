using CommandLine;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.CreateLagFeatures;

/// <summary>
/// create-lag-features command parameters for time series preprocessing
/// </summary>
[Verb("create-lag-features", HelpText = "Create lag features from time series data for machine learning")]
public class CreateLagFeaturesParameters : SingleInputParameters
{
    [Option('g', "group-by", Required = true,
        HelpText = "Column to group by (e.g., Part Number, Entity ID)")]
    public string GroupByColumn { get; set; } = string.Empty;

    [Option('t', "time-column", Required = true,
        HelpText = "Column representing time/sequence for sorting within groups")]
    public string TimeColumn { get; set; } = string.Empty;

    [Option('l', "lag-columns", Required = true, Separator = ',',
        HelpText = "Comma-separated list of columns to create lag features from")]
    public IEnumerable<string> LagColumns { get; set; } = Array.Empty<string>();

    [Option('p', "lag-periods", Required = true, Separator = ',',
        HelpText = "Comma-separated list of lag periods (e.g., 1,2,3)")]
    public IEnumerable<int> LagPeriods { get; set; } = Array.Empty<int>();

    [Option("target", Required = false,
        HelpText = "Target column to predict (optional, will be kept in output)")]
    public string? TargetColumn { get; set; }

    [Option("drop-nulls", Required = false, Default = true,
        HelpText = "Drop rows with null lag values (default: true)")]
    public bool DropNullRows { get; set; } = true;

    [Option('k', "keep-columns", Required = false, Separator = ',',
        HelpText = "Comma-separated list of additional columns to keep in output")]
    public IEnumerable<string> KeepColumns { get; set; } = Array.Empty<string>();

    public override Type GetHandlerType() => typeof(CreateLagFeaturesHandler);

    protected override bool ValidateInternal(ILogger logger)
    {
        if (!base.ValidateInternal(logger))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(GroupByColumn))
        {
            logger.LogError("Group-by column must be specified");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TimeColumn))
        {
            logger.LogError("Time column must be specified");
            return false;
        }

        if (!LagColumns.Any())
        {
            logger.LogError("At least one lag column must be specified");
            return false;
        }

        if (!LagPeriods.Any())
        {
            logger.LogError("At least one lag period must be specified");
            return false;
        }

        if (LagPeriods.Any(p => p <= 0))
        {
            logger.LogError("All lag periods must be positive integers");
            return false;
        }

        return true;
    }

    public override string? GetExample() =>
        "create-lag-features -i timeseries.csv -o features.csv " +
        "-g \"Part Number\" -t \"CRET_TIME\" " +
        "-l \"D+3,D+4,D+5\" -p \"3\" " +
        "--target \"D일계획\" --drop-nulls true";
}
