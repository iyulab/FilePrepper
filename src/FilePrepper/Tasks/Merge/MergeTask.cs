using CsvHelper;

namespace FilePrepper.Tasks.Merge;

public class MergeTask : BaseTask<MergeOption>
{
    private HashSet<string> _allHeaders = [];
    private List<(List<Dictionary<string, string>> records, List<string> headers)> _allFilesData = new();

    public MergeTask(ILogger<MergeTask> logger) : base(logger)
    {
    }


    protected override async Task<List<Dictionary<string, string>>> PreProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // 첫 번째 파일 데이터를 저장
        _allFilesData.Add((records, _originalHeaders));

        // 나머지 파일들을 읽어서 저장
        for (int i = 1; i < Options.InputPaths.Count; i++)
        {
            var (otherRecords, headers) = await ReadCsvFileAsync(Options.InputPaths[i]);
            _allFilesData.Add((otherRecords, headers));
        }

        return records; // 원본 records 반환
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        try
        {
            List<Dictionary<string, string>> allRecords;

            if (Options.MergeType == MergeType.Vertical)
            {
                allRecords = await MergeVerticalAsync();
            }
            else
            {
                allRecords = await MergeHorizontalAsync();
            }

            if (allRecords == null || !allRecords.Any())
            {
                throw new ValidationException("No records were produced during merge operation.", ValidationExceptionErrorCode.General);
            }

            return allRecords;
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException(ex.Message, ValidationExceptionErrorCode.General);
        }
    }

    private Task<List<Dictionary<string, string>>> MergeVerticalAsync()
    {
        var allRecords = new List<Dictionary<string, string>>();
        _allHeaders = new HashSet<string>();
        int? expectedColumnCount = null;

        foreach (var (records, headers) in _allFilesData)
        {
            // 세로 병합 시에는 모든 파일의 열 개수가 동일해야 함
            if (expectedColumnCount == null)
            {
                expectedColumnCount = headers.Count;
                _logger.LogDebug("Set expected column count to {Count}", expectedColumnCount);
            }
            else if (headers.Count != expectedColumnCount)
            {
                throw new ValidationException(
                    $"Column count mismatch. Expected: {expectedColumnCount}, Actual: {headers.Count}",
                    ValidationExceptionErrorCode.General);
            }

            headers.ForEach(h => _allHeaders.Add(h));
            _logger.LogDebug("Current headers: {Headers}", string.Join(", ", _allHeaders));

            allRecords.AddRange(records);
            _logger.LogDebug("Total records after merge: {Count}", allRecords.Count);
        }

        _logger.LogInformation("Vertical merge completed. Total records: {Count}", allRecords.Count);
        return Task.FromResult(allRecords);
    }

    private Task<List<Dictionary<string, string>>> MergeHorizontalAsync()
    {
        // 첫 번째 파일의 데이터 가져오기
        var (records, headers) = _allFilesData[0];
        var mergedRecords = records;
        _allHeaders = new HashSet<string>(headers);

        // JoinMappings가 있는 경우 (new approach with heterogeneous column names)
        if (Options.JoinMappings?.Count > 0)
        {
            for (int i = 1; i < _allFilesData.Count; i++)
            {
                var (rightRecords, rightHeaders) = _allFilesData[i];
                mergedRecords = JoinTwoSetsWithMapping(mergedRecords, rightRecords, rightHeaders);
            }
            return Task.FromResult(mergedRecords);
        }

        // Join Key가 있는 경우는 JoinType에 따라 Join 수행 (legacy approach)
        if (Options.JoinKeyColumns?.Count > 0)
        {
            ValidateJoinKeyColumns(headers);

            for (int i = 1; i < _allFilesData.Count; i++)
            {
                var (rightRecords, rightHeaders) = _allFilesData[i];
                ValidateJoinKeyColumns(rightHeaders);

                mergedRecords = JoinTwoSets(mergedRecords, rightRecords);
            }
            return Task.FromResult(mergedRecords);
        }

        // Join Key가 없는 경우는 단순히 열 추가 (행 개수가 같아야 함)
        for (int i = 1; i < _allFilesData.Count; i++)
        {
            var (rightRecords, rightHeaders) = _allFilesData[i];

            // 행 개수가 같은지 검증
            if (rightRecords.Count != mergedRecords.Count)
            {
                throw new ValidationException(
                    $"Row count mismatch in file {i + 1}. " +
                    $"Expected: {mergedRecords.Count}, Actual: {rightRecords.Count}",
                    ValidationExceptionErrorCode.General);
            }

            // 중복 컬럼 이름 처리
            var headerMapping = new Dictionary<string, string>();
            foreach (var header in rightHeaders)
            {
                var finalHeader = _allHeaders.Contains(header) ? GetUniqueHeader(header) : header;
                headerMapping[header] = finalHeader;
                _allHeaders.Add(finalHeader);
            }

            // 데이터 병합
            for (int j = 0; j < mergedRecords.Count; j++)
            {
                foreach (var kvp in headerMapping)
                {
                    var originalHeader = kvp.Key;
                    var newHeader = kvp.Value;
                    mergedRecords[j][newHeader] = rightRecords[j][originalHeader];
                }
            }
        }

        return Task.FromResult(mergedRecords);
    }

    // MergeTask는 필수 컬럼 검증이 필요 없으므로 빈 배열 반환
    protected override IEnumerable<string> GetRequiredColumns() => Array.Empty<string>();

    private List<Dictionary<string, string>> JoinTwoSets(
        List<Dictionary<string, string>> leftRecords,
        List<Dictionary<string, string>> rightRecords)
    {
        var result = new List<Dictionary<string, string>>();

        // 조인 키 값을 문자열로 조합하여 비교
        string GetKeyValue(Dictionary<string, string> record)
        {
            return string.Join("|", Options.JoinKeyColumns.Select(keyCol =>
            {
                if (keyCol.Name != null)
                {
                    return record.GetValueOrDefault(keyCol.Name, string.Empty);
                }
                else if (keyCol.Index.HasValue)
                {
                    var header = _allHeaders.ElementAt(keyCol.Index.Value);
                    return record.GetValueOrDefault(header, string.Empty);
                }
                return string.Empty;
            }));
        }

        // 오른쪽 레코드를 키로 인덱싱
        var rightDict = rightRecords.GroupBy(GetKeyValue)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 왼쪽 레코드를 기준으로 오른쪽과 조인
        foreach (var leftRecord in leftRecords)
        {
            var leftKey = GetKeyValue(leftRecord);

            if (rightDict.TryGetValue(leftKey, out var matchingRightRecords))
            {
                // 매칭되는 레코드가 있는 경우
                foreach (var rightRecord in matchingRightRecords)
                {
                    var joinedRecord = new Dictionary<string, string>(leftRecord);

                    // Join 키가 아닌 컬럼만 추가
                    foreach (var kvp in rightRecord)
                    {
                        var isJoinKey = Options.JoinKeyColumns.Any(keyCol =>
                            (keyCol.Name != null && keyCol.Name == kvp.Key) ||
                            (keyCol.Index.HasValue && _allHeaders.ElementAt(keyCol.Index.Value) == kvp.Key));

                        if (!isJoinKey)
                        {
                            joinedRecord[kvp.Key] = kvp.Value;
                        }
                    }
                    result.Add(joinedRecord);
                }
            }
            else if (Options.JoinType == JoinType.Left || Options.JoinType == JoinType.Full)
            {
                // LEFT/FULL OUTER JOIN에서 매칭되는 레코드가 없는 경우
                var joinedRecord = new Dictionary<string, string>(leftRecord);
                foreach (var header in rightRecords.FirstOrDefault()?.Keys ?? Enumerable.Empty<string>())
                {
                    var isJoinKey = Options.JoinKeyColumns.Any(keyCol =>
                        (keyCol.Name != null && keyCol.Name == header) ||
                        (keyCol.Index.HasValue && _allHeaders.ElementAt(keyCol.Index.Value) == header));

                    if (!isJoinKey)
                    {
                        joinedRecord[header] = string.Empty;
                    }
                }
                result.Add(joinedRecord);
            }
        }

        // RIGHT/FULL OUTER JOIN의 경우 왼쪽에 매칭되지 않은 오른쪽 레코드도 추가
        if (Options.JoinType == JoinType.Right || Options.JoinType == JoinType.Full)
        {
            var processedKeys = result.Select(GetKeyValue).ToHashSet();

            foreach (var rightRecord in rightRecords)
            {
                var rightKey = GetKeyValue(rightRecord);
                if (!processedKeys.Contains(rightKey))
                {
                    var joinedRecord = new Dictionary<string, string>();

                    // 왼쪽 레코드의 모든 컬럼을 빈 값으로 설정
                    foreach (var header in leftRecords.FirstOrDefault()?.Keys ?? Enumerable.Empty<string>())
                    {
                        joinedRecord[header] = string.Empty;
                    }

                    // 오른쪽 레코드의 값을 추가
                    foreach (var kvp in rightRecord)
                    {
                        joinedRecord[kvp.Key] = kvp.Value;
                    }
                    result.Add(joinedRecord);
                }
            }
        }

        return result;
    }

    private void ValidateJoinKeyColumns(List<string> headers)
    {
        foreach (var keyCol in Options.JoinKeyColumns)
        {
            if (keyCol.Name != null)
            {
                if (!headers.Contains(keyCol.Name))
                {
                    throw new ValidationException(
                        $"Join key column '{keyCol.Name}' not found in headers: {string.Join(", ", headers)}",
                        ValidationExceptionErrorCode.General);
                }
            }
            else if (keyCol.Index.HasValue)
            {
                if (keyCol.Index.Value >= headers.Count)
                {
                    throw new ValidationException(
                        $"Join key column index {keyCol.Index.Value} is out of range. Header count: {headers.Count}",
                        ValidationExceptionErrorCode.General);
                }
            }
        }
    }

    private string GetUniqueHeader(string baseHeader)
    {
        int suffix = 2;
        string newHeader = $"{baseHeader}_{suffix}";
        while (_allHeaders.Contains(newHeader))
        {
            suffix++;
            newHeader = $"{baseHeader}_{suffix}";
        }
        return newHeader;
    }

    private List<Dictionary<string, string>> JoinTwoSetsWithMapping(
        List<Dictionary<string, string>> leftRecords,
        List<Dictionary<string, string>> rightRecords,
        List<string> rightHeaders)
    {
        var result = new List<Dictionary<string, string>>();

        // Build join key for left and right records using JoinMappings
        string GetLeftKeyValue(Dictionary<string, string> record)
        {
            return string.Join("|", Options.JoinMappings.Select(mapping =>
            {
                if (mapping.LeftColumn.Name != null)
                {
                    return record.GetValueOrDefault(mapping.LeftColumn.Name, string.Empty);
                }
                else if (mapping.LeftColumn.Index.HasValue)
                {
                    var header = _allHeaders.ElementAt(mapping.LeftColumn.Index.Value);
                    return record.GetValueOrDefault(header, string.Empty);
                }
                return string.Empty;
            }));
        }

        string GetRightKeyValue(Dictionary<string, string> record)
        {
            return string.Join("|", Options.JoinMappings.Select(mapping =>
            {
                if (mapping.RightColumn.Name != null)
                {
                    return record.GetValueOrDefault(mapping.RightColumn.Name, string.Empty);
                }
                else if (mapping.RightColumn.Index.HasValue)
                {
                    var header = rightHeaders.ElementAt(mapping.RightColumn.Index.Value);
                    return record.GetValueOrDefault(header, string.Empty);
                }
                return string.Empty;
            }));
        }

        // Index right records by key
        var rightDict = rightRecords.GroupBy(GetRightKeyValue)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get right join column names for exclusion
        var rightJoinColumnNames = Options.JoinMappings
            .Select(m => m.RightColumn.Name ?? (m.RightColumn.Index.HasValue ? rightHeaders.ElementAt(m.RightColumn.Index.Value) : null))
            .Where(name => name != null)
            .ToHashSet();

        // Join left records with right records
        foreach (var leftRecord in leftRecords)
        {
            var leftKey = GetLeftKeyValue(leftRecord);

            if (rightDict.TryGetValue(leftKey, out var matchingRightRecords))
            {
                // Matching records found
                foreach (var rightRecord in matchingRightRecords)
                {
                    var joinedRecord = new Dictionary<string, string>(leftRecord);

                    // Add output column name if specified, otherwise keep left column name
                    foreach (var mapping in Options.JoinMappings)
                    {
                        if (!string.IsNullOrEmpty(mapping.OutputColumnName))
                        {
                            // Use specified output column name
                            var leftColumnName = mapping.LeftColumn.Name ?? _allHeaders.ElementAt(mapping.LeftColumn.Index!.Value);
                            var leftValue = leftRecord.GetValueOrDefault(leftColumnName, string.Empty);

                            // Remove original left column and add with new name
                            joinedRecord.Remove(leftColumnName);
                            joinedRecord[mapping.OutputColumnName] = leftValue;
                        }
                    }

                    // Add non-join-key columns from right record
                    foreach (var kvp in rightRecord)
                    {
                        if (!rightJoinColumnNames.Contains(kvp.Key))
                        {
                            // Handle column name conflicts
                            var finalColumnName = joinedRecord.ContainsKey(kvp.Key) ? GetUniqueHeader(kvp.Key) : kvp.Key;
                            joinedRecord[finalColumnName] = kvp.Value;
                            _allHeaders.Add(finalColumnName);
                        }
                    }

                    result.Add(joinedRecord);
                }
            }
            else if (Options.JoinType == JoinType.Left || Options.JoinType == JoinType.Full)
            {
                // LEFT/FULL OUTER JOIN: no matching records
                var joinedRecord = new Dictionary<string, string>(leftRecord);

                // Apply output column name mapping if specified
                foreach (var mapping in Options.JoinMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.OutputColumnName))
                    {
                        var leftColumnName = mapping.LeftColumn.Name ?? _allHeaders.ElementAt(mapping.LeftColumn.Index!.Value);
                        var leftValue = leftRecord.GetValueOrDefault(leftColumnName, string.Empty);
                        joinedRecord.Remove(leftColumnName);
                        joinedRecord[mapping.OutputColumnName] = leftValue;
                    }
                }

                // Add empty values for right columns
                foreach (var header in rightHeaders)
                {
                    if (!rightJoinColumnNames.Contains(header))
                    {
                        var finalColumnName = joinedRecord.ContainsKey(header) ? GetUniqueHeader(header) : header;
                        joinedRecord[finalColumnName] = string.Empty;
                        _allHeaders.Add(finalColumnName);
                    }
                }

                result.Add(joinedRecord);
            }
        }

        // RIGHT/FULL OUTER JOIN: add unmatched right records
        if (Options.JoinType == JoinType.Right || Options.JoinType == JoinType.Full)
        {
            var processedKeys = new HashSet<string>();
            foreach (var leftRecord in leftRecords)
            {
                var leftKey = GetLeftKeyValue(leftRecord);
                if (rightDict.TryGetValue(leftKey, out _))
                {
                    processedKeys.Add(leftKey);
                }
            }

            foreach (var rightRecord in rightRecords)
            {
                var rightKey = GetRightKeyValue(rightRecord);
                if (!processedKeys.Contains(rightKey))
                {
                    var joinedRecord = new Dictionary<string, string>();

                    // Add empty values for left columns
                    foreach (var header in _allHeaders)
                    {
                        joinedRecord[header] = string.Empty;
                    }

                    // Add right record values
                    foreach (var kvp in rightRecord)
                    {
                        if (!rightJoinColumnNames.Contains(kvp.Key))
                        {
                            var finalColumnName = joinedRecord.ContainsKey(kvp.Key) ? GetUniqueHeader(kvp.Key) : kvp.Key;
                            joinedRecord[finalColumnName] = kvp.Value;
                            _allHeaders.Add(finalColumnName);
                        }
                        else
                        {
                            // For join columns, use output column name if specified
                            var mapping = Options.JoinMappings.FirstOrDefault(m =>
                                m.RightColumn.Name == kvp.Key ||
                                (m.RightColumn.Index.HasValue && rightHeaders.ElementAt(m.RightColumn.Index.Value) == kvp.Key));

                            if (mapping != null && !string.IsNullOrEmpty(mapping.OutputColumnName))
                            {
                                joinedRecord[mapping.OutputColumnName] = kvp.Value;
                            }
                        }
                    }

                    result.Add(joinedRecord);
                }
            }
        }

        return result;
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();

        // Merge 특화 검증 로직
        if (Options.MergeType == MergeType.Horizontal &&
            Options.JoinKeyColumns?.Count > 0)
        {
            foreach (var keyCol in Options.JoinKeyColumns)
            {
                if (!keyCol.IsValid)
                {
                    errors.Add($"Invalid join key column configuration");
                }
            }
        }

        return [.. errors];
    }
}