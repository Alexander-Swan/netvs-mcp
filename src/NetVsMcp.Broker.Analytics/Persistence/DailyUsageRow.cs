namespace NetVsMcp.Broker.Analytics;

internal sealed record DailyUsageRow(
    DateTime Date,
    string AppVersion,
    string ToolName,
    string Category,
    string Endpoint,
    bool Success,
    long CallCount,
    long RequestChars,
    long ResponseChars,
    long EstimatedRequestTokens,
    long EstimatedResponseTokens,
    long EstimatedTotalTokens,
    long DurationMsTotal,
    long DurationMsMax);
