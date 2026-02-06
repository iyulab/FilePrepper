using FilePrepper.Tasks;

public abstract class BaseOption : ITaskOption
{
    public string OutputPath { get; set; } = string.Empty;
    public bool HasHeader { get; set; } = true;
    public bool IgnoreErrors { get; set; }
    public string Encoding { get; set; } = "auto";
    public int SkipRows { get; set; } = 0;

    public bool IsValid => Validate().Length == 0;

    public virtual string[] Validate()
    {
        var errors = new List<string>();
        errors.AddRange(ValidateCommon());
        errors.AddRange(ValidateInternal());
        return [.. errors];
    }

    protected virtual string[] ValidateCommon()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            errors.Add("Output path cannot be empty");
        }

        if (SkipRows < 0)
        {
            errors.Add("SkipRows cannot be negative");
        }

        return [.. errors];
    }

    protected abstract string[] ValidateInternal();
}

public abstract class SingleInputOption : BaseOption
{
    public string InputPath { get; set; } = string.Empty;

    protected override string[] ValidateCommon()
    {
        var errors = new List<string>();
        errors.AddRange(base.ValidateCommon());

        if (string.IsNullOrWhiteSpace(InputPath))
        {
            errors.Add("Input path cannot be empty");
        }

        if (!File.Exists(InputPath))
        {
            errors.Add($"Input file does not exist: {InputPath}");
        }

        return [.. errors];
    }
}

public abstract class MultipleInputOption : BaseOption
{
    public List<string> InputPaths { get; set; } = [];

    protected override string[] ValidateCommon()
    {
        var errors = new List<string>();
        errors.AddRange(base.ValidateCommon());

        if (InputPaths == null || InputPaths.Count == 0)
        {
            errors.Add("At least one input path must be specified");
            return [.. errors];
        }

        foreach (var (path, index) in InputPaths.Select((p, i) => (p, i)))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add($"Input path at index {index} cannot be empty");
            }
            else if (!File.Exists(path))
            {
                errors.Add($"Input file does not exist: {path}");
            }
        }

        return [.. errors];
    }
}