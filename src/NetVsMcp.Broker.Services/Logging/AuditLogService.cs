using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed record AuditToolCall(
    DateTimeOffset TimestampUtc,
    string ToolName,
    bool Success,
    string? SessionId = null,
    string? SolutionName = null,
    string? SolutionPath = null,
    string? FailureReason = null,
    string? Message = null,
    BrokerLogLevel Level = BrokerLogLevel.Info);

internal sealed class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly object _gate = new();

    public AuditLogService(string logsDirectory)
    {
        LogsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? BrokerOptions.DefaultLogsDirectory
            : logsDirectory;
    }

    public string LogsDirectory { get; }

    public string CurrentLogFilePath =>
        Path.Combine(LogsDirectory, $"audit-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");

    public void RecordToolCall(AuditToolCall entry)
    {
        Directory.CreateDirectory(LogsDirectory);

        var line = JsonSerializer.Serialize(entry, SerializerOptions);
        lock (_gate)
        {
            File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
        }
    }

    public int PruneOldLogs(int retentionDays, DateTimeOffset? now = null)
    {
        if (retentionDays <= 0 || !Directory.Exists(LogsDirectory))
        {
            return 0;
        }

        var cutoff = (now ?? DateTimeOffset.UtcNow).UtcDateTime.Date.AddDays(1 - retentionDays);
        var removed = 0;

        lock (_gate)
        {
            foreach (var path in Directory.EnumerateFiles(LogsDirectory, "audit-*.jsonl"))
            {
                if (!TryGetLogFileDate(path, out var fileDate) || fileDate >= cutoff)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    removed++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return removed;
    }

    private static bool TryGetLogFileDate(string path, out DateTime date)
    {
        const string prefix = "audit-";
        var fileName = Path.GetFileNameWithoutExtension(path);

        if (fileName.Length <= prefix.Length || !fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            date = default;
            return false;
        }

        return DateTime.TryParseExact(
            fileName[prefix.Length..],
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}
