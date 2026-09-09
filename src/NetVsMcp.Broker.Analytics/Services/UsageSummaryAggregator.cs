using System.Globalization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Analytics;

internal static class UsageSummaryAggregator
{
    public static IReadOnlyCollection<ToolUsageSummaryRow> Aggregate(
        IEnumerable<DailyUsageRow> rows,
        string groupBy)
    {
        return rows
            .GroupBy(row => CreateGroupKey(row, groupBy))
            .Select(group => CreateSummaryRow(group.Key, group))
            .OrderBy(row => row.Bucket ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.ToolName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.Category ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.AppVersion ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static UsageGroupKey CreateGroupKey(DailyUsageRow row, string groupBy)
    {
        var bucket = groupBy switch
        {
            "day" or "tool_day" or "category_day" => row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "week" or "tool_week" or "category_week" => CreateIsoWeekBucket(row.Date),
            "month" or "tool_month" or "category_month" => row.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => null
        };

        var toolName = groupBy is "tool" or "tool_day" or "tool_week" or "tool_month" or "tool_version"
            ? row.ToolName
            : null;
        var category = groupBy is "category" or "category_day" or "category_week" or "category_month" or "category_version"
            ? row.Category
            : null;
        var appVersion = groupBy is "version" or "tool_version" or "category_version"
            ? row.AppVersion
            : null;

        return new UsageGroupKey(bucket, appVersion, toolName, category);
    }

    private static string CreateIsoWeekBucket(DateTime date)
    {
        var week = ISOWeek.GetWeekOfYear(date);
        var year = ISOWeek.GetYear(date);
        return FormattableString.Invariant($"{year}-W{week:00}");
    }

    private static ToolUsageSummaryRow CreateSummaryRow(
        UsageGroupKey key,
        IEnumerable<DailyUsageRow> rows)
    {
        var materialized = rows.ToArray();
        var callCount = materialized.Sum(row => row.CallCount);
        var successCount = materialized.Where(row => row.Success).Sum(row => row.CallCount);
        var failureCount = materialized.Where(row => !row.Success).Sum(row => row.CallCount);
        var requestChars = materialized.Sum(row => row.RequestChars);
        var responseChars = materialized.Sum(row => row.ResponseChars);
        var requestTokens = materialized.Sum(row => row.EstimatedRequestTokens);
        var responseTokens = materialized.Sum(row => row.EstimatedResponseTokens);
        var totalTokens = materialized.Sum(row => row.EstimatedTotalTokens);
        var durationTotal = materialized.Sum(row => row.DurationMsTotal);
        var durationMax = materialized.Length == 0 ? 0 : materialized.Max(row => row.DurationMsMax);

        return new ToolUsageSummaryRow(
            key.Bucket,
            key.AppVersion,
            key.ToolName,
            key.Category,
            callCount,
            successCount,
            failureCount,
            requestChars,
            responseChars,
            requestTokens,
            responseTokens,
            totalTokens,
            durationTotal,
            durationMax,
            Average(requestChars, callCount),
            Average(responseChars, callCount),
            Average(requestTokens, callCount),
            Average(responseTokens, callCount),
            Average(totalTokens, callCount),
            Average(durationTotal, callCount));
    }

    private static double Average(long total, long count) =>
        count == 0 ? 0 : Math.Round((double)total / count, 2);

    private sealed record UsageGroupKey(
        string? Bucket,
        string? AppVersion,
        string? ToolName,
        string? Category);
}
