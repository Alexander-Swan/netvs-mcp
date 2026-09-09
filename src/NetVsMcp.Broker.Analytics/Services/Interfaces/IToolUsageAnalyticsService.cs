using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Analytics.Services.Interfaces;

public interface IToolUsageAnalyticsService
{
    string DatabasePath { get; }
    void Record(ToolUsageRecord record);
    ToolUsageSummaryResult Query(ToolUsageSummaryQuery query, bool analyticsEnabled, int? retentionDays);
    int PruneOldBuckets(int retentionDays, DateTimeOffset? now = null);
}
