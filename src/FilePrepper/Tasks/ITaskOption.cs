namespace FilePrepper.Tasks;

public interface ITaskOption
{
    string OutputPath { get; set; }
    bool HasHeader { get; set; }
    bool IgnoreErrors { get; set; }
    string Encoding { get; set; }
    int SkipRows { get; set; }
    bool IsValid { get; }
    string[] Validate();
}

// DefaultValue 패턴이 필요한 옵션들을 위한 인터페이스
public interface IDefaultValueOption
{
    string? DefaultValue { get; set; }
}

// AppendToSource 패턴이 필요한 옵션들을 위한 인터페이스 
public interface IAppendableOption
{
    bool AppendToSource { get; set; }
    string? OutputColumnTemplate { get; set; }
}