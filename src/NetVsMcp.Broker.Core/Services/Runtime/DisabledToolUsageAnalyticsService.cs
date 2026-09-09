using NetVsMcp.Broker.Analytics;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class DisabledToolUsageAnalyticsService : IToolUsageAnalyticsService
{
    public DisabledToolUsageAnalyticsService(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public void Record(ToolUsageRecord record)
    {
    }

    public ToolUsageSummaryResult Query(
        ToolUsageSummaryQuery query,
        bool analyticsEnabled,
        int? retentionDays) =>
        new(
            analyticsEnabled,
            retentionDays,
            analyticsEnabled && retentionDays is > 0,
            DatabasePath,
            string.IsNullOrWhiteSpace(query.GroupBy) ? "day" : query.GroupBy,
            query.FromDate,
            query.ToDate,
            []);

    public int PruneOldBuckets(int retentionDays, DateTimeOffset? now = null) => 0;
}
