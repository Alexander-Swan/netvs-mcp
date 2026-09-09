using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NetVsMcp.Broker.Analytics;

internal static class SqliteUsageSchema
{
    private const int CurrentSchemaVersion = 1;

    public static void Ensure(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        var version = GetUserVersion(connection, transaction);
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Analytics database schema version {version} is newer than this broker supports.");
        }

        if (version < 1)
        {
            ApplyMigration001(connection, transaction);
        }

        transaction.Commit();
    }

    private static int GetUserVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void ApplyMigration001(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_daily (
              date TEXT NOT NULL,
              app_version TEXT NOT NULL,
              tool_name TEXT NOT NULL,
              category TEXT NOT NULL,
              endpoint TEXT NOT NULL,
              success INTEGER NOT NULL,
              call_count INTEGER NOT NULL DEFAULT 0,
              request_chars INTEGER NOT NULL DEFAULT 0,
              response_chars INTEGER NOT NULL DEFAULT 0,
              estimated_request_tokens INTEGER NOT NULL DEFAULT 0,
              estimated_response_tokens INTEGER NOT NULL DEFAULT 0,
              estimated_total_tokens INTEGER NOT NULL DEFAULT 0,
              duration_ms_total INTEGER NOT NULL DEFAULT 0,
              duration_ms_max INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (date, app_version, tool_name, category, endpoint, success)
            );

            CREATE INDEX IF NOT EXISTS ix_usage_daily_date ON usage_daily(date);
            CREATE INDEX IF NOT EXISTS ix_usage_daily_app_version ON usage_daily(app_version);
            CREATE INDEX IF NOT EXISTS ix_usage_daily_tool_name ON usage_daily(tool_name);
            CREATE INDEX IF NOT EXISTS ix_usage_daily_category ON usage_daily(category);
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
    }
}
