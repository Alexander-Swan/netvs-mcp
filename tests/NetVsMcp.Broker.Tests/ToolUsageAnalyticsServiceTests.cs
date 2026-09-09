using NetVsMcp.Broker.Analytics;
using NetVsMcp.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Tests;

public sealed class ToolUsageAnalyticsServiceTests
{
    [Fact]
    public void Record_UpsertsMatchingDailyBucket()
    {
        var service = CreateService();
        var timestamp = DateTimeOffset.Parse("2026-09-09T12:00:00Z");

        service.Record(CreateRecord(timestamp, requestChars: 40, responseChars: 80, durationMs: 10));
        service.Record(CreateRecord(timestamp, requestChars: 20, responseChars: 40, durationMs: 25));

        var result = service.Query(new ToolUsageSummaryQuery(GroupBy: "tool"), analyticsEnabled: true, retentionDays: null);
        var row = Assert.Single(result.Rows);
        Assert.Equal("document_read", row.ToolName);
        Assert.Equal(2, row.CallCount);
        Assert.Equal(60, row.RequestCharsTotal);
        Assert.Equal(120, row.ResponseCharsTotal);
        Assert.Equal(15, row.EstimatedRequestTokensTotal);
        Assert.Equal(30, row.EstimatedResponseTokensTotal);
        Assert.Equal(25, row.DurationMsMax);
        Assert.Equal(30, row.AverageRequestChars);
        Assert.Equal(60, row.AverageResponseChars);
    }

    [Fact]
    public void Record_SplitsSuccessAndFailureCounts()
    {
        var service = CreateService();
        var timestamp = DateTimeOffset.Parse("2026-09-09T12:00:00Z");

        service.Record(CreateRecord(timestamp, success: true));
        service.Record(CreateRecord(timestamp, success: false));

        var result = service.Query(new ToolUsageSummaryQuery(GroupBy: "day"), analyticsEnabled: true, retentionDays: null);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.CallCount);
        Assert.Equal(1, row.SuccessCount);
        Assert.Equal(1, row.FailureCount);
    }

    [Fact]
    public void Query_GroupsByIsoWeekAndMonth()
    {
        var service = CreateService();
        service.Record(CreateRecord(DateTimeOffset.Parse("2026-01-01T12:00:00Z")));
        service.Record(CreateRecord(DateTimeOffset.Parse("2026-01-31T12:00:00Z")));

        var week = service.Query(new ToolUsageSummaryQuery(GroupBy: "week"), analyticsEnabled: true, retentionDays: null);
        Assert.Contains(week.Rows, row => row.Bucket == "2026-W01");
        Assert.Contains(week.Rows, row => row.Bucket == "2026-W05");

        var month = service.Query(new ToolUsageSummaryQuery(GroupBy: "month"), analyticsEnabled: true, retentionDays: null);
        var monthRow = Assert.Single(month.Rows);
        Assert.Equal("2026-01", monthRow.Bucket);
        Assert.Equal(2, monthRow.CallCount);
    }

    [Fact]
    public void Query_FiltersByToolCategoryAndVersion()
    {
        var service = CreateService();
        var timestamp = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        service.Record(CreateRecord(timestamp, toolName: "document_read", category: BrokerToolCategory.Read, appVersion: "1.6.1-dev"));
        service.Record(CreateRecord(timestamp, toolName: "build_solution", category: BrokerToolCategory.Build, appVersion: "1.6.2-dev"));

        var result = service.Query(
            new ToolUsageSummaryQuery(GroupBy: "version", ToolName: "document_read", Category: "Read", AppVersion: "1.6.1-dev"),
            analyticsEnabled: true,
            retentionDays: null);

        var row = Assert.Single(result.Rows);
        Assert.Equal("1.6.1-dev", row.AppVersion);
        Assert.Equal(1, row.CallCount);
    }

    [Fact]
    public void PruneOldBuckets_RemovesRowsOlderThanRetentionWindow()
    {
        var service = CreateService();
        service.Record(CreateRecord(DateTimeOffset.Parse("2026-09-06T12:00:00Z")));
        service.Record(CreateRecord(DateTimeOffset.Parse("2026-09-07T12:00:00Z")));
        service.Record(CreateRecord(DateTimeOffset.Parse("2026-09-09T12:00:00Z")));

        var removed = service.PruneOldBuckets(3, DateTimeOffset.Parse("2026-09-09T00:00:00Z"));

        Assert.Equal(1, removed);
        var result = service.Query(new ToolUsageSummaryQuery(GroupBy: "day"), analyticsEnabled: true, retentionDays: 3);
        Assert.DoesNotContain(result.Rows, row => row.Bucket == "2026-09-06");
        Assert.Contains(result.Rows, row => row.Bucket == "2026-09-07");
        Assert.Contains(result.Rows, row => row.Bucket == "2026-09-09");
    }

    [Fact]
    public void Record_RejectsMissingAppVersion()
    {
        var service = CreateService();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Record(CreateRecord(DateTimeOffset.Parse("2026-09-09T12:00:00Z"), appVersion: "")));

        Assert.Contains("version", exception.Message);
    }

    private static IToolUsageAnalyticsService CreateService()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "NetVsMcp.Broker.Tests",
            Guid.NewGuid().ToString("N"),
            "analytics.db");
        var services = new ServiceCollection();
        services.AddNetVsMcpBrokerAnalytics(path);
        return services.BuildServiceProvider().GetRequiredService<IToolUsageAnalyticsService>();
    }

    private static ToolUsageRecord CreateRecord(
        DateTimeOffset timestamp,
        string appVersion = "1.6.1-dev",
        string toolName = "document_read",
        BrokerToolCategory category = BrokerToolCategory.Read,
        bool success = true,
        long requestChars = 4,
        long responseChars = 8,
        long durationMs = 10)
    {
        var requestTokens = (long)Math.Ceiling(requestChars / 4.0);
        var responseTokens = (long)Math.Ceiling(responseChars / 4.0);
        return new ToolUsageRecord(
            timestamp,
            appVersion,
            toolName,
            category,
            McpEndpointRouting.DefaultEndpointPath,
            success,
            requestChars,
            responseChars,
            requestTokens,
            responseTokens,
            requestTokens + responseTokens,
            durationMs);
    }
}
