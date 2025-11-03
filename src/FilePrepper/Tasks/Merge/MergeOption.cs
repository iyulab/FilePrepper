using CsvHelper;

namespace FilePrepper.Tasks.Merge;

public enum MergeType
{
    /// <summary>
    /// 세로로 머지 (union). 모든 컬럼 통합, 중복 열은 그대로 유지
    /// </summary>
    Vertical,

    /// <summary>
    /// 가로로 머지 (join). 지정된 키 컬럼을 기준으로 데이터 결합
    /// </summary>
    Horizontal
}

public enum JoinType
{
    /// <summary>
    /// 두 집합에서 키가 모두 있는 레코드만 결합 (INNER JOIN)
    /// </summary>
    Inner,

    /// <summary>
    /// 왼쪽 집합 기준, 오른쪽이 없어도 결합 (LEFT JOIN)
    /// </summary>
    Left,

    /// <summary>
    /// 오른쪽 집합 기준, 왼쪽이 없어도 결합 (RIGHT JOIN)
    /// </summary>
    Right,

    /// <summary>
    /// 키가 있든 없든 전부 결합 (FULL OUTER JOIN)
    /// </summary>
    Full
}

public class MergeOption : MultipleInputOption
{
    public MergeType MergeType { get; set; } = MergeType.Vertical;
    public JoinType JoinType { get; set; } = JoinType.Inner;
    public List<ColumnIdentifier> JoinKeyColumns { get; set; } = new();
    public bool StrictColumnCount { get; set; }

    // 컬럼 매핑 정보를 저장하기 위한 속성 추가
    public Dictionary<string, List<string>> ColumnMappings { get; private set; } = new();

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        // 1) 기본 입력 파일 검증
        if (!ValidateBasicInputs(errors))
            return errors.ToArray();

        // 2) 파일 구조 검증 (행/열 수) - 헤더 검증보다 먼저 수행
        ValidateFileStructure(errors);

        // 파일 구조에 문제가 있다면 추가 검증 중단
        if (errors.Any())
            return errors.ToArray();

        // 3) Join Key 검증
        ValidateJoinKeys(errors);

        // 4) Header 검증 - 파일 구조 검증 후 수행
        if (!ValidateHeaders(errors))
            return errors.ToArray();

        return errors.ToArray();
    }

    private void ValidateFileStructure(List<string> errors)
    {
        try
        {
            var (primaryColCount, primaryRowCount) = GetFileStats(InputPaths.First());

            // 세로 병합의 경우, 열 개수 검증을 먼저 수행
            if (MergeType == MergeType.Vertical)
            {
                foreach (var file in InputPaths.Skip(1))
                {
                    var (colCount, _) = GetFileStats(file);
                    if (colCount != primaryColCount)
                    {
                        errors.Add($"Column count mismatch for vertical merge. Primary file has {primaryColCount} columns, but {Path.GetFileName(file)} has {colCount} columns.");
                    }
                }
            }
            else if (MergeType == MergeType.Horizontal)
            {
                foreach (var file in InputPaths.Skip(1))
                {
                    var (_, rowCount) = GetFileStats(file);
                    bool isSimpleMerge = !JoinKeyColumns.Any();

                    // Simple horizontal merge (키 없음): 행 수가 같아야 함
                    if (isSimpleMerge && rowCount != primaryRowCount)
                    {
                        errors.Add($"Row count mismatch for horizontal merge. Primary file has {primaryRowCount} rows, but {Path.GetFileName(file)} has {rowCount} rows.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating file structure: {ex.Message}");
        }
    }

    private bool ValidateBasicInputs(List<string> errors)
    {
        if (InputPaths == null || InputPaths.Count < 2)
        {
            errors.Add("At least two input files must be specified for merging.");
            return false;
        }

        foreach (var (path, index) in InputPaths.Select((p, i) => (p, i)))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add($"Input path at index {index} cannot be empty or whitespace.");
            }
            else if (!File.Exists(path))
            {
                errors.Add($"Input file does not exist: {path}");
            }
        }

        return !errors.Any();
    }

    private void ValidateJoinKeys(List<string> errors)
    {
        if (JoinKeyColumns?.Count > 0)
        {
            if (MergeType != MergeType.Horizontal)
            {
                errors.Add("Join key columns can only be specified for horizontal merge.");
            }
            else
            {
                foreach (var (keyCol, index) in JoinKeyColumns.Select((k, i) => (k, i)))
                {
                    if (!keyCol.IsValid)
                    {
                        errors.Add($"Join key column at index {index} must specify either Name or Index.");
                    }
                    if (keyCol.Index.HasValue && keyCol.Index.Value < 0)
                    {
                        errors.Add($"Join key column index at position {index} cannot be negative.");
                    }
                }
            }
        }
    }

    private bool ValidateHeaders(List<string> errors)
    {
        // 컬럼명으로 join하는 경우에만 헤더 필수
        bool requiresHeader = MergeType == MergeType.Horizontal &&
                            JoinKeyColumns.Any(k => k.Name != null);

        if (requiresHeader && !HasHeader)
        {
            errors.Add("Header is required when joining by column names.");
            return false;
        }

        // 헤더가 없는 경우는 추가 검증 불필요
        if (!HasHeader)
            return true;

        try
        {
            // 첫 번째 파일의 헤더 읽기
            var primaryHeaders = GetFileHeaders(InputPaths.First());

            // 중복된 헤더 검사
            var duplicateHeaders = primaryHeaders
                .GroupBy(h => h)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateHeaders.Any())
            {
                errors.Add($"Duplicate headers found in primary file: {string.Join(", ", duplicateHeaders)}");
                return false;
            }

            // 각 파일별 헤더 검증 및 매핑 생성
            foreach (var file in InputPaths.Skip(1))
            {
                var currentHeaders = GetFileHeaders(file);

                // 현재 파일의 중복 헤더 검사
                duplicateHeaders = currentHeaders
                    .GroupBy(h => h)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateHeaders.Any())
                {
                    errors.Add($"Duplicate headers found in file {Path.GetFileName(file)}: {string.Join(", ", duplicateHeaders)}");
                    continue;
                }

                // Vertical merge의 경우 헤더가 동일해야 함
                if (MergeType == MergeType.Vertical)
                {
                    var missingHeaders = primaryHeaders.Except(currentHeaders).ToList();
                    var extraHeaders = currentHeaders.Except(primaryHeaders).ToList();

                    if (missingHeaders.Any() || extraHeaders.Any())
                    {
                        errors.Add($"Headers mismatch in file {Path.GetFileName(file)}. " +
                                 $"Missing headers: {string.Join(", ", missingHeaders)}. " +
                                 $"Extra headers: {string.Join(", ", extraHeaders)}");
                    }
                }
                // Horizontal merge의 경우 join key 검증
                else if (MergeType == MergeType.Horizontal)
                {
                    foreach (var key in JoinKeyColumns.Where(k => k.Name != null))
                    {
                        if (!primaryHeaders.Contains(key.Name!))
                            errors.Add($"Join key column '{key.Name}' not found in primary file");
                        if (!currentHeaders.Contains(key.Name!))
                            errors.Add($"Join key column '{key.Name}' not found in file {Path.GetFileName(file)}");
                    }
                }

                // 헤더 매핑 정보 저장
                ColumnMappings[file] = currentHeaders;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating headers: {ex.Message}");
            return false;
        }

        return !errors.Any();
    }

    private List<string> GetFileHeaders(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();
        return csv.HeaderRecord?.ToList() ?? new List<string>();
    }

    private (int ColumnCount, int RowCount) GetFileStats(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Get column count from first row
        csv.Read();
        var columnCount = csv.Parser.Count;

        // Count remaining rows
        var rowCount = HasHeader ? 0 : 1; // If has header, start from 0, else count first row
        while (csv.Read())
        {
            rowCount++;
        }

        return (columnCount, rowCount);
    }
}