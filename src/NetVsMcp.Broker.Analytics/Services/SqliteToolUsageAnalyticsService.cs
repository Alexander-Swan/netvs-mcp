using System.Globalization;
using Microsoft.Data.Sqlite;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Analytics;

internal sealed class SqliteToolUsageAnalyticsService : IToolUsageAnalyticsService
{
    private readonly object _gate = new();

    public SqliteToolUsageAnalyticsService(string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Analytics database path is required.", nameof(databasePath));
        }

        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public void Record(ToolUsageRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.AppVersion))
        {
            throw new InvalidOperationException("Broker version is required for usage analytics.");
        }

        lock (_gate)
        {
            using var connection = OpenInitializedConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO usage_daily (
                  date,
                  app_version,
                  tool_name,
                  category,
                  endpoint,
                  success,
                  call_count,
                  request_chars,
                  response_chars,
                  estimated_request_tokens,
                  estimated_response_tokens,
                  estimated_total_tokens,
                  duration_ms_total,
                  duration_ms_max)
                VALUES (
                  $date,
                  $appVersion,
                  $toolName,
                  $category,
                  $endpoint,
                  $success,
                  1,
                  $requestChars,
                  $responseChars,
                  $estimatedRequestTokens,
                  $estimatedResponseTokens,
                  $estimatedTotalTokens,
                  $durationMs,
                  $durationMs)
                ON CONFLICT(date, app_version, tool_name, category, endpoint, success)
                DO UPDATE SET
                  call_count = call_count + excluded.call_count,
                  request_chars = request_chars + excluded.request_chars,
                  response_chars = response_chars + excluded.response_chars,
                  estimated_request_tokens = estimated_request_tokens + excluded.estimated_request_tokens,
                  estimated_response_tokens = estimated_response_tokens + excluded.estimated_response_tokens,
                  estimated_total_tokens = estimated_total_tokens + excluded.estimated_total_tokens,
                  duration_ms_total = duration_ms_total + excluded.duration_ms_total,
                  duration_ms_max = max(duration_ms_max, excluded.duration_ms_max);
                """;
            command.Parameters.AddWithValue("$date", record.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$appVersion", record.AppVersion);
            command.Parameters.AddWithValue("$toolName", record.ToolName);
            command.Parameters.AddWithValue("$category", record.Category.ToString());
            command.Parameters.AddWithValue("$endpoint", record.Endpoint);
            command.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
            command.Parameters.AddWithValue("$requestChars", record.RequestChars);
            command.Parameters.AddWithValue("$responseChars", record.ResponseChars);
            command.Parameters.AddWithValue("$estimatedRequestTokens", record.EstimatedRequestTokens);
            command.Parameters.AddWithValue("$estimatedResponseTokens", record.EstimatedResponseTokens);
            command.Parameters.AddWithValue("$estimatedTotalTokens", record.EstimatedTotalTokens);
            command.Parameters.AddWithValue("$durationMs", record.DurationMs);
            command.ExecuteNonQuery();
        }
    }

    public ToolUsageSummaryResult Query(ToolUsageSummaryQuery query, bool analyticsEnabled, int? retentionDays)
    {
        var validated = UsageSummaryQuery.Validate(query);

        lock (_gate)
        {
            using var connection = OpenInitializedConnection();
            var rows = ReadRows(connection, validated);
            var summaries = UsageSummaryAggregator.Aggregate(rows, validated.GroupBy);

            return new ToolUsageSummaryResult(
                analyticsEnabled,
                retentionDays,
                analyticsEnabled && retentionDays is > 0,
                DatabasePath,
                validated.GroupBy,
                query.FromDate,
                query.ToDate,
                summaries);
        }
    }

    public int PruneOldBuckets(int retentionDays, DateTimeOffset? now = null)
    {
        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be greater than zero.");
        }

        var today = (now ?? DateTimeOffset.UtcNow).UtcDateTime.Date;
        var cutoff = today.AddDays(1 - retentionDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        lock (_gate)
        {
            using var connection = OpenInitializedConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM usage_daily WHERE date < $cutoffDate;";
            command.Parameters.AddWithValue("$cutoffDate", cutoff);
            return command.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenInitializedConnection()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();
        SqliteUsageSchema.Ensure(connection);
        return connection;
    }

    private static IReadOnlyCollection<DailyUsageRow> ReadRows(
        SqliteConnection connection,
        ValidatedToolUsageSummaryQuery query)
    {
        var conditions = new List<string>();
        using var command = connection.CreateCommand();
        AddDateFilter(command, conditions, "date >= $fromDate", "$fromDate", query.FromDate);
        AddDateFilter(command, conditions, "date <= $toDate", "$toDate", query.ToDate);
        AddTextFilter(command, conditions, "tool_name = $toolName", "$toolName", query.ToolName);
        AddTextFilter(command, conditions, "category = $category", "$category", query.Category);
        AddTextFilter(command, conditions, "app_version = $appVersion", "$appVersion", query.AppVersion);

        if (!query.IncludeFailures)
        {
            conditions.Add("success = 1");
        }

        command.CommandText = $"""
            SELECT date, app_version, tool_name, category, endpoint, success, call_count,
                   request_chars, response_chars, estimated_request_tokens,
                   estimated_response_tokens, estimated_total_tokens,
                   duration_ms_total, duration_ms_max
            FROM usage_daily
            {(conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions))}
            ORDER BY date, tool_name, category, app_version;
            """;

        var rows = new List<DailyUsageRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static void AddDateFilter(
        SqliteCommand command,
        ICollection<string> conditions,
        string condition,
        string parameterName,
        DateTime? value)
    {
        if (value is null)
        {
            return;
        }

        conditions.Add(condition);
        command.Parameters.AddWithValue(parameterName, value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static void AddTextFilter(
        SqliteCommand command,
        ICollection<string> conditions,
        string condition,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        conditions.Add(condition);
        command.Parameters.AddWithValue(parameterName, value);
    }

    private static DailyUsageRow ReadRow(SqliteDataReader reader) =>
        new(
            DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5) != 0,
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.GetInt64(9),
            reader.GetInt64(10),
            reader.GetInt64(11),
            reader.GetInt64(12),
            reader.GetInt64(13));
}
