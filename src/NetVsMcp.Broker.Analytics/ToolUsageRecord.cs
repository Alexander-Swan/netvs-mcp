using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Analytics;

public sealed record ToolUsageRecord(
    DateTimeOffset TimestampUtc,
    string AppVersion,
    string ToolName,
    BrokerToolCategory Category,
    string Endpoint,
    bool Success,
    long RequestChars,
    long ResponseChars,
    long EstimatedRequestTokens,
    long EstimatedResponseTokens,
    long EstimatedTotalTokens,
    long DurationMs);
