using System.Globalization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Analytics;

internal sealed record ValidatedToolUsageSummaryQuery(
    string GroupBy,
    DateTime? FromDate,
    DateTime? ToDate,
    string? ToolName,
    string? Category,
    string? AppVersion,
    bool IncludeFailures);

internal static class UsageSummaryQuery
{
    public static ValidatedToolUsageSummaryQuery Validate(ToolUsageSummaryQuery query)
    {
        var groupBy = NormalizeGroupBy(query.GroupBy);
        var fromDate = ParseDate(query.FromDate, nameof(query.FromDate));
        var toDate = ParseDate(query.ToDate, nameof(query.ToDate));
        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            throw new ArgumentException("fromDate must be earlier than or equal to toDate.");
        }

        return new ValidatedToolUsageSummaryQuery(
            groupBy,
            fromDate,
            toDate,
            NormalizeOptional(query.ToolName),
            NormalizeOptional(query.Category),
            NormalizeOptional(query.AppVersion),
            query.IncludeFailures);
    }

    private static DateTime? ParseDate(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new ArgumentException($"{parameterName} must use yyyy-MM-dd format.");
    }

    private static string NormalizeGroupBy(string? value)
    {
        var groupBy = string.IsNullOrWhiteSpace(value) ? "day" : value.Trim().Replace('-', '_').ToLowerInvariant();
        return groupBy switch
        {
            "day" or "week" or "month" or "tool" or "category" or "version" or
            "tool_day" or "tool_week" or "tool_month" or
            "category_day" or "category_week" or "category_month" or
            "tool_version" or "category_version" => groupBy,
            _ => throw new ArgumentException("groupBy must be one of: day, week, month, tool, category, version, tool_day, tool_week, tool_month, category_day, category_week, category_month, tool_version, category_version.")
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
