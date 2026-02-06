using CommandLine;
using FilePrepper.Tasks.Merge;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.Merge;

[Verb("merge", HelpText = "Merge multiple CSV files")]
public class MergeParameters : MultipleInputParameters
{
    [Option('t', "type", Required = true,
        HelpText = "Merge type (Vertical/Horizontal)")]
    public string MergeType { get; set; } = string.Empty;

    [Option('j', "join-type", Default = "Inner",
        HelpText = "Join type for horizontal merge (Inner/Left/Right/Full)")]
    public string JoinType { get; set; } = "Inner";

    [Option('k', "key-columns", Separator = ',',
        HelpText = "Key columns for horizontal merge")]
    public IEnumerable<string> JoinKeyColumns { get; set; } = Array.Empty<string>();

    [Option('p', "input-pattern",
        HelpText = "Glob pattern for input files (e.g., 'data/*.csv')")]
    public string? InputPattern { get; set; }

    public override Type GetHandlerType() => typeof(MergeHandler);

    protected override bool ValidateInternal(ILogger logger)
    {
        if (!base.ValidateInternal(logger))
            return false;

        // MergeType 검증
        if (!Enum.TryParse<Tasks.Merge.MergeType>(MergeType, true, out var mergeType))
        {
            logger.LogError("Invalid merge type: {Type}. Valid values are: {ValidValues}",
                MergeType, string.Join(", ", Enum.GetNames<Tasks.Merge.MergeType>()));
            return false;
        }

        // 수평 병합일 때의 추가 검증
        if (mergeType == Tasks.Merge.MergeType.Horizontal)
        {
            // JoinType 검증
            if (!Enum.TryParse<Tasks.Merge.JoinType>(JoinType, true, out _))
            {
                logger.LogError("Invalid join type: {Type}. Valid values are: {ValidValues}",
                    JoinType, string.Join(", ", Enum.GetNames<Tasks.Merge.JoinType>()));
                return false;
            }

            // Join 키 컬럼 검증
            foreach (var column in JoinKeyColumns)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    logger.LogError("Join key column name cannot be empty");
                    return false;
                }
            }
        }

        return true;
    }

    public override string? GetExample() =>
        "merge file1.csv file2.csv -t Vertical -o merged.csv\n" +
        "merge customers1.csv customers2.csv -t Horizontal -k \"CustomerID\" -j Left -o merged.csv";
}