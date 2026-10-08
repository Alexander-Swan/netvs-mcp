using NetVsMcp.Broker.Analytics;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class DisabledToolUsageAnalyticsServiceTests
{
    [Fact]
    public void Query_ReturnsMetadataWithoutRows()
    {
        var service = new DisabledToolUsageAnalyticsService("analytics.db");

        var result = service.Query(
            new ToolUsageSummaryQuery(
                FromDate: "2026-09-01",
                ToDate: "2026-09-30",
                GroupBy: "tool",
                ToolName: "document_read"),
            analyticsEnabled: false,
            retentionDays: 30);

        Assert.False(result.AnalyticsEnabled);
        Assert.False(result.PruningEnabled);
        Assert.Equal(30, result.RetentionDays);
        Assert.Equal("analytics.db", result.DatabasePath);
        Assert.Equal("tool", result.GroupBy);
        Assert.Equal("2026-09-01", result.FromDate);
        Assert.Equal("2026-09-30", result.ToDate);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Query_DefaultsBlankGroupByToDay()
    {
        var service = new DisabledToolUsageAnalyticsService("analytics.db");

        var result = service.Query(
            new ToolUsageSummaryQuery(GroupBy: " "),
            analyticsEnabled: true,
            retentionDays: null);

        Assert.Equal("day", result.GroupBy);
        Assert.False(result.PruningEnabled);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(7, true)]
    public void Query_ReportsPruningOnlyWhenAnalyticsAndRetentionAreEnabled(int? retentionDays, bool expected)
    {
        var service = new DisabledToolUsageAnalyticsService("analytics.db");

        var result = service.Query(
            new ToolUsageSummaryQuery(),
            analyticsEnabled: true,
            retentionDays: retentionDays);

        Assert.Equal(expected, result.PruningEnabled);
    }

    [Fact]
    public void RecordAndPruneAreNoOps()
    {
        var service = new DisabledToolUsageAnalyticsService("analytics.db");
        var record = new ToolUsageRecord(
            DateTimeOffset.Parse("2026-09-09T12:00:00Z"),
            "1.6.1-dev",
            "document_read",
            BrokerToolCategory.Read,
            McpEndpointRouting.DefaultEndpointPath,
            true,
            4,
            8,
            1,
            2,
            3,
            10);

        service.Record(record);

        Assert.Equal(0, service.PruneOldBuckets(1));
        Assert.Empty(service.Query(new ToolUsageSummaryQuery(), analyticsEnabled: false, retentionDays: null).Rows);
    }
}
