namespace NetVsMcp.Contracts;

public sealed record ToolUsageSummaryQuery(
    string? FromDate = null,
    string? ToDate = null,
    string GroupBy = "day",
    string? ToolName = null,
    string? Category = null,
    string? AppVersion = null,
    bool IncludeFailures = true);

public sealed record ToolUsageSummaryResult(
    bool AnalyticsEnabled,
    int? RetentionDays,
    bool PruningEnabled,
    string DatabasePath,
    string GroupBy,
    string? FromDate,
    string? ToDate,
    IReadOnlyCollection<ToolUsageSummaryRow> Rows);

public sealed record ToolUsageSummaryRow(
    string? Bucket,
    string? AppVersion,
    string? ToolName,
    string? Category,
    long CallCount,
    long SuccessCount,
    long FailureCount,
    long RequestCharsTotal,
    long ResponseCharsTotal,
    long EstimatedRequestTokensTotal,
    long EstimatedResponseTokensTotal,
    long EstimatedTotalTokensTotal,
    long DurationMsTotal,
    long DurationMsMax,
    double AverageRequestChars,
    double AverageResponseChars,
    double AverageEstimatedRequestTokens,
    double AverageEstimatedResponseTokens,
    double AverageEstimatedTotalTokens,
    double AverageDurationMs);
